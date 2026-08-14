using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PersonalCabinetEducationProgram.Controllers;
using PersonalCabinetEducationProgram.Data;
using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Tests;

public sealed class SecurityEndpointCoverageTests
{
    private static readonly Type[] AdministrativeControllers =
        [typeof(AdminController), typeof(AdministrationController)];

    [Fact]
    public void GlobalAntiforgeryAndAdministrativeRoleProtection_AreConfigured()
    {
        using var factory = new CustomWebApplicationFactory();
        var mvcOptions = factory.Services.GetRequiredService<IOptions<MvcOptions>>().Value;

        Assert.Contains(mvcOptions.Filters, filter =>
            filter is AutoValidateAntiforgeryTokenAttribute ||
            filter is TypeFilterAttribute typeFilter &&
            typeFilter.ImplementationType == typeof(AutoValidateAntiforgeryTokenAttribute));

        foreach (var controllerType in AdministrativeControllers)
        {
            var authorize = controllerType.GetCustomAttribute<AuthorizeAttribute>(inherit: true);
            Assert.NotNull(authorize);
            Assert.Contains(AppRoles.Admin,
                (authorize!.Roles ?? string.Empty).Split(',', StringSplitOptions.TrimEntries));
        }
    }

    [Fact]
    public async Task EveryAdministrativeAction_RejectsNonAdministrator()
    {
        using var factory = new CustomWebApplicationFactory();
        var actionNumber = 0;

        foreach (var action in GetAdministrativeActions())
        {
            using var client = CreateClient(factory, 10_000 + actionNumber++, AppRoles.Manager);
            using var request = new HttpRequestMessage(
                IsPost(action.Method) ? HttpMethod.Post : HttpMethod.Get,
                $"/{action.ControllerName}/{action.Method.Name}");
            if (request.Method == HttpMethod.Post)
                request.Content = new FormUrlEncodedContent([]);

            using var response = await client.SendAsync(request);
            Assert.True(response.StatusCode == HttpStatusCode.Forbidden,
                $"{request.Method} {request.RequestUri}: expected 403, received {(int)response.StatusCode}.");
        }
    }

    [Fact]
    public async Task EveryAdministrativeMutation_RejectsGetAndPostWithoutAntiforgeryToken()
    {
        using var factory = new CustomWebApplicationFactory();
        var actionNumber = 0;

        foreach (var action in GetAdministrativeActions().Where(action => IsPost(action.Method)))
        {
            var url = $"/{action.ControllerName}/{action.Method.Name}";

            using (var getClient = CreateClient(factory, 20_000 + actionNumber++, AppRoles.Admin))
            using (var getResponse = await getClient.GetAsync(url))
            {
                Assert.True(getResponse.StatusCode == HttpStatusCode.MethodNotAllowed,
                    $"GET {url}: expected 405, received {(int)getResponse.StatusCode}.");
            }

            using var postClient = CreateClient(factory, 20_000 + actionNumber++, AppRoles.Admin);
            using var postResponse = await postClient.PostAsync(url, new FormUrlEncodedContent([]));
            Assert.True(postResponse.StatusCode == HttpStatusCode.BadRequest,
                $"POST {url} without antiforgery token: expected 400, received {(int)postResponse.StatusCode}.");
        }
    }

    [Fact]
    public async Task Administrator_CanOpenEveryAdministrativeTab()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = CreateClient(factory, 4, AppRoles.Admin);
        var tabs = new[]
        {
            "/Admin/Users",
            "/Admin/Audit",
            "/Admin/Programs",
            "/Admin/ProgramDetails?id=1",
            "/Admin/Assignments",
            "/Admin/Departments",
            "/Admin/DepartmentDetails?id=1",
            "/Admin/Faculties",
            "/Admin/FacultyDetails?id=1",
            "/Administration/Logs",
            "/Administration/Server",
            "/Administration/Storage",
            "/Administration/Security",
            "/Administration/UserNetworks?userId=1",
            "/Administration/Audit"
        };

        foreach (var tab in tabs)
        {
            using var response = await client.GetAsync(tab);
            Assert.True(response.StatusCode == HttpStatusCode.OK,
                $"GET {tab}: expected 200, received {(int)response.StatusCode}.");
        }
    }

    [Fact]
    public async Task SecurityEventStatuses_WorkThroughHttp_AndUnauthorizedChangesDoNotPersist()
    {
        using var factory = new CustomWebApplicationFactory();
        var eventIds = await SeedSecurityEventsAsync(factory, SecurityEventStatuses.All.Count + 1);

        using (var manager = CreateClient(factory, 1, AppRoles.Manager))
        {
            var token = await GetAntiforgeryTokenAsync(manager, "/ManagerHome/Index?programId=1");
            using var denied = await PostFormAsync(manager, "/Administration/UpdateSecurityEvent", token,
                ("id", eventIds[0].ToString()),
                ("status", SecurityEventStatuses.Resolved),
                ("reviewNote", "Unauthorized review"));
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }

        using var admin = CreateClient(factory, 4, AppRoles.Admin);
        var adminToken = await GetAntiforgeryTokenAsync(admin, "/Administration/Security");

        using (var missingCsrf = await admin.PostAsync(
                   "/Administration/UpdateSecurityEvent",
                   new FormUrlEncodedContent(new Dictionary<string, string>
                   {
                       ["id"] = eventIds[0].ToString(),
                       ["status"] = SecurityEventStatuses.Investigating
                   })))
        {
            Assert.Equal(HttpStatusCode.BadRequest, missingCsrf.StatusCode);
        }

        using (var invalidStatus = await PostFormAsync(admin, "/Administration/UpdateSecurityEvent", adminToken,
                   ("id", eventIds[0].ToString()), ("status", "Unknown")))
        {
            Assert.Equal(HttpStatusCode.BadRequest, invalidStatus.StatusCode);
        }

        var statusIndex = 0;
        foreach (var status in SecurityEventStatuses.All)
        {
            var values = new List<(string Name, string Value)>
            {
                ("id", eventIds[statusIndex++].ToString()),
                ("status", status)
            };
            if (status is SecurityEventStatuses.Resolved or SecurityEventStatuses.FalsePositive)
                values.Add(("reviewNote", $"Reviewed as {status}"));

            using var response = await PostFormAsync(
                admin,
                "/Administration/UpdateSecurityEvent",
                adminToken,
                values.ToArray());
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var updated = await context.SecurityEventLogs
            .Where(item => eventIds.Contains(item.Id))
            .OrderBy(item => item.Id)
            .ToListAsync();

        for (var index = 0; index < SecurityEventStatuses.All.Count; index++)
        {
            Assert.Equal(SecurityEventStatuses.All.ElementAt(index), updated[index].Status);
            Assert.Equal(4, updated[index].ReviewedByUserId);
            Assert.NotNull(updated[index].ReviewedAtUtc);
        }

        Assert.Equal(SecurityEventStatuses.New, updated[^1].Status);
        Assert.Null(updated[^1].ReviewedByUserId);
        Assert.Equal(SecurityEventStatuses.All.Count,
            await context.AuditLogs.CountAsync(item =>
                item.EntityType == "SecurityEvent" && item.Action == "ReviewStatusChanged"));
    }

    [Fact]
    public async Task NetworkIdSubstitution_IsRejectedForTrustAndArchiveActions()
    {
        using var factory = new CustomWebApplicationFactory();
        var networkId = await SeedNetworkAsync(factory, userId: 1);
        using var admin = CreateClient(factory, 4, AppRoles.Admin);
        var token = await GetAntiforgeryTokenAsync(admin, "/Administration/UserNetworks?userId=1");

        using (var trust = await PostFormAsync(admin, "/Administration/SetNetworkTrust", token,
                   ("userId", "2"), ("networkId", networkId.ToString()), ("isTrusted", "true")))
        {
            Assert.Equal(HttpStatusCode.NotFound, trust.StatusCode);
        }

        using (var archive = await PostFormAsync(admin, "/Administration/ArchiveUserNetwork", token,
                   ("userId", "2"), ("networkId", networkId.ToString())))
        {
            Assert.Equal(HttpStatusCode.NotFound, archive.StatusCode);
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var unchanged = await context.UserLoginLocations.SingleAsync(item => item.Id == networkId);
        Assert.False(unchanged.IsTrusted);
        Assert.False(unchanged.IsArchived);
        Assert.DoesNotContain(await context.AuditLogs.ToListAsync(), item =>
            item.EntityType == "UserLoginLocation" && item.EntityId == networkId);
    }

    private static IEnumerable<(string ControllerName, MethodInfo Method)> GetAdministrativeActions() =>
        AdministrativeControllers.SelectMany(controllerType => controllerType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttribute<NonActionAttribute>(inherit: true) == null)
            .Where(method => typeof(IActionResult).IsAssignableFrom(method.ReturnType) ||
                             method.ReturnType == typeof(Task<IActionResult>))
            .Select(method => (controllerType.Name.Replace("Controller", string.Empty), method)));

    private static bool IsPost(MethodInfo method) =>
        method.GetCustomAttribute<HttpPostAttribute>(inherit: true) != null;

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

    private static async Task<long[]> SeedSecurityEventsAsync(
        CustomWebApplicationFactory factory,
        int count)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = DateTime.UtcNow;
        var entries = Enumerable.Range(0, count).Select(index => new SecurityEventLog
        {
            FirstOccurredAtUtc = now,
            LastOccurredAtUtc = now,
            EventType = SecurityEventTypes.AccessDenied,
            Severity = SecurityEventSeverities.Warning,
            Title = $"Coverage event {index}",
            IpAddress = "10.20.30.40",
            Status = SecurityEventStatuses.New
        }).ToArray();
        context.SecurityEventLogs.AddRange(entries);
        await context.SaveChangesAsync();
        return entries.Select(item => item.Id).ToArray();
    }

    private static async Task<long> SeedNetworkAsync(CustomWebApplicationFactory factory, int userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var location = new UserLoginLocation
        {
            UserId = userId,
            IpAddress = "8.8.8.8",
            NetworkAddress = "8.8.8.0",
            NetworkPrefixLength = 24,
            FirstSeenAtUtc = DateTime.UtcNow,
            LastSeenAtUtc = DateTime.UtcNow,
            SuccessfulLoginCount = 1
        };
        context.UserLoginLocations.Add(location);
        await context.SaveChangesAsync();
        return location.Id;
    }
}
