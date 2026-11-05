namespace SkailCatalogImport.Web.Skail;

public sealed class SkailGatewayException(string message, Exception? inner = null) : Exception(message, inner);

public interface ISkailGateway
{
    /// <summary>Starts ImportCatalog; the execution id is the import id.</summary>
    Task TriggerAsync(string importId, CancellationToken ct = default);
}
