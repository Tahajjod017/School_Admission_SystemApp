using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// The browser (frontend on :3000) calls this API directly, so CORS must allow it.
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy => policy
        .AllowAnyOrigin()
        .AllowAnyHeader()   // includes X-Student-Id
        .AllowAnyMethod()));

var app = builder.Build();
app.UseCors();

// Both values come from environment variables (see docker-compose.yml):
//   ConnectionStrings__Default  and  HOLD_DURATION_SECONDS
var connectionString = app.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.");
var holdDurationSeconds = app.Configuration.GetValue<int?>("HOLD_DURATION_SECONDS") ?? 600;

app.MapGet("/health", async (CancellationToken ct) =>
{
    try
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("select 1", conn);
        await cmd.ExecuteScalarAsync(ct);
        return Results.Ok(new { status = "ok", database = "up", holdDurationSeconds });
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Health check: database unreachable");
        return Results.Json(
            new { status = "degraded", database = "down", holdDurationSeconds },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.Run();
