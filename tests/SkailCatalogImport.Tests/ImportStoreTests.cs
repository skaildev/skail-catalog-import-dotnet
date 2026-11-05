using SkailCatalogImport.Contracts;
using SkailCatalogImport.Web.Imports;

namespace SkailCatalogImport.Tests;

public class ImportStoreTests
{
    private static PageRecord Record(int page, int products = 20, int ms = 400) =>
        new(page, Enumerable.Range((page - 1) * 20, products).Select(i => new Product(i + 1, $"P{i}", "c", i)).ToList(), ms);

    private static ImportStore Store(bool simulateFailure = false)
    {
        var store = new ImportStore();
        store.Create("i1", simulateFailure);
        return store;
    }

    [Fact]
    public void A_new_import_is_starting_and_empty()
    {
        var state = Store().Get("i1")!;

        Assert.Equal(ImportStage.Starting, state.Stage);
        Assert.False(state.SimulateFailure);
        Assert.Empty(state.Pages);
        Assert.Equal(0, state.Imported);
        Assert.Null(new ImportStore().Get("nope"));
    }

    [Fact]
    public void Saving_pages_adds_products_and_moves_to_started()
    {
        var store = Store();

        Assert.Equal(SaveOutcome.Saved, store.SavePage("i1", Record(1)));
        store.SavePage("i1", Record(2));

        var state = store.Get("i1")!;
        Assert.Equal(ImportStage.Started, state.Stage);
        Assert.Equal([new PageSummary(1, 20, 400, 1), new PageSummary(2, 20, 400, 1)], state.Pages);
        Assert.Equal(40, state.Imported);
    }

    [Fact]
    public void The_same_page_twice_replaces_it_and_counts_two_attempts()
    {
        var store = Store();

        store.SavePage("i1", Record(1));
        store.SavePage("i1", Record(1, ms: 900));

        Assert.Equal(new PageSummary(1, 20, 900, 2), Assert.Single(store.Get("i1")!.Pages));
        Assert.Equal(20, store.Products("i1").Count);
    }

    [Fact]
    public void Pages_stay_ordered_and_the_products_have_no_duplicates()
    {
        var store = Store();
        foreach (var n in new[] { 4, 1, 3, 2, 10, 9, 8, 7, 6, 5 }) store.SavePage("i1", Record(n, n == 10 ? 14 : 20));
        store.SavePage("i1", Record(5));

        var state = store.Get("i1")!;
        Assert.Equal(Enumerable.Range(1, 10), state.Pages.Select(p => p.Page));
        Assert.Equal(194, store.Products("i1").Select(p => p.Id).Distinct().Count());
        Assert.Equal(194, state.Imported);
        Assert.Equal(2, state.Pages.Single(p => p.Page == 5).Attempts);
    }

    [Fact]
    public void With_the_simulated_failure_page_3_is_rejected_twice_then_saved()
    {
        var store = Store(simulateFailure: true);
        store.SavePage("i1", Record(1));
        store.SavePage("i1", Record(2));

        Assert.Equal(SaveOutcome.Rejected, store.SavePage("i1", Record(3)));
        Assert.Equal(new PageSummary(3, null, null, 1), store.Get("i1")!.Pages[^1]);
        Assert.Equal(SaveOutcome.Rejected, store.SavePage("i1", Record(3)));
        Assert.Equal(SaveOutcome.Saved, store.SavePage("i1", Record(3)));
        Assert.Equal(new PageSummary(3, 20, 400, 3), store.Get("i1")!.Pages[^1]);
        Assert.Equal(SaveOutcome.Saved, store.SavePage("i1", Record(4)));
    }

    [Fact]
    public void Without_the_simulated_failure_page_3_is_saved_at_once()
    {
        Assert.Equal(SaveOutcome.Saved, Store().SavePage("i1", Record(3)));
    }

    [Fact]
    public void Pages_of_an_unknown_import_are_ignored()
    {
        Assert.Equal(SaveOutcome.Unknown, new ImportStore().SavePage("ghost", Record(1)));
    }

    [Fact]
    public void Events_change_the_stage_once_and_completed_keeps_the_detail()
    {
        var store = Store();

        Assert.True(store.Apply(new ImportEvent("i1", ImportStage.Started, null)));
        Assert.False(store.Apply(new ImportEvent("i1", ImportStage.Started, null)));
        Assert.True(store.Apply(new ImportEvent("i1", ImportStage.Completed, "194 products in 10 pages")));

        var state = store.Get("i1")!;
        Assert.Equal(ImportStage.Completed, state.Stage);
        Assert.Equal("194 products in 10 pages", state.Detail);
    }

    [Fact]
    public void Failed_keeps_the_detail_and_the_saved_pages_and_ignores_later_events()
    {
        var store = Store();
        store.SavePage("i1", Record(1));

        store.Apply(new ImportEvent("i1", ImportStage.Failed, "Page 2: boom"));

        Assert.False(store.Apply(new ImportEvent("i1", ImportStage.Completed, null)));
        var state = store.Get("i1")!;
        Assert.Equal(ImportStage.Failed, state.Stage);
        Assert.Equal("Page 2: boom", state.Detail);
        Assert.Single(state.Pages);
    }

    [Fact]
    public async Task Subscribers_get_every_change_and_the_channel_completes_when_the_import_ends()
    {
        var store = Store();
        using var subscription = store.Subscribe("i1");

        store.SavePage("i1", Record(1));
        store.Apply(new ImportEvent("i1", ImportStage.Completed, null));

        var stages = new List<ImportStage>();
        await foreach (var state in subscription.Reader.ReadAllAsync().WithCancellation(new CancellationTokenSource(5000).Token))
            stages.Add(state.Stage);
        Assert.Equal([ImportStage.Started, ImportStage.Completed], stages);
    }

    [Fact]
    public void Remove_forgets_the_import()
    {
        var store = Store();

        store.Remove("i1");

        Assert.Null(store.Get("i1"));
        Assert.Equal(0, store.Count);
    }
}
