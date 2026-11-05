using SkailCatalogImport.Contracts;

namespace SkailCatalogImport.Worker.Target;

/// <summary>The catalog the products go to. Writing the same import and page again replaces the page.</summary>
public interface ITargetCatalog
{
    Task SavePageAsync(string importId, PageRecord record, CancellationToken ct = default);
}
