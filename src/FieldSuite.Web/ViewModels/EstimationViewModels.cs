using FieldSuite.Domain.Entities;

namespace FieldSuite.Web.ViewModels;

public static class EstimateTotals
{
    public static decimal ItemLineTotal(EstimateItem item) => item.Quantity * item.UnitRate * (1 + item.WastePercent / 100m);

    public static decimal CategorySubtotal(EstimateCategory category)
    {
        if (category.Items == null) return 0m;
        return category.Items.Sum(ItemLineTotal);
    }

    public static decimal Subtotal(Estimate estimate)
    {
        if (estimate.Categories == null) return 0m;
        return estimate.Categories.Sum(CategorySubtotal);
    }

    public static decimal WasteAmount(Estimate estimate)
    {
        if (estimate.Categories == null) return 0m;
        decimal waste = 0m;
        foreach (var cat in estimate.Categories)
        {
            if (cat.Items == null) continue;
            foreach (var item in cat.Items)
            {
                waste += item.Quantity * item.UnitRate * (item.WastePercent / 100m);
            }
        }
        return waste;
    }

    public static decimal MarginAmount(Estimate estimate) => Subtotal(estimate) * (estimate.MarginPercent / 100m);

    public static decimal GrandTotal(Estimate estimate) => Subtotal(estimate) + MarginAmount(estimate);
}

public class EstimateListItemViewModel
{
    public int Id { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public FieldSuite.Domain.Common.EstimateStatus Status { get; set; }
    public DateTime? ValidUntil { get; set; }
    public decimal GrandTotal { get; set; }
}

public class EstimateCreateViewModel
{
    public string Title { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public int? ProjectId { get; set; }
    public DateTime? ValidUntil { get; set; }
    public decimal MarginPercent { get; set; }
    public string Notes { get; set; } = string.Empty;
}
