namespace SkailCatalogImport.Contracts;

public static class CatalogLimits
{
    public const int PageSize = 20;

    /// <summary>Wait between pages, the rate limit of the source API.</summary>
    public const int SecondsBetweenPages = 2;

    /// <summary>Safety cap in case the source never reports the end. It also bounds the run history.</summary>
    public const int MaxPages = 50;

    /// <summary>Size of the demo catalog (DummyJSON has 194 products), used only by the page.</summary>
    public const int ExpectedProducts = 194;

    public const int ExpectedPages = (ExpectedProducts + PageSize - 1) / PageSize;
}

/// <summary>The checkbox on the page: the target catalog rejects the first writes of this page.</summary>
public static class SimulatedFailure
{
    public const int Page = 3;
    public const int Times = 2;
}

public sealed record Product(int Id, string Title, string? Category, decimal Price);

/// <summary>One page as served by the source API. Pages start at 1.</summary>
public sealed record SourcePage(IReadOnlyList<Product> Products, bool HasMore);

/// <summary>What ImportPage returns to the function. It goes into the run history, so it has no products.</summary>
public sealed record ImportedPage(int Imported, bool HasMore);

/// <summary>A page written to the target catalog. Writing the same import and page again replaces it.</summary>
public sealed record PageRecord(int Page, IReadOnlyList<Product> Products, int DurationMs);

public enum ImportStage
{
    Starting,
    Started,
    Completed,
    Failed,
}

public sealed record ImportResult(string ImportId, int Pages, int Products);

/// <summary>What ReportStatus sends to the web app.</summary>
public sealed record ImportEvent(string ImportId, ImportStage Stage, string? Detail);

public static class ImportText
{
    public static string Completed(int products, int pages) =>
        $"{products} products in {pages} {(pages == 1 ? "page" : "pages")}";
}
