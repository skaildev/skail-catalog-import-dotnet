using SkailCatalogImport.Contracts;

namespace SkailCatalogImport.Worker.Source;

/// <summary>Offline source (SOURCE_PROVIDER=fake) with the 194 products of the demo.</summary>
public sealed class FakeSourceCatalog : ISourceCatalog
{
    public Task<SourcePage> GetPageAsync(int page, CancellationToken ct = default)
    {
        var skip = (page - 1) * CatalogLimits.PageSize;
        var count = Math.Max(0, Math.Min(CatalogLimits.PageSize, CatalogLimits.ExpectedProducts - skip));
        var products = Enumerable.Range(skip + 1, count)
            .Select(id => new Product(id, $"Product {id}", $"category-{id % 5}", 5m + id % 50))
            .ToList();
        return Task.FromResult(new SourcePage(products, HasMore: skip + count < CatalogLimits.ExpectedProducts));
    }
}
