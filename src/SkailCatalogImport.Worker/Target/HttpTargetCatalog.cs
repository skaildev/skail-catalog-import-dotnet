using System.Net.Http.Json;
using System.Text.Json;
using SkailCatalogImport.Contracts;

namespace SkailCatalogImport.Worker.Target;

/// <summary>In this demo the web app plays the target catalog: POST {web}/internal/imports/{id}/pages.</summary>
public sealed class HttpTargetCatalog(HttpClient http) : ITargetCatalog
{
    public async Task SavePageAsync(string importId, PageRecord record, CancellationToken ct = default)
    {
        using var response = await http.PostAsJsonAsync($"internal/imports/{Uri.EscapeDataString(importId)}/pages", record, Json.Options, ct);
        if (response.IsSuccessStatusCode) return;

        var reason = await ReasonAsync(response, ct);
        throw new HttpRequestException(
            $"Target catalog answered {(int)response.StatusCode} to the write of page {record.Page}{(reason is null ? "" : $": {reason}")}",
            inner: null,
            response.StatusCode);
    }

    private static async Task<string?> ReasonAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            return doc.RootElement.TryGetProperty("error", out var error) ? error.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
