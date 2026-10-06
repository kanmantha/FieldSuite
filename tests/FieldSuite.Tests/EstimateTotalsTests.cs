using FieldSuite.Domain.Entities;
using FieldSuite.Web.ViewModels;

namespace FieldSuite.Tests;

public class EstimateTotalsTests
{
    private static EstimateItem Item(decimal qty, decimal rate, decimal waste = 0) =>
        new() { Quantity = qty, UnitRate = rate, WastePercent = waste };

    [Fact]
    public void ItemLineTotal_AppliesWastePercentage()
    {
        Assert.Equal(1050m, EstimateTotals.ItemLineTotal(Item(10, 100, 5)));
        Assert.Equal(1000m, EstimateTotals.ItemLineTotal(Item(10, 100)));
        Assert.Equal(0m, EstimateTotals.ItemLineTotal(Item(0, 999)));
    }

    [Fact]
    public void CategorySubtotal_SumsItems()
    {
        var category = new EstimateCategory { Items = { Item(2, 100), Item(3, 50, 10) } };
        Assert.Equal(200m + 165m, EstimateTotals.CategorySubtotal(category));
    }

    [Fact]
    public void EstimateGrandTotal_AddsMarginOnTopOfSubtotal()
    {
        var estimate = new Estimate
        {
            MarginPercent = 15,
            Categories =
            {
                new EstimateCategory { Items = { Item(1, 1000) } },
                new EstimateCategory { Items = { Item(1, 1000) } }
            }
        };

        Assert.Equal(2000m, EstimateTotals.Subtotal(estimate));
        Assert.Equal(300m, EstimateTotals.MarginAmount(estimate));
        Assert.Equal(2300m, EstimateTotals.GrandTotal(estimate));
    }

    [Fact]
    public void EstimateWithNoCategories_TotalsZero()
    {
        var estimate = new Estimate { MarginPercent = 20 };
        Assert.Equal(0m, EstimateTotals.GrandTotal(estimate));
    }
}
