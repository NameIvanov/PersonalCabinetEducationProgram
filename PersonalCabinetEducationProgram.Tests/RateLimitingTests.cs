using System.Net;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using PersonalCabinetEducationProgram.Controllers;
using PersonalCabinetEducationProgram.Services;

namespace PersonalCabinetEducationProgram.Tests;

public class RateLimitingTests
{
    [Fact]
    public void EveryPostActionHasRateLimitPolicy()
    {
        var controllerTypes = typeof(AccountController).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(Controller).IsAssignableFrom(type));
        var postActions = controllerTypes
            .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            .Where(method => method.GetCustomAttributes<HttpPostAttribute>(inherit: true).Any())
            .ToList();

        var unprotected = postActions
            .Where(method => method.GetCustomAttribute<AppRateLimitAttribute>(inherit: true) == null)
            .Select(method => $"{method.DeclaringType!.Name}.{method.Name}")
            .OrderBy(name => name)
            .ToList();

        Assert.True(unprotected.Count == 0, $"POST actions without rate limiting: {string.Join(", ", unprotected)}");
    }

    [Fact]
    public void EveryAssignedPolicyExists()
    {
        var attributes = typeof(AccountController).Assembly.GetTypes()
            .Where(type => typeof(Controller).IsAssignableFrom(type))
            .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            .Select(method => method.GetCustomAttribute<AppRateLimitAttribute>(inherit: true))
            .Where(attribute => attribute != null)
            .Cast<AppRateLimitAttribute>();

        var missing = attributes
            .Select(attribute => attribute.PolicyName)
            .Distinct(StringComparer.Ordinal)
            .Where(policyName => !AppRateLimitPolicies.Rules.ContainsKey(policyName))
            .ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public void EveryPhysicalDownloadAction_UsesPostToProtectSecurityCountersFromCsrf()
    {
        var downloadActions = typeof(AccountController).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(Controller).IsAssignableFrom(type))
            .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            .Where(method => method.Name is "Download" or "DownloadElement")
            .ToList();

        var unsafeActions = downloadActions
            .Where(method => method.GetCustomAttribute<HttpPostAttribute>(inherit: true) == null)
            .Select(method => $"{method.DeclaringType!.Name}.{method.Name}")
            .OrderBy(name => name)
            .ToList();

        Assert.True(unsafeActions.Count == 0,
            $"Physical download actions callable by GET: {string.Join(", ", unsafeActions)}");
    }

    [Fact]
    public async Task LoginPolicyRejectsEleventhRequestFromSameIp()
    {
        var limiter = CreateLimiter();
        var context = CreateContext(AppRateLimitPolicies.Login, "10.0.0.1");

        for (var attempt = 0; attempt < 10; attempt++)
        {
            using var lease = limiter.AttemptAcquire(context);
            Assert.True(lease.IsAcquired);
        }

        using var rejectedLease = limiter.AttemptAcquire(context);
        Assert.False(rejectedLease.IsAcquired);
        await limiter.DisposeAsync();
    }

    [Fact]
    public async Task PlxApplyConcurrencyIsSharedByProgram()
    {
        var limiter = CreateLimiter();
        var firstUser = CreateContext(AppRateLimitPolicies.PlxApply, "10.0.0.1", "1", 15);
        var secondUser = CreateContext(AppRateLimitPolicies.PlxApply, "10.0.0.2", "2", 15);
        var otherProgram = CreateContext(AppRateLimitPolicies.PlxApply, "10.0.0.2", "2", 16);

        using var activeLease = limiter.AttemptAcquire(firstUser);
        Assert.True(activeLease.IsAcquired);

        using var rejectedLease = limiter.AttemptAcquire(secondUser);
        Assert.False(rejectedLease.IsAcquired);

        using var otherProgramLease = limiter.AttemptAcquire(otherProgram);
        Assert.True(otherProgramLease.IsAcquired);
        await limiter.DisposeAsync();
    }

    [Fact]
    public void DownloadQuotaRejectsBytesAboveHourlyAllowance()
    {
        var service = new DownloadQuotaService(
            Options.Create(new DownloadQuotaOptions
            {
                MaxBytesPerWindow = 100,
                Window = TimeSpan.FromHours(1)
            }),
            TimeProvider.System);

        Assert.True(service.TryConsume("user:1", 60, out _));
        Assert.False(service.TryConsume("user:1", 50, out var retryAfter));
        Assert.True(retryAfter > TimeSpan.Zero);
        Assert.True(service.TryConsume("user:1", 40, out _));
        Assert.True(service.TryConsume("user:2", 100, out _));
    }

    private static System.Threading.RateLimiting.PartitionedRateLimiter<HttpContext> CreateLimiter()
    {
        var options = new RateLimiterOptions();
        AppRateLimiterConfiguration.Configure(options);
        return options.GlobalLimiter!;
    }

    private static DefaultHttpContext CreateContext(
        string policyName,
        string ipAddress,
        string? userId = null,
        int? programId = null)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(ipAddress);
        if (!string.IsNullOrWhiteSpace(userId))
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId)],
                "Test"));
        }

        if (programId.HasValue)
            context.Request.QueryString = QueryString.Create("programId", programId.Value.ToString());

        context.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new AppRateLimitAttribute(policyName)),
            policyName));
        return context;
    }
}
