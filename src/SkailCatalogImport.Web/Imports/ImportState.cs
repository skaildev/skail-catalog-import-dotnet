using SkailCatalogImport.Contracts;

namespace SkailCatalogImport.Web.Imports;

/// <summary>One row of the page table. <c>Products</c> and <c>DurationMs</c> are null until a write succeeds.</summary>
/// <param name="Attempts">Writes the target catalog received for the page, failed ones included.</param>
public sealed record PageSummary(int Page, int? Products, int? DurationMs, int Attempts);

public sealed record ImportState(
    string ImportId,
    ImportStage Stage,
    bool SimulateFailure,
    IReadOnlyList<PageSummary> Pages,
    string? Detail)
{
    public bool IsTerminal => Stage is ImportStage.Completed or ImportStage.Failed;

    public int Imported => Pages.Sum(p => p.Products ?? 0);
}
