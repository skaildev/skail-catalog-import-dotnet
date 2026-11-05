using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Skail.Platform.Runtime;
using Skail.Platform.Runtime.Hosting;
using SkailCatalogImport.Worker;
using SkailCatalogImport.Worker.Progress;
using SkailCatalogImport.Worker.Source;
using SkailCatalogImport.Worker.Target;

[assembly: VisibleToSkailPlatform]

var builder = Host.CreateApplicationBuilder(args);

// HttpClient request logs would show up as extra lines under each command in the skail Monitor
builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);

var webBaseUrl = Environment.GetEnvironmentVariable("WEB_BASE_URL") ?? "http://localhost:8080";
var sourceProvider = Environment.GetEnvironmentVariable("SOURCE_PROVIDER") ?? "http";
var sourceApiUrl = Environment.GetEnvironmentVariable("SOURCE_API_URL") ?? "https://dummyjson.com";

static Uri WithSlash(string url) => new(url.EndsWith('/') ? url : url + "/");

if (sourceProvider.Equals("fake", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<ISourceCatalog, FakeSourceCatalog>();
}
else
{
    builder.Services.AddHttpClient<ISourceCatalog, HttpSourceCatalog>(c =>
    {
        c.BaseAddress = WithSlash(sourceApiUrl);
        c.Timeout = TimeSpan.FromSeconds(15);
    });
}

builder.Services.AddHttpClient<ITargetCatalog, HttpTargetCatalog>(c =>
{
    c.BaseAddress = WithSlash(webBaseUrl);
    c.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddHttpClient<IProgressNotifier, HttpProgressNotifier>(c =>
{
    c.BaseAddress = WithSlash(webBaseUrl);
    c.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddSingleton<CatalogOperations>();

// reads SKAIL_WORKLOAD, SKAIL_NAMESPACE, SKAIL_KEY and SKAIL_SIDECAR from the environment (.env)
builder.UseSkail();

await builder.Build().RunAsync();
