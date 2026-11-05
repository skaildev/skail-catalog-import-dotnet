using Skail.Platform.Runtime;
using Skail.Platform.Runtime.Threading;
using SkailCatalogImport.Contracts;

namespace SkailCatalogImport.Worker;

/// <summary>
/// The I/O of the function, one operation per command. A retry runs the whole command again, so every command is
/// idempotent. The commands delegate to <see cref="CatalogOperations"/>, so their logic is tested without skail.
/// </summary>
public class CatalogCommands(CatalogOperations operations)
{
    [SkailCommand(retryCount: 3)]
    public async SkailTask<ImportedPage> ImportPage(string importId, int page)
        => await operations.ImportPageAsync(importId, page);

    /// <summary>Idempotent: the web app ignores a stage it already has.</summary>
    [SkailCommand(retryCount: 3)]
    public async SkailTask ReportStatus(string importId, ImportStage stage, string? detail)
        => await operations.ReportStatusAsync(importId, stage, detail);
}
