using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalCabinetEducationProgram.Data;
using PersonalCabinetEducationProgram.Models;
using PersonalCabinetEducationProgram.Services;

namespace PersonalCabinetEducationProgram.Tests;

public sealed class AccountSecurityIntegrationTests
{
    [Fact]
    public async Task FifthConsecutiveInvalidFile_BlocksAccount_AndAdminCanUnlockIt()
    {
        using var factory = new CustomWebApplicationFactory();
        _ = factory.Services;
        using var scope = factory.Services.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = CreateHttpContext(scope.ServiceProvider, 1, AppRoles.Manager);
        var security = scope.ServiceProvider.GetRequiredService<AccountSecurityService>();
        var registry = scope.ServiceProvider.GetRequiredService<SecurityBlockedAccountRegistry>();

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            await security.RecordInvalidUploadAsync(
                $"invalid-{attempt}.exe",
                128,
                "Недопустимое расширение .exe.",
                countsTowardsBlock: true);
        }

        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.ChangeTracker.Clear();
        var blockedUser = await context.Users.SingleAsync(user => user.Id == 1);
        Assert.Equal(5, blockedUser.ConsecutiveInvalidUploadCount);
        Assert.NotNull(blockedUser.SecurityBlockedAtUtc);
        Assert.NotNull(blockedUser.LockoutEnd);
        Assert.Contains("5 подряд", blockedUser.SecurityBlockReason);
        Assert.True(registry.IsBlocked(1));

        using (var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }))
        {
            client.DefaultRequestHeaders.Add("X-Test-UserId", "4");
            client.DefaultRequestHeaders.Add("X-Test-Role", AppRoles.Admin);
            var response = await client.GetAsync("/Admin/Users");
            var html = await response.Content.ReadAsStringAsync();
            Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Разблокировать", html);
            Assert.Contains("Заблокирован", html);
        }

        accessor.HttpContext = CreateHttpContext(scope.ServiceProvider, 4, AppRoles.Admin);
        var unlock = await security.UnlockAsync(1, 4, "Файлы проверены администратором.");

        Assert.True(unlock.Succeeded, unlock.Error);
        context.ChangeTracker.Clear();
        var unlockedUser = await context.Users.SingleAsync(user => user.Id == 1);
        Assert.Equal(0, unlockedUser.ConsecutiveInvalidUploadCount);
        Assert.Null(unlockedUser.SecurityBlockedAtUtc);
        Assert.Null(unlockedUser.SecurityBlockReason);
        Assert.Null(unlockedUser.LockoutEnd);
        Assert.False(registry.IsBlocked(1));
        Assert.Contains(
            await context.AuditLogs.ToListAsync(),
            entry => entry.UserId == 4 && entry.EntityId == 1 && entry.Action == "SecurityUnlocked");
    }

    [Fact]
    public async Task TwentyFirstDownloadWithinMinute_BlocksAccount()
    {
        using var factory = new CustomWebApplicationFactory();
        _ = factory.Services;
        using var scope = factory.Services.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = CreateHttpContext(scope.ServiceProvider, 2, AppRoles.Approver);
        var security = scope.ServiceProvider.GetRequiredService<AccountSecurityService>();

        for (var download = 1; download <= 21; download++)
            await security.RecordSuccessfulDownloadAsync($"document-{download}.pdf", 1024);

        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.ChangeTracker.Clear();
        var blockedUser = await context.Users.SingleAsync(user => user.Id == 2);
        Assert.NotNull(blockedUser.SecurityBlockedAtUtc);
        Assert.Contains("21-й файл", blockedUser.SecurityBlockReason);
        Assert.True(scope.ServiceProvider
            .GetRequiredService<SecurityBlockedAccountRegistry>()
            .IsBlocked(2));
    }

    private static DefaultHttpContext CreateHttpContext(
        IServiceProvider services,
        int userId,
        string role)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = services,
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Name, $"Пользователь {userId}"),
                new Claim(ClaimTypes.Role, role),
                new Claim("Username", $"user-{userId}")
            ], "Test"))
        };
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("10.20.30.40");
        context.Request.Path = "/test/security";
        context.Request.Method = HttpMethods.Post;
        return context;
    }
}
