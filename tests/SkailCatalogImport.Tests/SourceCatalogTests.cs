using System.Net;
using SkailCatalogImport.Contracts;
using SkailCatalogImport.Worker.Source;

namespace SkailCatalogImport.Tests;

public class SourceCatalogTests
{
    private static string DummyJson(int count, int skip, int total) =>
        "{\"products\":[" + string.Join(",", Enumerable.Range(skip + 1, count).Select(i =>
            $"{{\"id\":{i},\"title\":\"P{i}\",\"category\":\"c\",\"price\":{i}.5}}")) +
        $"],\"total\":{total},\"skip\":{skip},\"limit\":20}}";

    private static HttpSourceCatalog Http(StubHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://api.example.test/") });

    [Theory]
    [InlineData(1, 0)]
    [InlineData(4, 60)]
    [InlineData(10, 180)]
    public async Task Http_source_requests_limit_20_and_skip_from_the_page_starting_at_1(int page, int skip)
    {
        var handler = StubHandler.Json(DummyJson(1, skip, 194));

        await Http(handler).GetPageAsync(page);

        Assert.Equal($"https://api.example.test/products?limit=20&skip={skip}&select=id,title,category,price",
            Assert.Single(handler.Calls).Request.RequestUri!.ToString());
    }

    [Fact]
    public async Task Http_source_parses_products_and_reports_more_pages()
    {
        var page = await Http(StubHandler.Json(DummyJson(20, 0, 194))).GetPageAsync(1);

        Assert.Equal(20, page.Products.Count);
        Assert.Equal(new Product(1, "P1", "c", 1.5m), page.Products[0]);
        Assert.True(page.HasMore);
    }

    [Fact]
    public async Task Http_source_last_page_has_no_more()
    {
        var page = await Http(StubHandler.Json(DummyJson(14, 180, 194))).GetPageAsync(10);

        Assert.Equal(14, page.Products.Count);
        Assert.False(page.HasMore);
    }

    [Fact]
    public async Task Http_source_fails_with_status_and_body_so_the_command_retries()
    {
        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            Http(StubHandler.Json("rate limited", HttpStatusCode.TooManyRequests)).GetPageAsync(1));

        Assert.Contains("429", ex.Message);
        Assert.Contains("rate limited", ex.Message);
    }

    [Fact]
    public async Task Fake_source_serves_194_products_in_10_pages()
    {
        var source = new FakeSourceCatalog();
        var counts = new List<int>();
        for (var n = 1; ; n++)
        {
            var page = await source.GetPageAsync(n);
            counts.Add(page.Products.Count);
            if (!page.HasMore) break;
        }

        Assert.Equal(10, counts.Count);
        Assert.Equal(194, counts.Sum());
        Assert.Equal(14, counts[^1]);
    }

    [Fact]
    public async Task Fake_source_is_deterministic_and_has_unique_ids()
    {
        var a = new FakeSourceCatalog();
        var all = new List<Product>();
        for (var n = 1; n <= 10; n++) all.AddRange((await a.GetPageAsync(n)).Products);

        Assert.Equal(194, all.Select(p => p.Id).Distinct().Count());
        Assert.Equal((await a.GetPageAsync(4)).Products, (await new FakeSourceCatalog().GetPageAsync(4)).Products);
    }

    [Fact]
    public async Task Fake_source_beyond_the_end_is_empty()
    {
        var page = await new FakeSourceCatalog().GetPageAsync(11);

        Assert.Empty(page.Products);
        Assert.False(page.HasMore);
    }
}
