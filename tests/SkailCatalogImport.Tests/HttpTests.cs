using System.Net;
using System.Text.Json;
using SkailCatalogImport.Contracts;
using SkailCatalogImport.Worker.Progress;
using SkailCatalogImport.Worker.Target;

namespace SkailCatalogImport.Tests;

public class HttpTests
{
    private static HttpClient Client(StubHandler handler) =>
        new(handler) { BaseAddress = new Uri("http://web.test:8080/") };

    [Fact]
    public async Task Target_posts_the_page_to_the_internal_pages_endpoint_of_the_import()
    {
        var handler = StubHandler.Status(HttpStatusCode.NoContent);
        var record = new PageRecord(3, [new Product(1, "A", null, 1m)], 402);

        await new HttpTargetCatalog(Client(handler)).SavePageAsync("import 7", record);

        var (request, body) = Assert.Single(handler.Calls);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("http://web.test:8080/internal/imports/import%207/pages", request.RequestUri!.AbsoluteUri);
        Assert.Equal(3, JsonSerializer.Deserialize<PageRecord>(body, Json.Options)!.Page);
    }

    [Fact]
    public async Task Target_throws_with_the_status_and_the_reason_so_the_command_is_retried()
    {
        var handler = StubHandler.Json("{\"error\":\"simulated failure on page 3\"}", HttpStatusCode.ServiceUnavailable);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            new HttpTargetCatalog(Client(handler)).SavePageAsync("i1", new PageRecord(3, [], 1)));

        Assert.Equal("Target catalog answered 503 to the write of page 3: simulated failure on page 3", ex.Message);
    }

    [Fact]
    public async Task Notifier_posts_the_event_to_the_internal_events_endpoint_of_the_import()
    {
        var handler = StubHandler.Status(HttpStatusCode.NoContent);
        var evt = new ImportEvent("import-7", ImportStage.Started, null);

        await new HttpProgressNotifier(Client(handler)).NotifyAsync(evt);

        var (request, body) = Assert.Single(handler.Calls);
        Assert.Equal("http://web.test:8080/internal/imports/import-7/events", request.RequestUri!.ToString());
        Assert.Equal(evt, JsonSerializer.Deserialize<ImportEvent>(body, Json.Options));
    }

    [Fact]
    public async Task Notifier_throws_on_failure_so_the_command_is_retried()
    {
        var notifier = new HttpProgressNotifier(Client(StubHandler.Status(HttpStatusCode.InternalServerError)));

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            notifier.NotifyAsync(new ImportEvent("i1", ImportStage.Completed, "194 products in 10 pages")));
        Assert.Equal("Web app answered 500 to the progress event", ex.Message);
    }
}
