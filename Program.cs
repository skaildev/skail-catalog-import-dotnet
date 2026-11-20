using Skail.Platform.Runtime;
using Skail.Platform.Runtime.Hosting;
using SkailCatalogImport;
using SkailCatalogImport.Demo;
using SkailCatalogImport.Skail;

[assembly: VisibleToSkailPlatform]

// usage: dotnet run
//   then open http://localhost:8080
if (args.Any(a => !a.StartsWith('-')))
{
    Console.Error.WriteLine("This demo takes no arguments.");
    return 1;
}

DotEnv.Load();
string[] required = ["SKAIL_WORKLOAD", "SKAIL_NAMESPACE", "SKAIL_KEY", "SKAIL_SIDECAR"];
var missing = required.Where(name => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name))).ToArray();
if (missing.Length > 0)
{
    Console.Error.WriteLine($"Missing {string.Join(", ", missing)}. Copy .env.example to .env and fill it in.");
    return 1;
}

var builder = WebApplication.CreateBuilder(args);

// keep the console for the demo: only warnings and errors from the host and the libraries
builder.Logging.SetMinimumLevel(LogLevel.Warning);

// the page: localhost only, unless DEMO_LISTEN says otherwise (the container listens on all interfaces)
builder.WebHost.UseUrls(Environment.GetEnvironmentVariable("DEMO_LISTEN") ?? "http://localhost:8080");

builder.Services.AddHttpClient<DummyJsonSource>(http =>
{
    http.BaseAddress = DummyJsonSource.BaseAddress;
    http.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddSingleton<ImportDesk>();
builder.Services.AddSingleton(SkailApiOptions.FromEnvironment());
builder.Services.AddHttpClient<SkailApiClient>(http => http.Timeout = TimeSpan.FromSeconds(10));

// hosts the worker that executes CatalogFunctions; reads the SKAIL_* variables
builder.UseSkail();

var app = builder.Build();
var desk = app.Services.GetRequiredService<ImportDesk>();

var page = Path.Combine(AppContext.BaseDirectory, "Page", "index.html");
app.MapGet("/", () => Results.File(page, "text/html; charset=utf-8"));
// before the first one, the state is null: written as JSON, since an empty body is not JSON
app.MapGet("/api/state", () =>
    desk.View() is { } view ? Results.Json(view) : Results.Text("null", "application/json"));
app.MapPost("/api/imports", async (bool rejectPage3, CancellationToken ct) =>
    await desk.StartAsync(rejectPage3, ct) ? Results.Ok() : Results.Conflict());

app.Lifetime.ApplicationStarted.Register(() =>
    Console.WriteLine("Open http://localhost:8080 to import the catalog. Ctrl+C exits; an import in the middle goes on in skail."));

await app.RunAsync();
return 0;
