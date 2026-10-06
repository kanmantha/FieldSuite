using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FieldSuite.Infrastructure.Services;

public class LlmOptions
{
    public const string SectionName = "Llm";
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
}

public interface ILLMClient
{
    bool IsConfigured { get; }
    Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default);
}

public class OpenAICompatibleLlmClient : ILLMClient
{
    public const string HttpClientName = "llm";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly LlmOptions _options;

    public OpenAICompatibleLlmClient(IHttpClientFactory httpClientFactory, LlmOptions options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.BaseUrl) && !string.IsNullOrWhiteSpace(_options.Model);

    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        if (!IsConfigured) return string.Empty;

        var client = _httpClientFactory.CreateClient(HttpClientName);
        var payload = new
        {
            model = _options.Model,
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            temperature = 0.2,
            max_tokens = 800
        };

        var request = new HttpRequestMessage(HttpMethod.Post, _options.BaseUrl.TrimEnd('/') + "/chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.ApiKey);

        try
        {
            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) return string.Empty;
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                return choices[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
            }
        }
        catch
        {
            return string.Empty;
        }
        return string.Empty;
    }
}

public record StructuredDraft(string Title, string Description, string Category, string Severity);

public interface IStructuredDraftService
{
    Task<StructuredDraft> DraftAsync(string kind, string rawInput);
    bool IsLlmAvailable { get; }
}

public class StructuredDraftService : IStructuredDraftService
{
    private readonly ILLMClient _llm;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public StructuredDraftService(ILLMClient llm) => _llm = llm;
    public bool IsLlmAvailable => _llm.IsConfigured;

    public async Task<StructuredDraft> DraftAsync(string kind, string rawInput)
    {
        var input = (rawInput ?? string.Empty).Trim();
        if (input.Length == 0) return new StructuredDraft("", "", kind, "Low");

        if (_llm.IsConfigured)
        {
            var system = kind == "incident"
                ? "You convert field voice notes into structured incident reports. Reply with ONLY JSON: {\"title\": string, \"description\": string, \"category\": \"Incident|NearMiss|FirstAid\", \"severity\": \"Low|Medium|High|Critical\"}."
                : "You convert field notes into structured snag/punch items. Reply with ONLY JSON: {\"title\": string, \"description\": string, \"category\": string, \"severity\": \"Low|Medium|High|Critical\"}.";
            var raw = await _llm.CompleteAsync(system, input);
            try
            {
                var parsed = JsonSerializer.Deserialize<StructuredDraft>(raw.Trim().TrimStart('`').Replace("json", "").Trim().Trim('"'), JsonOptions);
                if (parsed is not null && !string.IsNullOrWhiteSpace(parsed.Title))
                    return parsed with { Description = string.IsNullOrWhiteSpace(parsed.Description) ? input : parsed.Description };
            }
            catch
            {
                // fall through to heuristic draft
            }
        }

        return HeuristicDraft(kind, input);
    }

    private static StructuredDraft HeuristicDraft(string kind, string input)
    {
        var lower = input.ToLowerInvariant();
        var severity =
            ContainsAny(lower, "fatality", "fatal", "death", "collapsed", "unconscious", "amputation") ? "Critical" :
            ContainsAny(lower, "hospital", "fracture", "fall from", "fire", "explosion", "gas leak", "spill") ? "High" :
            ContainsAny(lower, "cut", "bruise", "strain", "hit", "near miss", "close call") ? "Medium" : "Low";

        var category = kind == "incident"
            ? (ContainsAny(lower, "near miss", "near-miss", "close call", "almost") ? "NearMiss" :
               ContainsAny(lower, "first aid", "plaster", "bandage") ? "FirstAid" : "Incident")
            : "";

        var firstSentence = input.Split('.', '!', '?', '\n')
            .FirstOrDefault(s => s.Trim().Length > 3)?.Trim() ?? input;
        var title = firstSentence.Length > 90 ? firstSentence[..90] + "..." : firstSentence;

        return new StructuredDraft(title, input, category, severity);
    }

    private static bool ContainsAny(string haystack, params string[] needles) =>
        needles.Any(n => haystack.Contains(n, StringComparison.Ordinal));
}
