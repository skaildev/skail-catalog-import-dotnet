using System.Threading.Channels;
using SkailCatalogImport.Contracts;

namespace SkailCatalogImport.Web.Imports;

public enum SaveOutcome
{
    Saved,
    Rejected,
    Unknown,
}

/// <summary>
/// The imports kept in memory, as the page shows them, and the products written to the target catalog. The run
/// itself, with its history, lives in skail.
/// </summary>
public sealed class ImportStore
{
    private sealed class ImportData(ImportState state)
    {
        public ImportState State { get; set; } = state;
        public Dictionary<int, PageRecord> Pages { get; } = [];
        public Dictionary<int, int> Attempts { get; } = [];
        public HashSet<ImportStage> Seen { get; } = [];
        public int SimulatedFailures { get; set; }
    }

    private readonly object _gate = new();
    private readonly Dictionary<string, ImportData> _imports = [];
    private readonly Dictionary<string, List<Channel<ImportState>>> _subscribers = [];

    public int Count
    {
        get { lock (_gate) return _imports.Count; }
    }

    public ImportState Create(string importId, bool simulateFailure = false)
    {
        lock (_gate)
        {
            var state = new ImportState(importId, ImportStage.Starting, simulateFailure, [], null);
            _imports[importId] = new ImportData(state);
            return state;
        }
    }

    public ImportState? Get(string importId)
    {
        lock (_gate) return _imports.GetValueOrDefault(importId)?.State;
    }

    public void Remove(string importId)
    {
        lock (_gate)
        {
            _imports.Remove(importId);
            Complete(importId);
        }
    }

    /// <summary>The products written so far. A page written twice replaces the first write, so there are no duplicates.</summary>
    public IReadOnlyList<Product> Products(string importId)
    {
        lock (_gate)
            return _imports.TryGetValue(importId, out var data)
                ? data.Pages.OrderBy(p => p.Key).SelectMany(p => p.Value.Products).ToList()
                : [];
    }

    /// <summary>
    /// Writes a page. Every call counts as an attempt. With the simulated failure on, the first writes of
    /// <see cref="SimulatedFailure.Page"/> are rejected, and the caller answers 503.
    /// </summary>
    public SaveOutcome SavePage(string importId, PageRecord record)
    {
        lock (_gate)
        {
            if (!_imports.TryGetValue(importId, out var data)) return SaveOutcome.Unknown;

            data.Attempts[record.Page] = data.Attempts.GetValueOrDefault(record.Page) + 1;
            var reject = data.State.SimulateFailure
                && record.Page == SimulatedFailure.Page
                && data.SimulatedFailures < SimulatedFailure.Times;
            if (reject) data.SimulatedFailures++;
            else data.Pages[record.Page] = record;

            var pages = data.Attempts.OrderBy(a => a.Key)
                .Select(a => data.Pages.TryGetValue(a.Key, out var saved)
                    ? new PageSummary(a.Key, saved.Products.Count, saved.DurationMs, a.Value)
                    : new PageSummary(a.Key, null, null, a.Value))
                .ToList();
            var stage = data.State.Stage == ImportStage.Starting ? ImportStage.Started : data.State.Stage;
            Publish(data, data.State with { Stage = stage, Pages = pages });
            return reject ? SaveOutcome.Rejected : SaveOutcome.Saved;
        }
    }

    /// <returns>true when the event changed the import; false when ignored (unknown, finished or repeated stage).</returns>
    public bool Apply(ImportEvent evt)
    {
        lock (_gate)
        {
            if (!_imports.TryGetValue(evt.ImportId, out var data) || data.State.IsTerminal) return false;
            if (!data.Seen.Add(evt.Stage)) return false;

            Publish(data, data.State with { Stage = evt.Stage, Detail = evt.Detail ?? data.State.Detail });
            return true;
        }
    }

    public Subscription Subscribe(string importId)
    {
        var channel = Channel.CreateUnbounded<ImportState>();
        lock (_gate)
        {
            if (!_subscribers.TryGetValue(importId, out var list)) _subscribers[importId] = list = [];
            list.Add(channel);
        }
        return new Subscription(channel.Reader, () =>
        {
            lock (_gate)
            {
                if (_subscribers.TryGetValue(importId, out var list)) list.Remove(channel);
            }
        });
    }

    // must be called with _gate held
    private void Publish(ImportData data, ImportState state)
    {
        data.State = state;
        if (_subscribers.TryGetValue(state.ImportId, out var list))
            foreach (var channel in list) channel.Writer.TryWrite(state);
        if (state.IsTerminal) Complete(state.ImportId);
    }

    private void Complete(string importId)
    {
        if (!_subscribers.TryGetValue(importId, out var list)) return;
        foreach (var channel in list) channel.Writer.TryComplete();
    }

    public sealed class Subscription(ChannelReader<ImportState> reader, Action onDispose) : IDisposable
    {
        public ChannelReader<ImportState> Reader { get; } = reader;
        public void Dispose() => onDispose();
    }
}
