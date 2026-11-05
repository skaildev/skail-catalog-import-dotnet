using System.Text.Json;
using System.Text.Json.Serialization;
using SkailCatalogImport.Contracts;
using SkailCatalogImport.Web.Imports;
using SkailCatalogImport.Web.Skail;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSingleton<ImportStore>();
builder.Services.AddSingleton(SkailOptions.FromEnvironment());
builder.Services.AddHttpClient<ISkailGateway, SkailApiGateway>(c => c.Timeout = TimeSpan.FromSeconds(10));

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions { OnPrepareResponse = ctx => ctx.Context.Response.Headers.CacheControl = "no-cache" });

app.MapGet("/healthz", () => Results.Ok("ok"));

app.MapPost("/api/imports", async (StartRequest? body, ImportStore store, ISkailGateway skail, CancellationToken ct) =>
{
    var importId = Guid.NewGuid().ToString();
    var state = store.Create(importId, body?.SimulateFailure ?? false);
    try
    {
        await skail.TriggerAsync(importId, ct);
    }
    catch (SkailGatewayException e)
    {
        store.Remove(importId);
        return Results.Json(new { error = e.Message }, statusCode: StatusCodes.Status502BadGateway);
    }
    return Results.Ok(ImportView.From(store.Get(importId) ?? state));
});

app.MapGet("/api/imports/{id}", (string id, ImportStore store) =>
    store.Get(id) is { } state ? Results.Ok(ImportView.From(state)) : Results.NotFound());

// Called by the ImportPage command: here the web app plays the target catalog.
app.MapPost("/internal/imports/{id}/pages", (string id, PageRecord record, ImportStore store) =>
    store.SavePage(id, record) == SaveOutcome.Rejected
        ? Results.Json(new { error = $"simulated failure on page {SimulatedFailure.Page}" }, statusCode: StatusCodes.Status503ServiceUnavailable)
        : Results.NoContent());

// Called by the ReportStatus command.
app.MapPost("/internal/imports/{id}/events", (string id, ImportEvent evt, ImportStore store) =>
{
    store.Apply(evt with { ImportId = id });
    return Results.NoContent();
});

app.MapGet("/api/imports/{id}/stream", async (string id, HttpContext http, ImportStore store, CancellationToken ct) =>
{
    using var subscription = store.Subscribe(id); // subscribe before reading the snapshot, so no update is lost
    if (store.Get(id) is not { } current) return Results.NotFound();

    http.Response.ContentType = "text/event-stream";
    http.Response.Headers.CacheControl = "no-cache";

    async Task Send(ImportState state)
    {
        var json = JsonSerializer.Serialize(ImportView.From(state), Json.Options);
        await http.Response.WriteAsync($"data: {json}\n\n", ct);
        await http.Response.Body.FlushAsync(ct);
    }

    try
    {
        await Send(current);
        if (current.IsTerminal) return Results.Empty;

        await foreach (var state in subscription.Reader.ReadAllAsync(ct))
        {
            await Send(state);
            if (state.IsTerminal) break;
        }
    }
    catch (OperationCanceledException)
    {
        // client disconnected
    }
    return Results.Empty;
});

app.Run();

public sealed record StartRequest(bool SimulateFailure);

/// <summary>What <c>GET /api/imports/{id}</c> and the SSE stream return.</summary>
public sealed record ImportView(
    string ImportId,
    ImportStage Stage,
    bool SimulateFailure,
    int Imported,
    int ExpectedProducts,
    int ExpectedPages,
    IReadOnlyList<PageSummary> Pages,
    string? Detail,
    UiState Ui)
{
    public static ImportView From(ImportState s) =>
        new(s.ImportId, s.Stage, s.SimulateFailure, s.Imported, CatalogLimits.ExpectedProducts, CatalogLimits.ExpectedPages,
            s.Pages, s.Detail, ButtonRules.For(s));
}

public partial class Program;
