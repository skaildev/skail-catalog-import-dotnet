using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SkailCatalogImport.Contracts;
using SkailCatalogImport.Web.Skail;

namespace SkailCatalogImport.Tests;

public sealed class FakeGateway : ISkailGateway
{
    public List<string> Triggers { get; } = [];
    public Exception? FailWith { get; set; }

    public Task TriggerAsync(string importId, CancellationToken ct = default)
    {
        if (FailWith is not null) throw FailWith;
        Triggers.Add(importId);
        return Task.CompletedTask;
    }
}

public sealed class WebFactory : WebApplicationFactory<Program>
{
    public FakeGateway Gateway { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(s =>
        {
            s.RemoveAll<ISkailGateway>();
            s.AddSingleton<ISkailGateway>(Gateway);
        });
    }
}

public class WebApiTests(WebFactory factory) : IClassFixture<WebFactory>
{
    private readonly HttpClient _http = factory.CreateClient();

    private async Task<JsonElement> StartAsync(bool simulateFailure = false)
    {
        var res = await _http.PostAsJsonAsync("/api/imports", new { simulateFailure });
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<string> StartIdAsync(bool simulateFailure = false) =>
        (await StartAsync(simulateFailure)).GetProperty("importId").GetString()!;

    private Task<HttpResponseMessage> SavePage(string id, int page, int products = 20) =>
        _http.PostAsJsonAsync($"/internal/imports/{id}/pages",
            new PageRecord(page, Enumerable.Range((page - 1) * 20, products).Select(i => new Product(i + 1, $"P{i}", "c", i)).ToList(), 400),
            Json.Options);

    private Task<HttpResponseMessage> Report(string id, ImportStage stage, string? detail = null) =>
        _http.PostAsJsonAsync($"/internal/imports/{id}/events", new ImportEvent("ignored", stage, detail), Json.Options);

    private async Task<JsonElement> GetImport(string id) => await _http.GetFromJsonAsync<JsonElement>($"/api/imports/{id}");

    [Fact]
    public async Task Health_endpoint_is_ok()
    {
        Assert.Equal(HttpStatusCode.OK, (await _http.GetAsync("/healthz")).StatusCode);
    }

    [Fact]
    public async Task Index_page_is_served_with_the_button_and_the_failure_checkbox()
    {
        var html = await _http.GetStringAsync("/");

        Assert.Contains("id=\"import\"", html);
        Assert.Contains("Import catalog", html);
        Assert.Contains("Make the target catalog reject page 3 twice", html);
    }

    [Fact]
    public async Task Start_triggers_the_function_with_the_new_import_id()
    {
        var view = await StartAsync();

        var importId = view.GetProperty("importId").GetString()!;
        Assert.Equal("Starting", view.GetProperty("stage").GetString());
        Assert.False(view.GetProperty("simulateFailure").GetBoolean());
        Assert.Equal(194, view.GetProperty("expectedProducts").GetInt32());
        Assert.Equal(10, view.GetProperty("expectedPages").GetInt32());
        Assert.Equal("Importing…", view.GetProperty("ui").GetProperty("label").GetString());
        Assert.False(view.GetProperty("ui").GetProperty("enabled").GetBoolean());
        Assert.Contains(importId, factory.Gateway.Triggers);
    }

    [Fact]
    public async Task Start_without_a_body_does_not_simulate_the_failure()
    {
        var res = await _http.PostAsync("/api/imports", null);

        res.EnsureSuccessStatusCode();
        Assert.False((await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("simulateFailure").GetBoolean());
    }

    [Fact]
    public async Task Start_failure_is_a_bad_gateway_and_forgets_the_import()
    {
        factory.Gateway.FailWith = new SkailGatewayException("skail answered 401");
        try
        {
            var res = await _http.PostAsJsonAsync("/api/imports", new { simulateFailure = false });

            Assert.Equal(HttpStatusCode.BadGateway, res.StatusCode);
            Assert.Contains("skail answered 401", await res.Content.ReadAsStringAsync());
        }
        finally { factory.Gateway.FailWith = null; }
    }

    [Fact]
    public async Task Unknown_import_is_not_found()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _http.GetAsync("/api/imports/nope")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _http.GetAsync("/api/imports/nope/stream")).StatusCode);
    }

    [Fact]
    public async Task Internal_pages_update_the_import_and_count_the_attempts()
    {
        var id = await StartIdAsync();

        Assert.Equal(HttpStatusCode.NoContent, (await SavePage(id, 1)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SavePage(id, 2)).StatusCode);
        await SavePage(id, 2); // a retried command writes the same page again

        var view = await GetImport(id);
        Assert.Equal(40, view.GetProperty("imported").GetInt32());
        Assert.Equal(2, view.GetProperty("pages").GetArrayLength());
        Assert.Equal(2, view.GetProperty("pages")[1].GetProperty("attempts").GetInt32());
        Assert.Equal("Started", view.GetProperty("stage").GetString());
    }

    [Fact]
    public async Task With_the_simulated_failure_page_3_answers_503_twice_then_204()
    {
        var id = await StartIdAsync(simulateFailure: true);

        var first = await SavePage(id, 3);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, first.StatusCode);
        Assert.Equal("simulated failure on page 3", (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString());
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await SavePage(id, 3)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SavePage(id, 3)).StatusCode);

        var page = (await GetImport(id)).GetProperty("pages")[0];
        Assert.Equal(3, page.GetProperty("attempts").GetInt32());
        Assert.Equal(20, page.GetProperty("products").GetInt32());
    }

    [Fact]
    public async Task Internal_completed_event_ends_the_import_and_frees_the_button()
    {
        var id = await StartIdAsync();
        await SavePage(id, 10, 14);

        await Report(id, ImportStage.Completed, "194 products in 10 pages");

        var view = await GetImport(id);
        Assert.Equal("Completed", view.GetProperty("stage").GetString());
        Assert.Equal("194 products in 10 pages", view.GetProperty("detail").GetString());
        Assert.True(view.GetProperty("ui").GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public async Task Internal_failed_event_exposes_the_detail()
    {
        var id = await StartIdAsync();

        await Report(id, ImportStage.Failed, "Page 3: Source API answered 503");

        var view = await GetImport(id);
        Assert.Equal("Failed", view.GetProperty("stage").GetString());
        Assert.Equal("Page 3: Source API answered 503", view.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Internal_calls_for_an_unknown_import_are_accepted_and_ignored()
    {
        Assert.Equal(HttpStatusCode.NoContent, (await SavePage("ghost", 1)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Report("ghost", ImportStage.Started)).StatusCode);
    }

    [Fact]
    public async Task Stream_sends_the_current_state_then_each_update_and_closes_when_finished()
    {
        var id = await StartIdAsync();
        await SavePage(id, 1);

        using var res = await _http.GetAsync($"/api/imports/{id}/stream", HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal("text/event-stream", res.Content.Headers.ContentType!.MediaType);
        using var reader = new StreamReader(await res.Content.ReadAsStreamAsync());

        async Task<JsonElement> Next()
        {
            string? line;
            while ((line = await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5))) is not null)
                if (line.StartsWith("data: ")) return JsonDocument.Parse(line[6..]).RootElement.Clone();
            throw new InvalidOperationException("stream closed");
        }

        Assert.Equal(20, (await Next()).GetProperty("imported").GetInt32());
        await SavePage(id, 2);
        Assert.Equal(40, (await Next()).GetProperty("imported").GetInt32());
        await Report(id, ImportStage.Completed, "40 products in 2 pages");
        Assert.Equal("Completed", (await Next()).GetProperty("stage").GetString());
        await reader.ReadToEndAsync().WaitAsync(TimeSpan.FromSeconds(5)); // the server closes the stream when the import ends
    }
}
