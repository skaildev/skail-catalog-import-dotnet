using System.Diagnostics;
using Microsoft.Extensions.Logging;
using SkailCatalogImport.Contracts;
using SkailCatalogImport.Worker.Progress;
using SkailCatalogImport.Worker.Source;
using SkailCatalogImport.Worker.Target;

namespace SkailCatalogImport.Worker;

/// <summary>
/// The I/O behind the commands, as plain tasks. Commands only run inside the skail runtime, so keeping the
/// logic here lets it be unit tested without skail.
/// </summary>
public class CatalogOperations(
    ISourceCatalog source,
    ITargetCatalog target,
    IProgressNotifier notifier,
    ILogger<CatalogOperations> log)
{
    /// <summary>
    /// Reads the page from the source and writes it to the target. A retry runs both again: the read changes nothing
    /// and the write replaces the page, so nothing is duplicated. Only the count goes back to the run history.
    /// </summary>
    public async Task<ImportedPage> ImportPageAsync(string importId, int page, CancellationToken ct = default)
    {
        var clock = Stopwatch.StartNew();
        var read = await source.GetPageAsync(page, ct);
        await target.SavePageAsync(importId, new PageRecord(page, read.Products, (int)clock.ElapsedMilliseconds), ct);

        log.LogInformation("Page {Page}: {Count} products written in {Ms} ms", page, read.Products.Count, clock.ElapsedMilliseconds);
        return new ImportedPage(read.Products.Count, read.HasMore);
    }

    /// <summary>Idempotent: the web app ignores a stage it already has.</summary>
    public Task ReportStatusAsync(string importId, ImportStage stage, string? detail, CancellationToken ct = default) =>
        notifier.NotifyAsync(new ImportEvent(importId, stage, detail), ct);
}
