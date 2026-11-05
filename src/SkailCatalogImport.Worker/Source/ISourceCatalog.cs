using SkailCatalogImport.Contracts;

namespace SkailCatalogImport.Worker.Source;

/// <summary>The catalog the products come from.</summary>
public interface ISourceCatalog
{
    /// <summary>Page <paramref name="page"/> (starting at 1) of <see cref="CatalogLimits.PageSize"/> products.</summary>
    Task<SourcePage> GetPageAsync(int page, CancellationToken ct = default);
}
