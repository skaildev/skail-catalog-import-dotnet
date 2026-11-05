namespace SkailCatalogImport.Web.Imports;

public sealed record UiState(string Label, bool Enabled);

/// <summary>The button works only when no import is in progress.</summary>
public static class ButtonRules
{
    public static UiState For(ImportState? state) =>
        state is null || state.IsTerminal ? new UiState("Import catalog", Enabled: true) : new UiState("Importing…", Enabled: false);
}
