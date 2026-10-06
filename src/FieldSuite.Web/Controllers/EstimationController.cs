using FieldSuite.Domain.Common;
using FieldSuite.Domain.Entities;
using FieldSuite.Infrastructure.Data;
using FieldSuite.Infrastructure.Services;
using FieldSuite.Web.Services;
using FieldSuite.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FieldSuite.Web.Controllers;

[Authorize]
public class EstimationController : AppController
{
    private readonly ICostSuggestionService _costSuggest;

    public EstimationController(AppDbContext db, ICurrentUser current, IAuditService audit, ICostSuggestionService costSuggest)
        : base(db, current, audit)
    {
        _costSuggest = costSuggest;
    }

    [HttpGet]
    public async Task<IActionResult> Index(EstimateStatus? status = null)
    {
        IQueryable<Estimate> query = Db.Estimates
            .Where(e => e.OrganizationId == OrgId)
            .Include(e => e.Categories)
                .ThenInclude(c => c.Items)
            .Include(e => e.Project)
            .Include(e => e.CreatedBy);

        if (status.HasValue)
        {
            query = query.Where(e => e.Status == status.Value);
        }

        var estimates = await query.OrderByDescending(e => e.CreatedAt).ToListAsync();

        var vm = estimates.Select(e => new EstimateListItemViewModel
        {
            Id = e.Id,
            Reference = e.Reference,
            Title = e.Title,
            ClientName = e.ClientName,
            Status = e.Status,
            ValidUntil = e.ValidUntil,
            GrandTotal = EstimateTotals.GrandTotal(e)
        }).ToList();

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        ViewBag.Projects = await Db.Projects
            .Where(p => p.OrganizationId == OrgId && p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync();

        return View(new EstimateCreateViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(EstimateCreateViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Title))
            ModelState.AddModelError(nameof(model.Title), "Title is required.");

        ViewBag.Projects = await Db.Projects
            .Where(p => p.OrganizationId == OrgId && p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync();

        if (!ModelState.IsValid) return View(model);

        var estimate = new Estimate
        {
            OrganizationId = OrgId,
            ProjectId = model.ProjectId,
            Reference = "TMP-" + Guid.NewGuid().ToString("N").Substring(0, 24),
            Title = model.Title,
            ClientName = model.ClientName,
            Status = EstimateStatus.Draft,
            ValidUntil = model.ValidUntil,
            MarginPercent = model.MarginPercent,
            Notes = model.Notes,
            CreatedById = Current.UserId,
            CreatedAt = DateTime.UtcNow
        };

        Db.Estimates.Add(estimate);
        await Db.SaveChangesAsync();

        estimate.Reference = $"EST-{estimate.Id:D4}";
        Db.Estimates.Update(estimate);

        estimate.Categories.Add(new EstimateCategory
        {
            EstimateId = estimate.Id,
            Name = "General",
            SortOrder = 1
        });

        await Db.SaveChangesAsync();
        await AuditAsync("Create", "Estimate", estimate.Id, $"Created estimate {estimate.Reference}");

        TempData["Success"] = "Estimate created.";
        return RedirectToAction(nameof(Details), new { id = estimate.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var estimate = await Db.Estimates
            .Include(e => e.Categories)
                .ThenInclude(c => c.Items)
            .Include(e => e.Project)
            .Include(e => e.CreatedBy)
            .FirstOrDefaultAsync(e => e.Id == id && e.OrganizationId == OrgId);

        if (estimate is null) return NotFound();

        ViewBag.UnitRateSuggestion = await _costSuggest.SuggestUnitRateAsync(OrgId, string.Empty, string.Empty);

        return View(estimate);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Estimator,Admin,SiteManager")]
    public async Task<IActionResult> ItemAdd(int categoryId, string description, decimal quantity, string unit, decimal unitRate, decimal wastePercent)
    {
        var category = await Db.EstimateCategories
            .Include(c => c.Estimate)
            .FirstOrDefaultAsync(c => c.Id == categoryId);

        if (category is null) return NotFound();
        if (category.Estimate == null || category.Estimate.OrganizationId != OrgId) return NotFound();

        var item = new EstimateItem
        {
            EstimateCategoryId = categoryId,
            Description = description ?? string.Empty,
            Quantity = quantity,
            Unit = unit ?? string.Empty,
            UnitRate = unitRate,
            WastePercent = wastePercent
        };

        Db.EstimateItems.Add(item);
        await Db.SaveChangesAsync();
        await AuditAsync("Create", "EstimateItem", item.Id, $"Added item to {category.Estimate.Reference}");
        TempData["Success"] = "Item added.";
        return RedirectToAction(nameof(Details), new { id = category.EstimateId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Estimator,Admin,SiteManager")]
    public async Task<IActionResult> ItemEdit(int id, string description, decimal quantity, string unit, decimal unitRate, decimal wastePercent)
{
        var item = await Db.EstimateItems
            .Include(i => i.Category)
                .ThenInclude(c => c!.Estimate)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (item is null) return NotFound();
        if (item.Category == null || item.Category.Estimate == null || item.Category.Estimate.OrganizationId != OrgId) return NotFound();

        item.Description = description ?? string.Empty;
        item.Quantity = quantity;
        item.Unit = unit ?? string.Empty;
        item.UnitRate = unitRate;
        item.WastePercent = wastePercent;

        Db.EstimateItems.Update(item);
        await Db.SaveChangesAsync();
        await AuditAsync("Update", "EstimateItem", item.Id, $"Updated item in {item.Category.Estimate.Reference}");
        TempData["Success"] = "Item updated.";
        return RedirectToAction(nameof(Details), new { id = item.Category.EstimateId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Estimator,Admin,SiteManager")]
    public async Task<IActionResult> ItemDelete(int id)
{
        var item = await Db.EstimateItems
            .Include(i => i.Category)
                .ThenInclude(c => c!.Estimate)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (item is null) return NotFound();
        if (item.Category == null || item.Category.Estimate == null || item.Category.Estimate.OrganizationId != OrgId) return NotFound();
        Db.EstimateItems.Remove(item);
await Db.SaveChangesAsync();
        await AuditAsync("Delete", "EstimateItem", id, $"Deleted item from {item.Category.Estimate.Reference}");
        TempData["Success"] = "Item deleted.";
        return RedirectToAction(nameof(Details), new { id = item.Category.EstimateId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Estimator,Admin,SiteManager")]
    public async Task<IActionResult> CategoryAdd(int estimateId, string name)
    {
        var estimate = await Db.Estimates
            .Include(e => e.Categories)
            .FirstOrDefaultAsync(e => e.Id == estimateId && e.OrganizationId == OrgId);

        if (estimate is null) return NotFound();

        int sortOrder = estimate.Categories.Count + 1;
        var category = new EstimateCategory
        {
            EstimateId = estimateId,
            Name = name ?? "General",
            SortOrder = sortOrder
        };

        Db.EstimateCategories.Add(category);
        await Db.SaveChangesAsync();
        await AuditAsync("Create", "EstimateCategory", category.Id, $"Added category to {estimate.Reference}");
        TempData["Success"] = "Category added.";
        return RedirectToAction(nameof(Details), new { id = estimateId });
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> SuggestRate(string unit, string description)
    {
        var rate = await _costSuggest.SuggestUnitRateAsync(OrgId, unit ?? string.Empty, description ?? string.Empty);
        return Json(new { rate });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Estimator,Admin,SiteManager")]
    public async Task<IActionResult> StatusChange(int id, EstimateStatus to)
    {
        var estimate = await Db.Estimates.FirstOrDefaultAsync(e => e.Id == id && e.OrganizationId == OrgId);
        if (estimate is null) return NotFound();

        var from = estimate.Status;
        bool allowed = false;

        if (from == EstimateStatus.Draft && to == EstimateStatus.Sent) allowed = true;
        if (from == EstimateStatus.Sent && (to == EstimateStatus.Accepted || to == EstimateStatus.Rejected || to == EstimateStatus.Expired)) allowed = true;
        if (from == EstimateStatus.Draft && to == EstimateStatus.Expired) allowed = true;
        if (from == EstimateStatus.Draft && to == EstimateStatus.Rejected) allowed = true;

        if (!allowed)
        {
            TempData["Error"] = "Invalid status transition.";
            return RedirectToAction(nameof(Details), new { id });
        }

        estimate.Status = to;
        Db.Estimates.Update(estimate);
        await Db.SaveChangesAsync();
        await AuditAsync("StatusChange", "Estimate", estimate.Id, $"Status {from} → {to}");
        TempData["Success"] = "Status updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Proposal(int id)
    {
        var estimate = await Db.Estimates
            .Include(e => e.Categories)
                .ThenInclude(c => c.Items)
            .Include(e => e.Project)
            .Include(e => e.CreatedBy)
            .FirstOrDefaultAsync(e => e.Id == id && e.OrganizationId == OrgId);

        if (estimate is null) return NotFound();

        return View(estimate);
    }
}
