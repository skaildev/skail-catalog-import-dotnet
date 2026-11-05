using System.Net.Http.Json;
using SkailCatalogImport.Contracts;

namespace SkailCatalogImport.Worker.Progress;

/// <summary>Reports progress to the web app: POST {web}/internal/imports/{id}/events.</summary>
public sealed class HttpProgressNotifier(HttpClient http) : IProgressNotifier
{
    public async Task NotifyAsync(ImportEvent evt, CancellationToken ct = default)
    {
        using var response = await http.PostAsJsonAsync($"internal/imports/{Uri.EscapeDataString(evt.ImportId)}/events", evt, Json.Options, ct);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"Web app answered {(int)response.StatusCode} to the progress event", inner: null, response.StatusCode);
    }
}
