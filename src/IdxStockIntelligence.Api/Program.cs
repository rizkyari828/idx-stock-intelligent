using System.Text.Json.Serialization;
using IdxStockIntelligence.Api;
using IdxStockIntelligence.Application;
using IdxStockIntelligence.Domain;
using IdxStockIntelligence.Infrastructure;
using Npgsql;

var uiRoot = Path.GetFullPath(Environment.GetEnvironmentVariable("IDX_UI_ROOT") ?? "frontend/dist");
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args, WebRootPath = Directory.Exists(uiRoot) ? uiRoot : null
});
var address = builder.Configuration["urls"] ?? "http://127.0.0.1:5080";
if (!Uri.TryCreate(address, UriKind.Absolute, out var listenUri) || !listenUri.IsLoopback || listenUri.Scheme != "http")
    throw new ArgumentException("API requires one loopback HTTP address.");
builder.WebHost.UseUrls(address);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 24 * 1024 * 1024);
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
    options.SerializerOptions.NumberHandling = JsonNumberHandling.AllowReadingFromString;
});
var connectionString = builder.Configuration["IDX_DATABASE_CONNECTION"]
    ?? throw new InvalidOperationException("Set IDX_DATABASE_CONNECTION to a PostgreSQL connection string.");
builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
builder.Services.AddSingleton<PortfolioDatabase>();
var app = builder.Build();
app.Use(async (context, next) =>
{
    // Existing writes retain their smaller limit; only bounded exchange content needs more room.
    if (!context.Request.Path.StartsWithSegments("/api/portfolio-imports"))
    {
        var bodyLimit = context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>();
        if (bodyLimit is { IsReadOnly: false }) bodyLimit.MaxRequestBodySize = 65536;
    }
    // Local writes require same-origin JSON. Vite proxies /api; no permissive CORS.
    if (HttpMethods.IsPost(context.Request.Method)
        && (!context.Request.HasJsonContentType() || context.Request.Headers["Sec-Fetch-Site"] == "cross-site"))
    {
        context.Response.StatusCode = 415;
        return;
    }
    try { await next(context); }
    catch (Exception exception) when (exception is ArgumentException or KeyNotFoundException or InvalidOperationException or NpgsqlException)
    {
        var status = exception switch { KeyNotFoundException => 404, ArgumentException => 400,
            PostgresException { SqlState: "23505" } => 409, PostgresException { SqlState: "23503" or "23514" } => 400, _ => 503 };
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new { operation = OperationStatus.FAILED,
            error = exception is NpgsqlException ? "Database constraint or availability error." : exception.Message }, context.RequestAborted);
    }
});

app.MapPost("/api/portfolios", async (CreatePortfolio input, PortfolioDatabase db, CancellationToken ct) =>
    Results.Ok(await db.CreateAsync(input.Name, input.AllowNegativeCash, ct)));
app.MapPost("/api/portfolios/{id:guid}/events", async (Guid id, EventInput input, PortfolioDatabase db, CancellationToken ct) =>
{
    var result = await db.AppendAsync(id, input, ct);
    return Results.Ok(new { operation = OperationStatus.SUCCESS, result.Event, result.Duplicate });
});
app.MapGet("/api/portfolios/{id:guid}/events", async (Guid id, DateTimeOffset? cutoff, int? offset, int? limit, PortfolioDatabase db, CancellationToken ct) =>
    Results.Ok(await db.EventsAsync(id, ProductQuery.Cutoff(cutoff), offset ?? 0, limit ?? 100, ct)));
app.MapGet("/api/portfolios/{id:guid}", async (Guid id, DateOnly? through, DateTimeOffset? cutoff, PortfolioDatabase db, CancellationToken ct) =>
    Results.Ok(await db.ViewAsync(id, ProductQuery.Date(through), ProductQuery.Cutoff(cutoff), ct)));
app.MapGet("/api/portfolios/{id:guid}/holdings", async (Guid id, DateOnly? through, DateTimeOffset? cutoff, int? offset, int? limit, PortfolioDatabase db, CancellationToken ct) =>
{
    PortfolioDatabase.ValidatePage(offset ?? 0, limit ?? 100);
    var view = await db.ViewAsync(id, ProductQuery.Date(through), ProductQuery.Cutoff(cutoff), ct);
    return Results.Ok(new { view.Operation, view.ValuationCoverage, view.PricedHoldings, view.HoldingCount,
        holdings = view.Holdings.Skip(offset ?? 0).Take(limit ?? 100) });
});
app.MapPost("/api/portfolios/{id:guid}/holdings/{instrument:guid}/theses", async (Guid id, Guid instrument, ThesisInput input, PortfolioDatabase db, CancellationToken ct) =>
    Results.Ok(await db.AddThesisAsync(id, instrument, input, ct)));
app.MapGet("/api/portfolios/{id:guid}/holdings/{instrument:guid}/theses", async (Guid id, Guid instrument, DateTimeOffset? cutoff, int? offset, int? limit, PortfolioDatabase db, CancellationToken ct) =>
    Results.Ok(await db.ThesesAsync(id, instrument, ProductQuery.Cutoff(cutoff), offset ?? 0, limit ?? 100, ct)));
app.MapGet("/api/instruments/{id:guid}/market-state", async (Guid id, DateOnly? through, DateTimeOffset? cutoff, PortfolioDatabase db, CancellationToken ct) =>
    Results.Ok(await db.MarketAsync(id, ProductQuery.Date(through), ProductQuery.Cutoff(cutoff), ct)));
app.MapPost("/api/instruments", async (RegisterInstrument input, PortfolioDatabase db, CancellationToken ct) =>
{
    await db.RegisterInstrumentAsync(input.Id, input.Name, input.Symbol, input.Type, input.ValidFrom, ct);
    return Results.Ok(input);
});
app.MapGet("/api/instruments", async (int? offset, int? limit, DateOnly? through, PortfolioDatabase db, CancellationToken ct) =>
    Results.Ok(await db.InstrumentsAsync(offset ?? 0, limit ?? 100, ct, through)));
app.MapGet("/api/portfolios/{id:guid}/export", async (Guid id, string? format, PortfolioDatabase db, CancellationToken ct) =>
{
    var document = await db.ExportAsync(id, ct);
    return format switch
    {
        null or "JSON" => Results.Text(PortfolioExchange.Serialize(document), "application/json", System.Text.Encoding.UTF8),
        "CSV" => Results.Text(PortfolioExchange.CsvExport(document), "text/csv", System.Text.Encoding.UTF8),
        _ => Results.BadRequest(new { error = "format must be JSON or CSV." })
    };
});
app.MapPost("/api/portfolio-imports/preview", async (ImportRequest input, PortfolioDatabase db, CancellationToken ct) =>
    Results.Ok(await db.PreviewImportAsync(input, ct)));
app.MapPost("/api/portfolios/{id:guid}/reconciliation/preview", async (Guid id, ReconciliationInput input, PortfolioDatabase db, CancellationToken ct) =>
    Results.Ok(await db.ReconcileAsync(id, input, ct)));
app.MapPost("/api/portfolio-imports", async (ImportRequest input, PortfolioDatabase db, CancellationToken ct) =>
    Results.Ok(await db.ImportAsync(input, ct)));
app.UseDefaultFiles();
app.UseStaticFiles();
await app.RunAsync();

namespace IdxStockIntelligence.Api
{
    public sealed record CreatePortfolio(string Name, bool AllowNegativeCash = false);
    public sealed record RegisterInstrument(Guid Id, string Name, string Symbol, string Type, DateOnly ValidFrom);
}
