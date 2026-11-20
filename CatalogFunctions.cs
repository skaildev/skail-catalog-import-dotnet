using Skail.Platform.Runtime;
using Skail.Platform.Runtime.Threading;
using SkailCatalogImport.Demo;

namespace SkailCatalogImport;

public class CatalogFunctions(DummyJsonSource source, ImportDesk desk)
{
    /// <summary>
    /// Imports the catalog page by page, waiting 2 seconds between pages (the rate limit of the source API). After an
    /// app restart the run goes on at the page where it stopped, and the pages already imported are not read again.
    /// The name and the parameter are the trigger contract: ["{importId}"].
    /// </summary>
    [SkailFunction]
    public async SkailTask<ImportResult> ImportCatalog(string importId)
    {
        var page = 1;
        var products = 0;
        while (true)
        {
            // one command per page: when the source or the target fails, skail retries only this page
            var result = await ImportPage(importId, page);
            products += result.Imported;
            if (!result.HasMore || result.Imported == 0 || page == CatalogLimits.MaxPages) break;

            page++;
            await SkailTask.DelaySeconds(CatalogLimits.SecondsBetweenPages); // durable: holds no thread or process
        }

        var import = new ImportResult(importId, page, products);
        await ReportResult(import);
        return import;
    }

    /// <summary>
    /// Reads the page from the source and writes it to the target catalog. A retry runs both again: the read changes
    /// nothing and the write replaces the page, so nothing is duplicated. Only the count goes to the run history.
    /// </summary>
    [SkailCommand(retryCount: 3)]
    public async SkailTask<ImportedPage> ImportPage(string importId, int page)
    {
        var read = await source.GetPageAsync(page);
        desk.SavePage(importId, page, read.Products);
        return new ImportedPage(read.Products.Count, read.HasMore);
    }

    [SkailCommand]
    public async SkailTask ReportResult(ImportResult result)
    {
        desk.Completed(result);
        await Task.CompletedTask;
    }
}
