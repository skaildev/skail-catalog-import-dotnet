using SkailCatalogImport.Skail;

namespace SkailCatalogImport.Demo;

/// <summary>The target catalog did not accept the write: the command throws, and skail retries the page.</summary>
public sealed class TargetRejectedException(int page)
    : Exception($"the target catalog rejected the write of page {page} (503)");

/// <summary>
/// Starts the imports for the page and plays the target catalog: it keeps the products of each page in memory, and
/// writing the same page again replaces it. With the checkbox on, it rejects the first two writes of page 3. An import
/// the app does not know (it was started before the app restarted) shows up with its next page.
/// </summary>
public sealed class ImportDesk(SkailApiClient skail)
{
    private const int RejectedPage = 3;
    private const int RejectedTimes = 2;

    private sealed class PageRow
    {
        public int Attempts { get; set; }
        public bool Rejected { get; set; }
        public IReadOnlyList<Product> Products { get; set; } = [];
    }

    private sealed class Import(string id, bool rejectPage3)
    {
        public string Id { get; } = id;
        public bool RejectPage3 { get; } = rejectPage3;
        public SortedDictionary<int, PageRow> Pages { get; } = [];
        public ImportResult? Result { get; set; }
    }

    private readonly object gate = new();
    private readonly Dictionary<string, Import> imports = [];
    private Import? current;

    /// <summary>Starts an import, unless one is still running.</summary>
    public async Task<bool> StartAsync(bool rejectPage3, CancellationToken ct)
    {
        lock (gate) if (current is { Result: null }) return false;

        var importId = Guid.NewGuid().ToString();
        lock (gate) current = imports[importId] = new Import(importId, rejectPage3);
        try
        {
            await skail.TriggerAsync(importId, ct);
        }
        catch
        {
            lock (gate) { imports.Remove(importId); current = null; } // the run did not start: the button works again
            throw;
        }
        Print(importId, rejectPage3 ? "import started; the target will reject page 3 twice" : "import started");
        return true;
    }

    /// <summary>The target catalog: the import id and the page number are the key, so writing again replaces the page.</summary>
    public void SavePage(string importId, int page, IReadOnlyList<Product> products)
    {
        lock (gate)
        {
            var import = Find(importId);
            if (!import.Pages.TryGetValue(page, out var row)) import.Pages[page] = row = new PageRow();
            row.Attempts++;
            row.Rejected = import.RejectPage3 && page == RejectedPage && row.Attempts <= RejectedTimes;
            if (row.Rejected)
            {
                Print(importId, $"page {page}: rejected by the target (attempt {row.Attempts}), skail will try again");
                throw new TargetRejectedException(page);
            }
            row.Products = products;
        }
        Print(importId, $"page {page}: {products.Count} products written");
    }

    public void Completed(ImportResult result)
    {
        lock (gate) Find(result.ImportId).Result = result;
        Print(result.ImportId, $"import completed: {result.Products} products in {result.Pages} pages");
    }

    /// <summary>What the page shows: the latest import.</summary>
    public object? View()
    {
        lock (gate)
        {
            if (current is not { } i) return null;
            return new
            {
                i.Id, i.RejectPage3, i.Result,
                products = i.Pages.Values.Sum(p => p.Products.Count),
                pages = i.Pages.Select(p => new
                {
                    page = p.Key,
                    p.Value.Attempts,
                    products = p.Value.Products.Count,
                    status = p.Value.Rejected ? "rejected" : p.Value.Products.Count > 0 ? "written" : "writing",
                    sample = p.Value.Products.Take(3).Select(x => x.Title),
                }).Reverse(),
            };
        }
    }

    // an import started before the app restarted: the page follows it from its next page
    private Import Find(string importId)
    {
        if (!imports.TryGetValue(importId, out var import))
            current = imports[importId] = import = new Import(importId, rejectPage3: false);
        return import;
    }

    private static void Print(string importId, string message) => Console.WriteLine($"[{importId[..8]}] {message}");
}
