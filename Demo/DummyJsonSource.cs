using System.Text.Json;

namespace SkailCatalogImport.Demo;

/// <summary>
/// The catalog the products come from: DummyJSON, a free public API with 194 products and no key.
/// GET https://dummyjson.com/products?limit=20&amp;skip={(page - 1) * 20}&amp;select=id,title,category,price
/// </summary>
public sealed class DummyJsonSource(HttpClient http)
{
    public static readonly Uri BaseAddress = new("https://dummyjson.com/");

    public async Task<SourcePage> GetPageAsync(int page)
    {
        var skip = (page - 1) * CatalogLimits.PageSize;
        var query = $"products?limit={CatalogLimits.PageSize}&skip={skip}&select=id,title,category,price";
        using var response = await http.GetAsync(query);
        var content = await response.Content.ReadAsStringAsync();
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

        return new SourcePage(products, HasMore: skip + products.Count < root.GetProperty("total").GetInt32());
    }
}
