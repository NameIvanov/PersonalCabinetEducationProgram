using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using PersonalCabinetEducationProgram.Data;

namespace PersonalCabinetEducationProgram.Services;

public sealed class UserLoginSessionMiddleware
{
    private readonly RequestDelegate _next;

    public UserLoginSessionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ApplicationDbContext dbContext,
        IMemoryCache cache,
        IOptions<SecurityMonitoringOptions> options,
        TimeProvider timeProvider)
    {
        var sessionId = context.User.FindFirstValue(LoginSecurityService.SessionIdClaimType);
        var userIdValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(sessionId) && int.TryParse(userIdValue, out var userId))
        {
            var cacheKey = $"login-session-activity:{sessionId}";
            if (!cache.TryGetValue(cacheKey, out _))
            {
                var now = timeProvider.GetUtcNow().UtcDateTime;
                var intervalMinutes = Math.Max(1, options.Value.SessionActivityUpdateMinutes);
                var threshold = now.AddMinutes(-intervalMinutes);
                var session = await dbContext.UserLoginSessions.SingleOrDefaultAsync(item =>
                    item.SessionId == sessionId && item.UserId == userId && item.IsActive);
                if (session != null && session.LastActivityAtUtc <= threshold)
                {
                    session.LastActivityAtUtc = now;
                    await dbContext.SaveChangesAsync(context.RequestAborted);
                }

                cache.Set(cacheKey, true, TimeSpan.FromMinutes(intervalMinutes));
            }
        }

        await _next(context);
    }
}
