using System.Data;
using System.Diagnostics;
using Arvan.Capacity.Application.Common.Exceptions;
using Arvan.Capacity.Application.Telemetry;
using Dapper;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Arvan.Capacity.Infrastructure.Persistence;

/// <summary>
/// The one connection and transaction a scoped unit of work shares. The write repositories borrow
/// it, which is what lets them join the UseCase's transaction without being told about it.
/// </summary>
/// <remarks>
/// <para>
/// Writes whose outcome nobody needs at once are <see cref="Queue">queued</see> and travel to the
/// database together, in one round trip, when the next statement that needs an answer runs or the
/// transaction commits. Every immediate statement flushes the queue first, so the database sees
/// exactly the sequence the code wrote and a read always sees the writes before it. This is what
/// lets a hold cost a handful of round trips instead of one per row.
/// </para>
/// <para>
/// Timeouts are connection startup options, not per-transaction <c>SET LOCAL</c> statements, so the
/// transaction starts with no round trip but <c>BEGIN</c>.
/// </para>
/// </remarks>
internal sealed class PostgresSession(NpgsqlDataSource dataSource, IOptions<DatabaseOptions> options)
{
    private readonly List<PendingCommand> _pending = [];
    private NpgsqlConnection? _connection;
    private NpgsqlTransaction? _transaction;
    private int _savepoints;
    private int _roundTrips;

    /// <summary>
    /// A write waiting to be sent, and what the database reported once it was.
    /// </summary>
    public sealed class PendingCommand
    {
        internal PendingCommand(NpgsqlBatchCommand command, int? expectedAffected, string? mismatchMessage)
        {
            Command = command;
            ExpectedAffected = expectedAffected;
            MismatchMessage = mismatchMessage;
        }

        internal NpgsqlBatchCommand Command { get; }

        internal int? ExpectedAffected { get; }

        internal string? MismatchMessage { get; }

        /// <summary>
        /// The number of rows the statement affected; <c>-1</c> until the queue has been flushed.
        /// </summary>
        public int RecordsAffected { get; internal set; } = -1;
    }

    /// <summary>
    /// Whether a transaction is open.
    /// </summary>
    public bool InTransaction => _transaction is not null;

    private NpgsqlConnection Connection => _connection
        ?? throw new InvalidOperationException("A repository was used outside a UseCase's transaction.");

    /// <summary>
    /// The open transaction.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown outside a UseCase's transaction.</exception>
    public NpgsqlTransaction Transaction => _transaction
        ?? throw new InvalidOperationException("A repository was used outside a UseCase's transaction.");

    // ───────────────────────── queued writes ─────────────────────────

    /// <summary>
    /// Queues a write to be sent with the next flush. No I/O.
    /// </summary>
    /// <param name="sql">The statement, with <c>@name</c> parameters.</param>
    /// <param name="expectedAffected">When set, the flush fails if the statement affects a different number of rows.</param>
    /// <param name="mismatchMessage">What to say when it does.</param>
    /// <param name="parameters">The parameters; a <c>null</c> value is sent as SQL NULL.</param>
    public PendingCommand Queue(string sql, int? expectedAffected, string? mismatchMessage, params (string Name, object? Value)[] parameters)
    {
        var command = new NpgsqlBatchCommand(sql);
        foreach (var (name, value) in parameters)
            command.Parameters.Add(new NpgsqlParameter(name, value ?? DBNull.Value));

        var pending = new PendingCommand(command, expectedAffected, mismatchMessage);
        _pending.Add(pending);
        return pending;
    }

    /// <summary>
    /// Queues a write with no row-count expectation.
    /// </summary>
    /// <param name="sql">The statement, with <c>@name</c> parameters.</param>
    /// <param name="parameters">The parameters.</param>
    public PendingCommand Queue(string sql, params (string Name, object? Value)[] parameters) =>
        Queue(sql, null, null, parameters);

    /// <summary>
    /// Sends every queued write in one round trip.
    /// </summary>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    public async Task FlushAsync(CancellationToken ct)
    {
        if (_pending.Count is 0)
            return;

        var sent = _pending.ToArray();
        _pending.Clear();

        await using var batch = new NpgsqlBatch(Connection, Transaction);
        foreach (var p in sent)
            batch.BatchCommands.Add(p.Command);

        _roundTrips++;

        try
        {
            await batch.ExecuteNonQueryAsync(ct);
        }
        catch (PostgresException e) when (e.ConstraintName is "reservations_client_reference_live")
        {
            throw new ReservationClientReferenceTakenException(null);
        }

        foreach (var p in sent)
        {
            p.RecordsAffected = p.Command.RecordsAffected;

            if (p.ExpectedAffected is { } expected && p.RecordsAffected != expected)
                throw new InvalidOperationException(p.MismatchMessage ?? "A write did not affect the expected number of rows.");
        }
    }

    // ───────────────────── immediate statements (flush first) ─────────────────────

    /// <summary>
    /// Runs a query, after sending anything queued.
    /// </summary>
    public async Task<IEnumerable<T>> QueryAsync<T>(CommandDefinition command)
    {
        await PrepareAsync(command.CancellationToken);
        return await Connection.QueryAsync<T>(command);
    }

    /// <summary>
    /// Runs a query for at most one row, after sending anything queued.
    /// </summary>
    public async Task<T?> QuerySingleOrDefaultAsync<T>(CommandDefinition command)
    {
        await PrepareAsync(command.CancellationToken);
        return await Connection.QuerySingleOrDefaultAsync<T>(command);
    }

    /// <summary>
    /// Runs a query for exactly one row, after sending anything queued.
    /// </summary>
    public async Task<T> QuerySingleAsync<T>(CommandDefinition command)
    {
        await PrepareAsync(command.CancellationToken);
        return await Connection.QuerySingleAsync<T>(command);
    }

    /// <summary>
    /// Runs a query for one value, after sending anything queued.
    /// </summary>
    public async Task<T?> ExecuteScalarAsync<T>(CommandDefinition command)
    {
        await PrepareAsync(command.CancellationToken);
        return await Connection.ExecuteScalarAsync<T>(command);
    }

    /// <summary>
    /// Runs a write at once, after sending anything queued.
    /// </summary>
    public async Task<int> ExecuteAsync(CommandDefinition command)
    {
        await PrepareAsync(command.CancellationToken);
        return await Connection.ExecuteAsync(command);
    }

    /// <summary>
    /// The connection for a read that builds its own command, after sending anything queued.
    /// </summary>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    public async Task<NpgsqlConnection> ReadConnectionAsync(CancellationToken ct)
    {
        await PrepareAsync(ct);
        return Connection;
    }

    /// <summary>
    /// A command definition inside the open transaction.
    /// </summary>
    public CommandDefinition Command(string sql, object? parameters, CancellationToken ct) =>
        new(sql, parameters, Transaction, cancellationToken: ct);

    private async Task PrepareAsync(CancellationToken ct)
    {
        await FlushAsync(ct);
        _roundTrips++;
    }

    // ───────────────────────── the transaction ─────────────────────────

    /// <summary>
    /// Runs <paramref name="work"/> in one READ COMMITTED transaction, re-running it from the start
    /// after a deadlock or serialization failure.
    /// </summary>
    /// <param name="work">The transactional body.</param>
    /// <param name="ct">Cancels the operation, including any pending retry.</param>
    public async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct)
    {
        try
        {
            return await RunCoreAsync(work, ct);
        }
        catch (Exception e) when (IsUnavailable(e, ct))
        {
            // Nothing was committed, so the client may retry with the same idempotency key.
            throw new ServiceUnavailableException(e);
        }
    }

    private async Task<T> RunCoreAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct)
    {
        if (InTransaction)
            throw new InvalidOperationException("A transaction is already open — this call site is not the transaction root.");

        for (var attempt = 0; ; attempt++)
        {
            var waitStarted = Stopwatch.GetTimestamp();
            await using var connection = await dataSource.OpenConnectionAsync(ct);
            CapacityTelemetry.DbPoolWait.Record(Stopwatch.GetElapsedTime(waitStarted).TotalSeconds);

            await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

            _connection = connection;
            _transaction = transaction;
            _pending.Clear();
            _roundTrips = 2; // BEGIN and COMMIT

            try
            {
                var result = await work(ct);
                await FlushAsync(ct);
                await transaction.CommitAsync(ct);

                CapacityTelemetry.TransactionRoundTrips.Record(_roundTrips);
                return result;
            }
            catch (PostgresException e) when (IsRetryable(e) && attempt < options.Value.MaxRetries)
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }
            finally
            {
                _pending.Clear();
                _connection = null;
                _transaction = null;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(10 * (attempt + 1)), ct);
        }
    }

    /// <summary>
    /// Runs <paramref name="attempt"/> under a savepoint, rolling back to it when it returns
    /// <c>false</c> or throws.
    /// </summary>
    /// <remarks>
    /// The savepoint itself is queued, so it costs no round trip of its own. If the attempt fails
    /// before anything was sent, rolling back just forgets the queue and costs nothing at all.
    /// </remarks>
    /// <param name="attempt">The writes to try.</param>
    /// <param name="ct">Cancels the operation.</param>
    public async Task<bool> AttemptAsync(Func<CancellationToken, Task<bool>> attempt, CancellationToken ct)
    {
        _ = Transaction;
        var name = $"attempt_{++_savepoints}";
        var savepoint = Queue($"SAVEPOINT {name}");

        try
        {
            if (await attempt(ct))
                return true;
        }
        catch
        {
            await RollbackAsync(savepoint, name);
            throw;
        }

        await RollbackAsync(savepoint, name, ct);
        return false;
    }

    private async Task RollbackAsync(PendingCommand savepoint, string name, CancellationToken ct = default)
    {
        var index = _pending.IndexOf(savepoint);

        if (index >= 0)
        {
            // Never sent: forget it and everything after it.
            _pending.RemoveRange(index, _pending.Count - index);
            return;
        }

        _pending.Clear();
        _roundTrips++;
        await using var command = new NpgsqlCommand($"ROLLBACK TO SAVEPOINT {name}", Connection, Transaction);
        await command.ExecuteNonQueryAsync(CancellationToken.None);
    }

    /// <summary>
    /// The database time: the open transaction's start, or the current time outside one.
    /// </summary>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    public async Task<DateTimeOffset> NowAsync(CancellationToken ct)
    {
        if (InTransaction)
        {
            await PrepareAsync(ct);
            await using var inTransaction = new NpgsqlCommand("SELECT now()", Connection, Transaction);
            return ToUtc(await inTransaction.ExecuteScalarAsync(ct));
        }

        await using var command = dataSource.CreateCommand("SELECT now()");
        return ToUtc(await command.ExecuteScalarAsync(ct));
    }

    // timestamptz comes back from ExecuteScalar as a UTC DateTime.
    private static DateTimeOffset ToUtc(object? value) =>
        new(DateTime.SpecifyKind((DateTime)value!, DateTimeKind.Utc));

    // A lock or statement timeout, a shutdown or failover in progress, too many connections, or an
    // unreachable server: the service cannot decide right now.
    private static bool IsUnavailable(Exception e, CancellationToken ct) =>
        !ct.IsCancellationRequested
        && e switch
        {
            PostgresException p => p.SqlState is PostgresErrorCodes.LockNotAvailable or PostgresErrorCodes.QueryCanceled
                or PostgresErrorCodes.AdminShutdown or PostgresErrorCodes.CrashShutdown or PostgresErrorCodes.CannotConnectNow
                or PostgresErrorCodes.TooManyConnections,
            NpgsqlException n => n.IsTransient || n.InnerException is System.Net.Sockets.SocketException or IOException,
            _ => false,
        };

    private static bool IsRetryable(PostgresException e) =>
        e.SqlState is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected;
}
