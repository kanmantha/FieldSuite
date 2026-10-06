using System.Security.Claims;

namespace FieldSuite.Web.Services;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    string UserId { get; }
    string FullName { get; }
    string Email { get; }
    int OrgId { get; }
    string Role { get; }
    bool IsInRole(params string[] roles);
}

public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor) => _httpContextAccessor = httpContextAccessor;

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public string UserId => Principal?.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    public string FullName => Principal?.FindFirstValue("full_name")
        ?? Principal?.FindFirstValue(ClaimTypes.Name) ?? string.Empty;

    public string Email => Principal?.FindFirstValue(ClaimTypes.Email) ?? string.Empty;

    public int OrgId
    {
        get
        {
            var raw = Principal?.FindFirstValue("org_id");
            return int.TryParse(raw, out var id) ? id : 0;
        }
    }

    public string Role => Principal?.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

    public bool IsInRole(params string[] roles) =>
        roles.Any(r => string.Equals(r, Role, StringComparison.OrdinalIgnoreCase));
}
