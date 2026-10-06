using FieldSuite.Domain.Common;

namespace FieldSuite.Domain.Entities;

public class Estimate : Entity
{
    public int OrganizationId { get; set; }
    public int? ProjectId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public EstimateStatus Status { get; set; } = EstimateStatus.Draft;
    public DateTime? ValidUntil { get; set; }
    public decimal MarginPercent { get; set; }
    public string Notes { get; set; } = string.Empty;
    public string CreatedById { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Project? Project { get; set; }
    public AppUser? CreatedBy { get; set; }
    public ICollection<EstimateCategory> Categories { get; set; } = new List<EstimateCategory>();
}

public class EstimateCategory : Entity
{
    public int EstimateId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }

    public Estimate? Estimate { get; set; }
    public ICollection<EstimateItem> Items { get; set; } = new List<EstimateItem>();
}

public class EstimateItem : Entity
{
    public int EstimateCategoryId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal UnitRate { get; set; }
    public decimal WastePercent { get; set; }

    public EstimateCategory? Category { get; set; }
    public decimal LineTotal => Quantity * UnitRate * (1 + WastePercent / 100);
}
