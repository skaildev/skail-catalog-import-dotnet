using System.Text.Json;
using SkailCatalogImport.Contracts;

namespace SkailCatalogImport.Worker.Source;

/// <summary>DummyJSON: GET {base}/products?limit=20&amp;skip={(page - 1) * 20}&amp;select=id,title,category,price. No key.</summary>
public sealed class HttpSourceCatalog(HttpClient http) : ISourceCatalog
{
    public async Task<SourcePage> GetPageAsync(int page, CancellationToken ct = default)
    {
        var skip = (page - 1) * CatalogLimits.PageSize;
        using var response = await http.GetAsync($"products?limit={CatalogLimits.PageSize}&skip={skip}&select=id,title,category,price", ct);
        var content = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"Source API answered {(int)response.StatusCode}: {content[..Math.Min(content.Length, 300)]}",
                inner: null,
                response.StatusCode);

        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;
        var products = root.GetProperty("products").EnumerateArray()
            .Select(p => new Product(
                p.GetProperty("id").GetInt32(),
                p.GetProperty("title").GetString() ?? "",
                p.TryGetProperty("category", out var c) ? c.GetString() : null,
                p.GetProperty("price").GetDecimal()))
            .ToList();

        var total = root.GetProperty("total").GetInt32();
        return new SourcePage(products, HasMore: skip + products.Count < total);
    }
}
