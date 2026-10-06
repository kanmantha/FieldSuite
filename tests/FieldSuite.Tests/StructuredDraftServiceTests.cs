using FieldSuite.Infrastructure.Services;

namespace FieldSuite.Tests;

public class StructuredDraftServiceTests
{
    private sealed class OfflineLlm : ILLMClient
    {
        public bool IsConfigured => false;
        public Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default) =>
            Task.FromResult(string.Empty);
    }

    private static readonly StructuredDraftService Service = new(new OfflineLlm());

    [Fact]
    public async Task EmptyInput_ReturnsEmptyDraft()
    {
        var draft = await Service.DraftAsync("snag", "   ");
        Assert.Equal("", draft.Title);
        Assert.False(Service.IsLlmAvailable);
    }

    [Theory]
    [InlineData("worker fell unconscious after collapse", "Critical")]
    [InlineData("gas leak reported near manifold", "High")]
    [InlineData("worker got a small cut on hand", "Medium")]
    [InlineData("paint finish slightly uneven", "Low")]
    public async Task HeuristicDraft_ClassifiesSeverityFromKeywords(string input, string expected)
    {
        var draft = await Service.DraftAsync("incident", input);
        Assert.Equal(expected, draft.Severity);
    }

    [Fact]
    public async Task IncidentDraft_DetectsNearMissCategory()
    {
        var draft = await Service.DraftAsync("incident", "near miss - scaffolding plank slipped but nobody hurt");
        Assert.Equal("NearMiss", draft.Category);
        Assert.Equal("Medium", draft.Severity);
        Assert.False(string.IsNullOrWhiteSpace(draft.Title));
        Assert.Equal("near miss - scaffolding plank slipped but nobody hurt", draft.Description);
    }

    [Fact]
    public async Task SnagDraft_UsesFirstSentenceAsTitle_AndFullTextAsDescription()
    {
        var draft = await Service.DraftAsync("snag", "Crack above door frame at Level 2. It runs about 400mm diagonally.");
        Assert.Equal("Crack above door frame at Level 2", draft.Title);
        Assert.Equal("Crack above door frame at Level 2. It runs about 400mm diagonally.", draft.Description);
        Assert.Equal("", draft.Category);
    }
}
