using FieldSuite.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FieldSuite.Infrastructure.Services;

public interface ICostSuggestionService
{
    Task<decimal?> SuggestUnitRateAsync(int organizationId, string unit, string description);
}

public class CostSuggestionService : ICostSuggestionService
{
    private readonly AppDbContext _db;
    public CostSuggestionService(AppDbContext db) => _db = db;

    public async Task<decimal?> SuggestUnitRateAsync(int organizationId, string unit, string description)
    {
        if (string.IsNullOrWhiteSpace(unit)) return null;

        var unitKey = unit.Trim().ToLowerInvariant();
        var wordKey = description.Trim().ToLowerInvariant();

        var unitMatches = await _db.EstimateItems
            .Where(i => i.Unit.ToLower() == unitKey)
            .Where(i => i.Category!.Estimate!.OrganizationId == organizationId)
            .Select(i => (decimal?)i.UnitRate)
            .ToListAsync();

        if (unitMatches.Count == 0) return null;

        var exact = unitMatches.Count >= 5;
        if (!exact && wordKey.Length > 3)
        {
            var firstWord = wordKey.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
            var wordMatches = await _db.EstimateItems
                .Where(i => i.Unit.ToLower() == unitKey && i.Description.ToLower().Contains(firstWord))
                .Where(i => i.Category!.Estimate!.OrganizationId == organizationId)
                .Select(i => (decimal?)i.UnitRate)
                .ToListAsync();
            if (wordMatches.Count > 0)
            {
                exact = true;
                unitMatches = wordMatches;
            }
        }

        if (!exact && unitMatches.Count == 0) return null;
        var average = unitMatches.Average() ?? 0;
        return Math.Round(average, 2);
    }
}

