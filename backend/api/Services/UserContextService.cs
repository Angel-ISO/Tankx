using System.Security.Claims;

namespace backend.api.Services;

public sealed class UserContextService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public UserContextService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? GetUserId()
    {
        var subject = _httpContextAccessor.HttpContext?.User.FindFirstValue("sub");
        return Guid.TryParse(subject, out var userId) ? userId : null;
    }
}
