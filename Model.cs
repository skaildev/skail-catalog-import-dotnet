namespace SkailCatalogImport;

public static class CatalogLimits
{
    public const int PageSize = 20;

    /// <summary>Wait between pages, the rate limit of the source API.</summary>
    public const int SecondsBetweenPages = 2;

    /// <summary>Safety cap in case the source never reports the end. It also bounds the run history.</summary>
    public const int MaxPages = 50;
}

public sealed record Product(int Id, string Title, string? Category, decimal Price);

/// <summary>One page as the source API serves it. Pages start at 1.</summary>
public sealed record SourcePage(IReadOnlyList<Product> Products, bool HasMore);

/// <summary>What ImportPage returns to the function. It goes into the run history, so it has no products.</summary>
public sealed record ImportedPage(int Imported, bool HasMore);

public sealed record ImportResult(string ImportId, int Pages, int Products);
