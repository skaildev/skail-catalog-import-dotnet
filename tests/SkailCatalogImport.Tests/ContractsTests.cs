using System.Text.Json;
using SkailCatalogImport.Contracts;

namespace SkailCatalogImport.Tests;

public class ContractsTests
{
    [Fact]
    public void Limits_match_the_demo()
    {
        Assert.Equal(20, CatalogLimits.PageSize);
        Assert.Equal(2, CatalogLimits.SecondsBetweenPages);
        Assert.Equal(50, CatalogLimits.MaxPages);
        Assert.Equal(194, CatalogLimits.ExpectedProducts);
    }

    [Fact]
    public void Expected_pages_round_up_and_the_last_page_has_14_products()
    {
        Assert.Equal(10, CatalogLimits.ExpectedPages);
        Assert.Equal(14, CatalogLimits.ExpectedProducts - (CatalogLimits.ExpectedPages - 1) * CatalogLimits.PageSize);
    }

    [Fact]
    public void The_simulated_failure_is_inside_the_catalog_and_below_the_command_retries()
    {
        Assert.InRange(SimulatedFailure.Page, 1, CatalogLimits.ExpectedPages);
        Assert.True(SimulatedFailure.Times < 3);
    }

    [Fact]
    public void Completed_text_says_products_and_pages()
    {
        Assert.Equal("194 products in 10 pages", ImportText.Completed(194, 10));
        Assert.Equal("0 products in 1 page", ImportText.Completed(0, 1));
    }

    [Fact]
    public void Events_have_the_same_property_names_in_both_languages_and_stages_as_strings()
    {
        var evt = new ImportEvent("i1", ImportStage.Failed, "Page 3: boom");
        var json = JsonSerializer.Serialize(evt, Json.Options);

        Assert.Equal("{\"importId\":\"i1\",\"stage\":\"Failed\",\"detail\":\"Page 3: boom\"}", json);
        Assert.Equal(evt, JsonSerializer.Deserialize<ImportEvent>(json, Json.Options));
    }

    [Fact]
    public void Page_record_round_trips()
    {
        var record = new PageRecord(3, [new Product(41, "Soap", "beauty", 4.5m)], 398);

        var back = JsonSerializer.Deserialize<PageRecord>(JsonSerializer.Serialize(record, Json.Options), Json.Options)!;

        Assert.Equal(3, back.Page);
        Assert.Equal(new Product(41, "Soap", "beauty", 4.5m), Assert.Single(back.Products));
        Assert.Equal(398, back.DurationMs);
    }
}
