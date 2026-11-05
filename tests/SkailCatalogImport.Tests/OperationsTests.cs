using Microsoft.Extensions.Logging.Abstractions;
using SkailCatalogImport.Contracts;
using SkailCatalogImport.Worker;
using SkailCatalogImport.Worker.Progress;
using SkailCatalogImport.Worker.Source;
using SkailCatalogImport.Worker.Target;

namespace SkailCatalogImport.Tests;

public class OperationsTests
{
    private sealed class RecordingTarget(int rejections = 0) : ITargetCatalog
    {
        private int _rejections = rejections;
        public List<(string ImportId, PageRecord Record)> Saved { get; } = [];

        public Task SavePageAsync(string importId, PageRecord record, CancellationToken ct = default)
        {
            if (_rejections-- > 0) throw new HttpRequestException("Target catalog answered 503");
            Saved.Add((importId, record));
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingNotifier : IProgressNotifier
    {
        public List<ImportEvent> Events { get; } = [];

        public Task NotifyAsync(ImportEvent evt, CancellationToken ct = default)
        {
            Events.Add(evt);
            return Task.CompletedTask;
        }
    }

    private static CatalogOperations Operations(ITargetCatalog target, IProgressNotifier? notifier = null) =>
        new(new FakeSourceCatalog(), target, notifier ?? new RecordingNotifier(), NullLogger<CatalogOperations>.Instance);

    [Fact]
    public async Task Import_page_reads_the_page_writes_it_and_returns_only_the_count()
    {
        var target = new RecordingTarget();

        var result = await Operations(target).ImportPageAsync("i1", 2);

        Assert.Equal(new ImportedPage(20, true), result);
        var (importId, record) = Assert.Single(target.Saved);
        Assert.Equal("i1", importId);
        Assert.Equal(2, record.Page);
        Assert.Equal(Enumerable.Range(21, 20), record.Products.Select(p => p.Id));
        Assert.True(record.DurationMs >= 0);
    }

    [Fact]
    public async Task The_last_page_has_14_products_and_no_more()
    {
        Assert.Equal(new ImportedPage(14, false), await Operations(new RecordingTarget()).ImportPageAsync("i1", 10));
    }

    [Fact]
    public async Task A_rejected_write_throws_so_skail_retries_the_whole_command()
    {
        var target = new RecordingTarget(rejections: 1);
        var operations = Operations(target);

        await Assert.ThrowsAsync<HttpRequestException>(() => operations.ImportPageAsync("i1", 3));
        Assert.Empty(target.Saved);

        await operations.ImportPageAsync("i1", 3);
        Assert.Equal(3, Assert.Single(target.Saved).Record.Page);
    }

    [Fact]
    public async Task Report_status_sends_the_stage_and_the_detail()
    {
        var notifier = new RecordingNotifier();

        await Operations(new RecordingTarget(), notifier).ReportStatusAsync("i1", ImportStage.Completed, "194 products in 10 pages");

        Assert.Equal(new ImportEvent("i1", ImportStage.Completed, "194 products in 10 pages"), Assert.Single(notifier.Events));
    }
}
