using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Arvan.Capacity.Invariants.Tests;

/// <summary>
/// The HTTP contract: status codes, headers, the response envelope, identity and idempotency
/// headers, limits, health and metrics, and the OpenAPI document.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ApiContractTests(PostgresFixture fixture) : IAsyncLifetime
{
    private WebApplicationFactory<Program> _factory = default!;
    private HttpClient _client = default!;
    private string _connectionString = default!;

    public async Task InitializeAsync()
    {
        _connectionString = await fixture.CreateDatabaseAsync();
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Database", _connectionString);
            builder.UseSetting("Database:Migrate", "false");
        });
        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        Npgsql.NpgsqlConnection.ClearAllPools();
    }

    private static HttpRequestMessage Post(string path, string customer, string? key, object? body, string kind = "CUSTOMER")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = body is null ? null : new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("X-Customer-Id", customer);
        request.Headers.Add("X-Actor-Kind", kind);
        request.Headers.Add("X-Actor-Id", $"{customer}-actor");
        if (key is not null) request.Headers.Add("Idempotency-Key", key);
        return request;
    }

    private static HttpRequestMessage Get(string path, string customer, string kind = "CUSTOMER")
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Customer-Id", customer);
        request.Headers.Add("X-Actor-Kind", kind);
        request.Headers.Add("X-Actor-Id", $"{customer}-actor");
        return request;
    }

    private static object Vcpu(long quantity, string? zone = "ir-thr-1", string? onShortage = null) => new
    {
        product = "vm-standard", region = "ir-thr", zone, onShortage,
        lines = new[] { new { resourceClass = "Vcpu", quantity } },
    };

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>());

    [Fact]
    public async Task Submit_answers_201_with_location_and_replays_with_the_header()
    {
        var first = await _client.SendAsync(Post("/v1/reservations", "api1", "k1", Vcpu(1000)));
        var body = await Json(first);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.True(body.GetProperty("isSuccess").GetBoolean());
        Assert.Equal("Held", body.GetProperty("data").GetProperty("status").GetString());
        Assert.Equal($"/v1/reservations/{body.GetProperty("data").GetProperty("id").GetString()}", first.Headers.Location!.ToString());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("traceId").GetString()));

        var replay = await _client.SendAsync(Post("/v1/reservations", "api1", "k1", Vcpu(1000)));
        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        Assert.Equal("true", replay.Headers.GetValues("Idempotent-Replayed").Single());
        Assert.Equal(body.GetProperty("data").GetProperty("id").GetString(), (await Json(replay)).GetProperty("data").GetProperty("id").GetString());

        var reused = await _client.SendAsync(Post("/v1/reservations", "api1", "k1", Vcpu(2000)));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, reused.StatusCode);
        Assert.Equal("idempotency.key_reused", (await Json(reused)).GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_stored_refusal_is_409_or_422_with_the_reservation_in_data_and_a_retry_hint()
    {
        using var db = new Npgsql.NpgsqlConnection(_connectionString);
        await db.OpenAsync();
        await new Npgsql.NpgsqlCommand(
            "UPDATE inventories SET total = 2000, reserved = 0, allocation_ratio = 1, min_unit = 1000, step_size = 1000, max_unit = 8000 WHERE resource_class = 'VCPU' AND pool_id = (SELECT id FROM pools WHERE zone = 'ir-thr-1' AND host_ref IS NULL)", db)
            .ExecuteNonQueryAsync();

        await _client.SendAsync(Post("/v1/reservations", "api2", "a", Vcpu(2000)));

        var temporary = await _client.SendAsync(Post("/v1/reservations", "api2", "b", Vcpu(1000)));
        var temp = await Json(temporary);
        Assert.Equal(HttpStatusCode.Conflict, temporary.StatusCode);
        Assert.False(temp.GetProperty("isSuccess").GetBoolean());
        Assert.Equal("capacity.temporarily_unavailable", temp.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal("Rejected", temp.GetProperty("data").GetProperty("status").GetString());
        Assert.True(temporary.Headers.Contains("Retry-After"));

        var exceeds = await _client.SendAsync(Post("/v1/reservations", "api2", "c", Vcpu(8000)));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, exceeds.StatusCode);
        Assert.Equal("capacity.exceeds_registered", (await Json(exceeds)).GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_waiting_reservation_is_202()
    {
        using var db = new Npgsql.NpgsqlConnection(_connectionString);
        await db.OpenAsync();
        await new Npgsql.NpgsqlCommand(
            "UPDATE inventories SET total = 1000, reserved = 0, allocation_ratio = 1, min_unit = 1000, step_size = 1000, max_unit = 1000 WHERE resource_class = 'VCPU' AND pool_id = (SELECT id FROM pools WHERE zone = 'ir-thr-1' AND host_ref IS NULL)", db)
            .ExecuteNonQueryAsync();

        await _client.SendAsync(Post("/v1/reservations", "api3", "a", Vcpu(1000)));
        var waiting = await _client.SendAsync(Post("/v1/reservations", "api3", "b", Vcpu(1000, onShortage: "Wait")));

        Assert.Equal(HttpStatusCode.Accepted, waiting.StatusCode);
        Assert.NotNull(waiting.Headers.Location);
        Assert.Equal("Waiting", (await Json(waiting)).GetProperty("data").GetProperty("status").GetString());
    }

    [Fact]
    public async Task Missing_identity_is_401_missing_key_is_400_and_a_bad_body_lists_the_fields()
    {
        var noIdentity = await _client.PostAsJsonAsync("/v1/reservations", Vcpu(1000));
        Assert.Equal(HttpStatusCode.Unauthorized, noIdentity.StatusCode);
        Assert.Equal("identity.missing", (await Json(noIdentity)).GetProperty("error").GetProperty("code").GetString());

        var noKey = await _client.SendAsync(Post("/v1/reservations", "api4", null, Vcpu(1000)));
        Assert.Equal(HttpStatusCode.BadRequest, noKey.StatusCode);
        Assert.Equal("idempotency.key_required", (await Json(noKey)).GetProperty("error").GetProperty("code").GetString());

        var badKey = await _client.SendAsync(Post("/v1/reservations", "api4", "has space!", Vcpu(1000)));
        Assert.Equal(HttpStatusCode.BadRequest, badKey.StatusCode);

        var bad = await _client.SendAsync(Post("/v1/reservations", "api4", "k", new { product = "", region = "ir-thr", lines = Array.Empty<object>() }));
        var error = (await Json(bad)).GetProperty("error");
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.True(error.GetProperty("details").GetArrayLength() >= 2);
    }

    [Fact]
    public async Task Other_customers_get_404_customers_cannot_release_and_unknown_routes_still_use_the_envelope()
    {
        var created = await Json(await _client.SendAsync(Post("/v1/reservations", "owner", "k", Vcpu(1000))));
        var id = created.GetProperty("data").GetProperty("id").GetString();

        var theirs = await _client.SendAsync(Get($"/v1/reservations/{id}", "intruder"));
        Assert.Equal(HttpStatusCode.NotFound, theirs.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.SendAsync(Post($"/v1/reservations/{id}/confirm", "intruder", "c", null))).StatusCode);

        var release = await _client.SendAsync(Post($"/v1/reservations/{id}/release", "owner", "r", new { reason = "ServiceDeleted" }));
        Assert.Equal(HttpStatusCode.Forbidden, release.StatusCode);

        var missing = await _client.GetAsync("/v1/nothing");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("endpoint_not_found", (await Json(missing)).GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Transitions_answer_200_and_an_invalid_one_is_409()
    {
        var id = (await Json(await _client.SendAsync(Post("/v1/reservations", "tr", "k", Vcpu(1000))))).GetProperty("data").GetProperty("id").GetString();

        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(Post($"/v1/reservations/{id}/extend", "tr", "e1", null))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await _client.SendAsync(Post($"/v1/reservations/{id}/extend", "tr", "e2", null))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(Post($"/v1/reservations/{id}/confirm", "tr", "c1", null))).StatusCode);

        var cancel = await _client.SendAsync(Post($"/v1/reservations/{id}/cancel", "tr", "x1", null));
        Assert.Equal(HttpStatusCode.Conflict, cancel.StatusCode);
        Assert.Equal("reservation.invalid_state_transition", (await Json(cancel)).GetProperty("error").GetProperty("code").GetString());

        var list = await Json(await _client.SendAsync(Get("/v1/reservations?status=Confirmed&pageSize=5", "tr")));
        Assert.Equal(1, list.GetProperty("data").GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task Request_rate_is_limited_per_customer_with_a_retry_hint()
    {
        var statuses = new List<HttpStatusCode>();
        HttpResponseMessage? limited = null;

        for (var i = 0; i < 60; i++)
        {
            var response = await _client.SendAsync(Post("/v1/capacity/fit-check", "rate", null, new
            {
                product = "vm-standard", region = "ir-thr", lines = new[] { new { resourceClass = "Vcpu", quantity = 500 } },
            }));
            statuses.Add(response.StatusCode);
            if (response.StatusCode is HttpStatusCode.TooManyRequests) limited ??= response;
        }

        Assert.NotNull(limited);
        Assert.True(limited!.Headers.Contains("Retry-After"));
        Assert.Equal("rate.limited", (await Json(limited)).GetProperty("error").GetProperty("code").GetString());

        // Another customer is unaffected.
        Assert.Equal(HttpStatusCode.Created, (await _client.SendAsync(Post("/v1/reservations", "calm", "c", Vcpu(500)))).StatusCode);
    }

    [Fact]
    public async Task A_body_over_16_KB_is_refused()
    {
        var huge = new { product = "vm-standard", region = "ir-thr", clientReference = new string('x', 20_000), lines = new[] { new { resourceClass = "Vcpu", quantity = 500 } } };
        var response = await _client.SendAsync(Post("/v1/reservations", "big", "k", huge));

        Assert.True(response.StatusCode is HttpStatusCode.RequestEntityTooLarge or HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Capacity_levels_fit_check_and_the_admin_api_follow_the_actor_kind()
    {
        var levels = await Json(await _client.SendAsync(Get("/v1/capacity", "cap")));
        Assert.True(levels.GetProperty("data").GetProperty("items").GetArrayLength() > 0);
        Assert.False(levels.GetProperty("data").TryGetProperty("pools", out var pools) && pools.ValueKind is JsonValueKind.Array);

        Assert.Equal(HttpStatusCode.Forbidden, (await _client.SendAsync(Get("/v1/capacity?detail=true", "cap"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(Get("/v1/capacity?detail=true", "cap", "SERVICE"))).StatusCode);

        var fit = await Json(await _client.SendAsync(Post("/v1/capacity/fit-check", "cap", null, new
        {
            product = "vm-standard", region = "ir-thr", lines = new[] { new { resourceClass = "Vcpu", quantity = 1000 } },
        })));
        Assert.True(fit.GetProperty("data").GetProperty("fits").GetBoolean());

        var asCustomer = await _client.SendAsync(AdminPut("/v1/admin/quotas/cap/Vcpu/ir-thr", "CUSTOMER", "q1", new { limit = 5 }));
        Assert.Equal(HttpStatusCode.Forbidden, asCustomer.StatusCode);

        var asAdmin = await _client.SendAsync(AdminPut("/v1/admin/quotas/cap/Vcpu/ir-thr", "ADMIN", "q2", new { limit = 5 }));
        Assert.Equal(HttpStatusCode.OK, asAdmin.StatusCode);

        var report = await Json(await _client.SendAsync(Get("/v1/admin/reconciliation?refresh=true", "x", "ADMIN")));
        Assert.Equal(0, report.GetProperty("data").GetProperty("driftRows").GetInt32());
    }

    private static HttpRequestMessage AdminPut(string path, string kind, string key, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, path)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("X-Actor-Kind", kind);
        request.Headers.Add("X-Actor-Id", "tester");
        request.Headers.Add("Idempotency-Key", key);
        return request;
    }

    [Fact]
    public async Task Health_and_metrics_endpoints_are_served()
    {
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/health/ready")).StatusCode);

        await _client.SendAsync(Post("/v1/reservations", "metrics", "k", Vcpu(1000)));
        var metrics = await (await _client.GetAsync("/metrics")).Content.ReadAsStringAsync();
        Assert.Contains("capacity_holds_total", metrics);
        Assert.Contains("capacity_hold_duration_seconds", metrics);
    }

    [Fact]
    public async Task The_openapi_document_matches_the_committed_one()
    {
        var generated = await _client.GetStringAsync("/openapi/v1.json");
        var normalized = JsonSerializer.Serialize(JsonDocument.Parse(generated).RootElement, new JsonSerializerOptions { WriteIndented = true });

        var path = Path.Combine(RepositoryRoot(), "docs", "openapi", "capacity.v1.json");

        // `UPDATE_OPENAPI=1 dotnet test` rewrites it, as the reviewed diff of an intended API change.
        if (Environment.GetEnvironmentVariable("UPDATE_OPENAPI") is "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, normalized + "\n");
        }

        Assert.True(File.Exists(path), "Run `UPDATE_OPENAPI=1 dotnet test` once to commit docs/openapi/capacity.v1.json.");
        Assert.Equal((await File.ReadAllTextAsync(path)).TrimEnd(), normalized.TrimEnd());
    }

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Makefile")))
            dir = dir.Parent;

        return dir?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
