using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

namespace PersonalCabinetEducationProgram.Services
{
    public sealed class SecurityBlockedAccountRegistry
    {
        private readonly ConcurrentDictionary<int, byte> _blockedUserIds = new();

        public bool IsBlocked(int userId) => _blockedUserIds.ContainsKey(userId);
        public void Block(int userId) => _blockedUserIds[userId] = 0;
        public void Unblock(int userId) => _blockedUserIds.TryRemove(userId, out _);
    }

    public sealed class SecurityBlockedAccountMiddleware
    {
        private readonly RequestDelegate _next;

        public SecurityBlockedAccountMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(
            HttpContext context,
            SecurityBlockedAccountRegistry registry)
        {
            var userIdValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdValue, out var userId) || !registry.IsBlocked(userId))
            {
                await _next(context);
                return;
            }

            await context.SignOutAsync(IdentityConstants.ApplicationScheme);
            if (HttpMethods.IsGet(context.Request.Method) && AcceptsHtml(context.Request))
            {
                context.Response.Redirect("/Account/Login?securityBlocked=true");
                return;
            }

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "text/plain; charset=utf-8";
            await context.Response.WriteAsync("Учётная запись заблокирована службой безопасности. Обратитесь к администратору.");
        }

        private static bool AcceptsHtml(HttpRequest request) =>
            request.Headers.Accept.Any(value =>
                value?.Contains("text/html", StringComparison.OrdinalIgnoreCase) == true);
    }
}
