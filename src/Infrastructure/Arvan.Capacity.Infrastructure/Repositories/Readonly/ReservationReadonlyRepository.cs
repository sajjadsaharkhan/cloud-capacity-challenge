using Arvan.Capacity.Application.Repositories.Readonly;
using Arvan.Capacity.Domain.Enums;
using Arvan.Capacity.Framework.Domain.Abstractions;
using Arvan.Capacity.Infrastructure.Persistence;
using Dapper;
using Npgsql;

namespace Arvan.Capacity.Infrastructure.Repositories.Readonly;

/// <inheritdoc cref="IReservationReadonlyRepository"/>
internal sealed class ReservationReadonlyRepository(NpgsqlDataSource dataSource) : IReservationReadonlyRepository
{
    private const string Columns =
        """
        id AS Id, effective_status AS Status, settled AS Settled, effective_reason_code AS ReasonCode,
        product_code AS ProductCode, region AS Region, zone AS Zone, expires_at AS ExpiresAt,
        wait_deadline AS WaitDeadline, extended AS Extended, allocation_state AS AllocationState,
        client_reference AS ClientReference, version AS Version, created_at AS CreatedAt, updated_at AS UpdatedAt
        """;

    // A class with setters rather than a positional record: Dapper matches a constructor by exact
    // column types, and timestamptz arrives as DateTime where the projection wants DateTimeOffset.
    private sealed class Row
    {
        public Guid Id { get; set; }
        public string Status { get; set; } = default!;
        public bool Settled { get; set; }
        public string? ReasonCode { get; set; }
        public string ProductCode { get; set; } = default!;
        public string Region { get; set; } = default!;
        public string? Zone { get; set; }
        public DateTimeOffset? ExpiresAt { get; set; }
        public DateTimeOffset? WaitDeadline { get; set; }
        public bool Extended { get; set; }
        public string? AllocationState { get; set; }
        public string? ClientReference { get; set; }
        public int Version { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }

    /// <inheritdoc cref="IReservationReadonlyRepository.GetByIdAsync"/>
    public async Task<ReservationSummary?> GetByIdAsync(Guid id, string customerId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);

        var row = await connection.QuerySingleOrDefaultAsync<Row>(new CommandDefinition(
            $"SELECT {Columns} FROM reservation_effective WHERE id = @id AND customer_id = @customerId",
            new { id, customerId },
            cancellationToken: ct));

        return row is null ? null : (await ToSummariesAsync(connection, [row], ct))[0];
    }

    /// <inheritdoc cref="IReservationReadonlyRepository.ListAsync"/>
    public async Task<PagedData<ReservationSummary>> ListAsync(
        string customerId, ReservationListFilter filter, int pageNumber, int pageSize, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);

        var parameters = new
        {
            customerId,
            statuses = filter.Statuses.Select(s => DbEnum.ToDb(s)).ToArray(),
            productCode = filter.ProductCode,
            region = filter.Region,
            limit = pageSize,
            offset = (pageNumber - 1) * pageSize,
        };

        const string where =
            """
            WHERE customer_id = @customerId
              AND (cardinality(@statuses) = 0 OR effective_status = ANY(@statuses))
              AND (@productCode::text IS NULL OR product_code = @productCode)
              AND (@region::text IS NULL OR region = @region)
            """;

        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT count(*) FROM reservation_effective {where}", parameters, cancellationToken: ct));

        var rows = (await connection.QueryAsync<Row>(new CommandDefinition(
            $"SELECT {Columns} FROM reservation_effective {where} ORDER BY created_at DESC, id LIMIT @limit OFFSET @offset",
            parameters,
            cancellationToken: ct))).ToList();

        return PagedData<ReservationSummary>.Create(await ToSummariesAsync(connection, rows, ct), total, pageNumber, pageSize);
    }

    private static async Task<IReadOnlyList<ReservationSummary>> ToSummariesAsync(
        NpgsqlConnection connection, IReadOnlyList<Row> rows, CancellationToken ct)
    {
        var lines = (await connection.QueryAsync<(Guid ReservationId, string ResourceClass, long Quantity)>(
                new CommandDefinition(
                    """
                    SELECT reservation_id AS ReservationId, resource_class AS ResourceClass, quantity AS Quantity
                    FROM reservation_lines WHERE reservation_id = ANY(@ids) ORDER BY resource_class
                    """,
                    new { ids = rows.Select(r => r.Id).ToArray() },
                    cancellationToken: ct)))
            .ToLookup(l => l.ReservationId);

        return
        [
            .. rows.Select(r => new ReservationSummary(
                r.Id,
                DbEnum.FromDb<ReservationStatus>(r.Status),
                r.Settled,
                r.ReasonCode is null ? null : DbEnum.FromDb<ReasonCode>(r.ReasonCode),
                r.ProductCode,
                r.Region,
                r.Zone,
                [.. lines[r.Id].Select(l => new ReservationLineSummary(DbEnum.FromDb<ResourceClass>(l.ResourceClass), l.Quantity))],
                r.ExpiresAt,
                r.WaitDeadline,
                r.Extended,
                r.AllocationState is null ? null : DbEnum.FromDb<AllocationState>(r.AllocationState),
                r.ClientReference,
                r.Version,
                r.CreatedAt,
                r.UpdatedAt)),
        ];
    }
}
