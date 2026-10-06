using FieldSuite.Domain.Entities;
using FieldSuite.Infrastructure.Data;
using FieldSuite.Infrastructure.Services;

namespace FieldSuite.Tests;

public class CostSuggestionServiceTests
{
    private static async Task<(AppDbContext db, CostSuggestionService svc)> SetupAsync()
    {
        var db = await TestDbAsync();
        return (db, new CostSuggestionService(db));
    }

    private static async Task<AppDbContext> TestDbAsync()
    {
        var db = TestDb.Create();
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static async Task<EstimateItem> AddItemAsync(AppDbContext db, int orgId, string unit, string description, decimal rate)
    {
        var estimate = new Estimate { OrganizationId = orgId, Title = "T", ClientName = "C", CreatedById = "seed" };
        db.Estimates.Add(estimate);
        await db.SaveChangesAsync();
        var category = new EstimateCategory { EstimateId = estimate.Id, Name = "General" };
        db.EstimateCategories.Add(category);
        await db.SaveChangesAsync();
        var item = new EstimateItem { EstimateCategoryId = category.Id, Unit = unit, Description = description, UnitRate = rate, Quantity = 1 };
        db.EstimateItems.Add(item);
        await db.SaveChangesAsync();
        return item;
    }

    [Fact]
    public async Task NoHistory_ReturnsNull()
    {
        var (db, svc) = await SetupAsync();
        await using var _ = db;
        Assert.Null(await svc.SuggestUnitRateAsync(1, "sqm", "block work"));
    }

    [Fact]
    public async Task UnknownUnit_ReturnsNull()
    {
        var (db, svc) = await SetupAsync();
        await using var _ = db;
        await AddItemAsync(db, 1, "sqm", "plaster", 100);
        Assert.Null(await svc.SuggestUnitRateAsync(1, "kg", "steel"));
    }

    [Fact]
    public async Task FiveOrMoreSameUnit_AveragesAllMatches()
    {
        var (db, svc) = await SetupAsync();
        await using var _ = db;
        for (var i = 1; i <= 6; i++)
            await AddItemAsync(db, 1, "sqm", $"item {i}", 100m + i);

        var suggestion = await svc.SuggestUnitRateAsync(1, "SQM", "anything at all");
        Assert.Equal(103.5m, suggestion);
    }

    [Fact]
    public async Task FewMatches_FiltersByFirstWordOfDescription()
    {
        var (db, svc) = await SetupAsync();
        await using var _ = db;
        await AddItemAsync(db, 1, "sqm", "concrete slab casting", 50);
        await AddItemAsync(db, 1, "sqm", "formwork removal", 70);

        var suggestion = await svc.SuggestUnitRateAsync(1, "sqm", "slab pouring second floor");
        Assert.Equal(50m, suggestion);
    }

    [Fact]
    public async Task HistoryFromOtherOrganizations_IsIgnored()
    {
        var (db, svc) = await SetupAsync();
        await using var _ = db;
        for (var i = 1; i <= 6; i++)
            await AddItemAsync(db, 42, "sqm", $"foreign {i}", 999m);

        Assert.Null(await svc.SuggestUnitRateAsync(1, "sqm", "same unit but other org"));
    }

    [Fact]
    public async Task SuggestionIsRoundedToTwoDecimals()
    {
        var (db, svc) = await SetupAsync();
        await using var _ = db;
        for (var i = 0; i < 6; i++)
            await AddItemAsync(db, 1, "hour", "labour work", 100.111m + i);

        var suggestion = await svc.SuggestUnitRateAsync(1, "hour", "general labour");
        Assert.Equal(Math.Round(suggestion!.Value, 2), suggestion);
        Assert.Equal(102.61m, suggestion);
    }
}
