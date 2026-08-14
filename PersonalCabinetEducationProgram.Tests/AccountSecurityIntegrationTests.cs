using System.Security.Claims;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text.RegularExpressions;
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

    [Fact]
    public async Task FortySixHttpDownloads_AreAllLogged_AndAccountIsBlockedAfterTwentyFirst()
    {
        var storagePath = Path.Combine(Path.GetTempPath(), $"download-security-{Guid.NewGuid():N}");
        Directory.CreateDirectory(storagePath);
        try
        {
            const string storedFileName = "security-download.pdf";
            await File.WriteAllBytesAsync(
                Path.Combine(storagePath, storedFileName),
                "%PDF-1.4\nsecurity test"u8.ToArray());

            using var factory = new CustomWebApplicationFactory(services =>
                services.PostConfigure<FileStorageSettings>(options => options.StoragePath = storagePath));
            var fileId = await SeedDownloadFileAsync(factory, storedFileName);
            using var client = CreateClient(factory, 1, AppRoles.Manager);
            var token = await GetAntiforgeryTokenAsync(client, "/ManagerHome/Index?programId=1");
            var statuses = new List<HttpStatusCode>();

            for (var attempt = 1; attempt <= 46; attempt++)
            {
                using var response = await PostFormAsync(
                    client,
                    "/ElementFiles/Download",
                    token,
                    ("id", fileId.ToString()));
                statuses.Add(response.StatusCode);

                if (attempt == 1)
                {
                    Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
                    Assert.Contains("no-cache", response.Headers.CacheControl?.ToString());
                }
            }

            Assert.Equal(21, statuses.Count(status => status == HttpStatusCode.OK));
            Assert.Equal(25, statuses.Count(status => status == HttpStatusCode.Forbidden));

            await WaitUntilAsync(async () =>
            {
                await using var scope = factory.Services.CreateAsyncScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                return await context.SystemRequestLogs.CountAsync(log =>
                           log.Path == "/ElementFiles/Download") == 46 &&
                       await context.SecurityEventLogs.AnyAsync(log =>
                           log.UserId == 1 && log.EventType == SecurityEventTypes.MassDownload) &&
                       await context.SecurityEventLogs.AnyAsync(log =>
                           log.UserId == 1 && log.EventType == SecurityEventTypes.AccountAutomaticallyBlocked);
            });

            await using var verificationScope = factory.Services.CreateAsyncScope();
            var verificationContext = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await verificationContext.Users.SingleAsync(item => item.Id == 1);
            Assert.NotNull(user.SecurityBlockedAtUtc);
            Assert.True(verificationScope.ServiceProvider
                .GetRequiredService<SecurityBlockedAccountRegistry>()
                .IsBlocked(1));

            var requestLogs = await verificationContext.SystemRequestLogs
                .Where(log => log.Path == "/ElementFiles/Download")
                .ToListAsync();
            Assert.Equal(46, requestLogs.Count);
            Assert.Equal(21, requestLogs.Count(log => log.StatusCode == StatusCodes.Status200OK));
            Assert.Equal(25, requestLogs.Count(log => log.StatusCode == StatusCodes.Status403Forbidden));
        }
        finally
        {
            Directory.Delete(storagePath, recursive: true);
        }
    }

    [Fact]
    public async Task UnlockEndpoint_RejectsIdor_AndRestoresOnlyAfterAdministratorPost()
    {
        using var factory = new CustomWebApplicationFactory();
        await SetBlockedAsync(factory, userId: 1);

        using (var blockedUser = CreateClient(factory, 1, AppRoles.Manager))
        {
            blockedUser.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(MediaTypeNames.Text.Html));
            using var blockedResponse = await blockedUser.GetAsync("/ManagerHome/Index?programId=1");
            Assert.Equal(HttpStatusCode.Redirect, blockedResponse.StatusCode);
            Assert.Equal("/Account/Login?securityBlocked=true", blockedResponse.Headers.Location?.OriginalString);
        }

        using (var attacker = CreateClient(factory, 2, AppRoles.Approver))
        {
            var attackerToken = await GetAntiforgeryTokenAsync(attacker, "/ApproverHome/Index");
            using var denied = await PostFormAsync(
                attacker,
                "/Admin/UnlockUser",
                attackerToken,
                ("id", "1"),
                ("reviewNote", "IDOR attempt"));
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }

        await AssertBlockedAsync(factory, userId: 1);

        using (var administrator = CreateClient(factory, 4, AppRoles.Admin))
        {
            var adminToken = await GetAntiforgeryTokenAsync(administrator, "/Admin/Users");
            using var unlocked = await PostFormAsync(
                administrator,
                "/Admin/UnlockUser",
                adminToken,
                ("id", "1"),
                ("reviewNote", "Verified by administrator"));
            Assert.Equal(HttpStatusCode.Redirect, unlocked.StatusCode);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await context.Users.SingleAsync(item => item.Id == 1);
            Assert.Null(user.SecurityBlockedAtUtc);
            Assert.Null(user.SecurityBlockReason);
            Assert.Null(user.LockoutEnd);
            Assert.False(scope.ServiceProvider.GetRequiredService<SecurityBlockedAccountRegistry>().IsBlocked(1));
            Assert.Contains(await context.AuditLogs.ToListAsync(), item =>
                item.UserId == 4 && item.EntityId == 1 && item.Action == "SecurityUnlocked");
        }

        using var restoredUser = CreateClient(factory, 1, AppRoles.Manager);
        using var restoredResponse = await restoredUser.GetAsync("/ManagerHome/Index?programId=1");
        Assert.Equal(HttpStatusCode.OK, restoredResponse.StatusCode);
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

    private static HttpClient CreateClient(
        CustomWebApplicationFactory factory,
        int userId,
        string role)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Role", role);
        return client;
    }

    private static async Task<HttpResponseMessage> PostFormAsync(
        HttpClient client,
        string url,
        string antiforgeryToken,
        params (string Name, string Value)[] values)
    {
        var form = values.ToDictionary(pair => pair.Name, pair => pair.Value);
        form["__RequestVerificationToken"] = antiforgeryToken;
        return await client.PostAsync(url, new FormUrlEncodedContent(form));
    }

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string url)
    {
        var html = await client.GetStringAsync(url);
        var match = Regex.Match(
            html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"",
            RegexOptions.CultureInvariant);
        Assert.True(match.Success, $"Antiforgery token was not rendered by {url}.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private static async Task<int> SeedDownloadFileAsync(
        CustomWebApplicationFactory factory,
        string storedFileName)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var file = new EducationalProgramElementFile
        {
            EducationalProgramElementId = 1,
            StoredFileName = storedFileName,
            OriginalFileName = storedFileName,
            RevisionNumber = 1,
            IsCurrent = true,
            UploadedAt = DateTime.UtcNow,
            UploadedByUserId = 1
        };
        context.EducationalProgramElementFiles.Add(file);
        await context.SaveChangesAsync();
        return file.Id;
    }

    private static async Task SetBlockedAsync(CustomWebApplicationFactory factory, int userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await context.Users.SingleAsync(item => item.Id == userId);
        user.SecurityBlockedAtUtc = DateTime.UtcNow;
        user.SecurityBlockReason = "Security integration test";
        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.UtcNow.AddYears(100);
        await context.SaveChangesAsync();
        scope.ServiceProvider.GetRequiredService<SecurityBlockedAccountRegistry>().Block(userId);
    }

    private static async Task AssertBlockedAsync(CustomWebApplicationFactory factory, int userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await context.Users.SingleAsync(item => item.Id == userId);
        Assert.NotNull(user.SecurityBlockedAtUtc);
        Assert.NotNull(user.LockoutEnd);
        Assert.True(scope.ServiceProvider.GetRequiredService<SecurityBlockedAccountRegistry>().IsBlocked(userId));
        Assert.DoesNotContain(await context.AuditLogs.ToListAsync(), item =>
            item.EntityId == userId && item.Action == "SecurityUnlocked");
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        var timeoutAt = DateTime.UtcNow.AddSeconds(8);
        while (DateTime.UtcNow < timeoutAt)
        {
            if (await condition())
                return;
            await Task.Delay(100);
        }

        Assert.True(await condition(), "The background security log writer did not persist all expected entries.");
    }
}
