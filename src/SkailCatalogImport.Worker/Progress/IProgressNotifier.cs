using SkailCatalogImport.Contracts;

namespace SkailCatalogImport.Worker.Progress;

public interface IProgressNotifier
{
    Task NotifyAsync(ImportEvent evt, CancellationToken ct = default);
}
