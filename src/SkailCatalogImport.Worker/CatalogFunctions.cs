using Skail.Platform.Runtime;
using Skail.Platform.Runtime.Threading;
using SkailCatalogImport.Contracts;

namespace SkailCatalogImport.Worker;

public class CatalogFunctions(CatalogCommands commands)
{
    /// <summary>
    /// Imports the catalog page by page, waiting 2 seconds between pages (the rate limit of the source API). After a
    /// crash the run continues at the page where it stopped, and the pages already imported are not read again.
    /// </summary>
    [SkailFunction]
    public async SkailTask<ImportResult> ImportCatalog(string importId)
    {
        var page = 1;
        var products = 0;
        try
        {
            await commands.ReportStatus(importId, ImportStage.Started, null);

            while (true)
            {
                // One command per page: when the source or the target fails, skail retries only this page.
                var result = await commands.ImportPage(importId, page);
                products += result.Imported;
                if (!result.HasMore || result.Imported == 0 || page == CatalogLimits.MaxPages) break;

                page++;
                await SkailTask.DelaySeconds(CatalogLimits.SecondsBetweenPages);
            }

            await commands.ReportStatus(importId, ImportStage.Completed, ImportText.Completed(products, page));
            return new ImportResult(importId, page, products);
        }
        catch (Exception ex) when (ex is not SkailNonDeterministicException)
        {
            // The pages already written stay written.
            await commands.ReportStatus(importId, ImportStage.Failed, $"Page {page}: {ex.Message}");
            throw;
        }
    }
}
