using SkailCatalogImport.Contracts;
using SkailCatalogImport.Web.Imports;

namespace SkailCatalogImport.Tests;

public class ButtonRulesTests
{
    private static ImportState Import(ImportStage stage) => new("i1", stage, false, [], null);

    [Fact]
    public void Without_an_import_the_button_imports()
    {
        var ui = ButtonRules.For(null);

        Assert.Equal("Import catalog", ui.Label);
        Assert.True(ui.Enabled);
    }

    [Theory]
    [InlineData(ImportStage.Starting)]
    [InlineData(ImportStage.Started)]
    public void While_importing_the_button_is_disabled(ImportStage stage)
    {
        var ui = ButtonRules.For(Import(stage));

        Assert.Equal("Importing…", ui.Label);
        Assert.False(ui.Enabled);
    }

    [Theory]
    [InlineData(ImportStage.Completed)]
    [InlineData(ImportStage.Failed)]
    public void After_the_end_the_button_is_available_again(ImportStage stage)
    {
        var ui = ButtonRules.For(Import(stage));

        Assert.Equal("Import catalog", ui.Label);
        Assert.True(ui.Enabled);
    }
}
