using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PersonalCabinetEducationProgram.Controllers;
using PersonalCabinetEducationProgram.Data;
using PersonalCabinetEducationProgram.Models;
using PersonalCabinetEducationProgram.Services;

namespace PersonalCabinetEducationProgram.Tests;

public sealed class UnusualLoginMonitoringTests
{
    [Fact]
    public void IpNetworkService_NormalizesMappedIpv4AndUses24Prefix()
    {
        var service = new IpNetworkService();

        Assert.True(service.TryGetNetwork(IPAddress.Parse("::ffff:8.8.8.42"), out var network));
        Assert.Equal("8.8.8.42", network.IpAddress);
        Assert.Equal("8.8.8.0", network.NetworkAddress);
        Assert.Equal(24, network.PrefixLength);
        Assert.False(network.IsLocal);
    }

    [Fact]
    public void IpNetworkService_Uses64PrefixForIpv6AndMarksPrivateAddressesLocal()
    {
        var service = new IpNetworkService();

        Assert.True(service.TryGetNetwork(IPAddress.Parse("2001:4860:4860:12::8888"), out var ipv6));
        Assert.Equal("2001:4860:4860:12::", ipv6.NetworkAddress);
        Assert.Equal(64, ipv6.PrefixLength);
        Assert.False(ipv6.IsLocal);

        Assert.True(service.TryGetNetwork(IPAddress.Parse("192.168.10.25"), out var local));
        Assert.True(local.IsLocal);
    }

    [Fact]
    public async Task FirstSuccessfulLogin_CreatesBaselineWithoutSuspiciousEvent()
    {
        var geo = Geo(("8.8.8.8", Ru()));
        using var factory = CreateFactory(geo);
        using var scope = CreateScope(factory, "8.8.8.8");

        var result = await LoginAsync(scope, 1, "first-session");
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Assert.False(result.AccountBlocked);
        var location = Assert.Single(await db.UserLoginLocations.Where(item => item.UserId == 1).ToListAsync());
        Assert.Equal(1, location.SuccessfulLoginCount);
        Assert.DoesNotContain(await db.SecurityEventLogs.ToListAsync(), IsUnusualLoginEvent);
        var successfulLogin = Assert.Single(await db.SecurityEventLogs.Where(item =>
            item.UserId == 1 && item.EventType == SecurityEventTypes.LoginSucceeded).ToListAsync());
        Assert.Equal("8.8.8.0", successfulLogin.NetworkAddress);
        Assert.Equal(24, successfulLogin.NetworkPrefixLength);
        Assert.Equal("RU", successfulLogin.CountryCode);
        Assert.Contains(await db.AuditLogs.ToListAsync(), item => item.Action == "LoginSucceeded" && item.UserId == 1);
    }

    [Fact]
    public async Task DifferentIpv4InSame24_AndDifferentIpv6InSame64_AreKnownNetworks()
    {
        var geo = Geo(
            ("8.8.8.10", Ru()),
            ("8.8.8.99", Ru()),
            ("2001:4860:4860::8888", Ru()),
            ("2001:4860:4860::8844", Ru()));
        using var factory = CreateFactory(geo);
        using var scope = CreateScope(factory, "8.8.8.10");
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await LoginAsync(scope, 1, "ipv4-a");
        SetRemoteIp(scope, "8.8.8.99");
        await LoginAsync(scope, 1, "ipv4-b");
        Assert.Single(await db.UserLoginLocations.Where(item => item.UserId == 1).ToListAsync());
        Assert.DoesNotContain(await db.SecurityEventLogs.ToListAsync(), item => item.EventType == SecurityEventTypes.NewLoginNetwork);

        SetRemoteIp(scope, "2001:4860:4860::8888");
        await LoginAsync(scope, 2, "ipv6-a");
        SetRemoteIp(scope, "2001:4860:4860::8844");
        await LoginAsync(scope, 2, "ipv6-b");
        Assert.Single(await db.UserLoginLocations.Where(item => item.UserId == 2).ToListAsync());
    }

    [Fact]
    public async Task NewRussianNetwork_CreatesOneWarning_AndKnownOrTrustedNetworkDoesNotRepeatIt()
    {
        var geo = Geo(("8.8.8.8", Ru()), ("1.1.1.1", Ru()), ("1.1.1.25", Ru()));
        using var factory = CreateFactory(geo);
        using var scope = CreateScope(factory, "8.8.8.8");
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await LoginAsync(scope, 1, "ru-baseline");
        SetRemoteIp(scope, "1.1.1.1");
        await LoginAsync(scope, 1, "ru-new");

        var warning = Assert.Single(await db.SecurityEventLogs.Where(item =>
            item.UserId == 1 && item.EventType == SecurityEventTypes.NewLoginNetwork).ToListAsync());
        Assert.Equal(SecurityEventSeverities.Warning, warning.Severity);
        Assert.False((await db.Users.SingleAsync(item => item.Id == 1)).SecurityBlockedAtUtc.HasValue);

        var trusted = await db.UserLoginLocations.SingleAsync(item => item.UserId == 1 && item.NetworkAddress == "1.1.1.0");
        trusted.IsTrusted = true;
        await db.SaveChangesAsync();
        SetRemoteIp(scope, "1.1.1.25");
        await LoginAsync(scope, 1, "ru-known");
        Assert.Single(await db.SecurityEventLogs.Where(item =>
            item.UserId == 1 && item.EventType == SecurityEventTypes.NewLoginNetwork).ToListAsync());
    }

    [Fact]
    public async Task NewForeignNetwork_CreatesHighButDoesNotBlockByDefault()
    {
        var geo = Geo(("8.8.8.8", Ru()), ("9.9.9.9", Us()));
        using var factory = CreateFactory(geo);
        using var scope = CreateScope(factory, "8.8.8.8");
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await LoginAsync(scope, 1, "foreign-baseline");
        SetRemoteIp(scope, "9.9.9.9");
        var result = await LoginAsync(scope, 1, "foreign-new");

        var foreign = Assert.Single(await db.SecurityEventLogs.Where(item =>
            item.UserId == 1 && item.EventType == SecurityEventTypes.ForeignLogin).ToListAsync());
        Assert.Equal(SecurityEventSeverities.High, foreign.Severity);
        Assert.False(result.AccountBlocked);
        Assert.Null((await db.Users.SingleAsync(item => item.Id == 1)).SecurityBlockedAtUtc);
        Assert.Contains(await db.Notifications.ToListAsync(), item =>
            item.UserId == 1 && item.Type == NotificationType.Security && item.EducationalProgramElementId == null);
        Assert.Contains(await db.Notifications.ToListAsync(), item =>
            item.UserId == 4 && item.Type == NotificationType.Security);
    }

    [Fact]
    public async Task GeolocationFailure_CreatesInformationAndDoesNotBlock()
    {
        var geo = new StubGeolocationService(_ => throw new HttpRequestException("offline"));
        using var factory = CreateFactory(geo, blockForeign: true);
        using var scope = CreateScope(factory, "8.8.8.8");
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await LoginAsync(scope, 1, "unknown-baseline");
        SetRemoteIp(scope, "1.1.1.1");
        var result = await LoginAsync(scope, 1, "unknown-new");

        Assert.False(result.AccountBlocked);
        Assert.Null((await db.Users.SingleAsync(item => item.Id == 1)).SecurityBlockedAtUtc);
        Assert.Contains(await db.SecurityEventLogs.ToListAsync(), item =>
            item.EventType == SecurityEventTypes.LoginCountryUnknown &&
            item.Severity == SecurityEventSeverities.Information);
    }

    [Fact]
    public async Task ImpossibleTravel_UsesHaversineThresholds()
    {
        var moscow = new IpCountryLookup(true, true, "RU", "Russia", 55.7558, 37.6173);
        var newYork = new IpCountryLookup(true, true, "US", "United States", 40.7128, -74.0060);
        var geo = Geo(("8.8.8.8", moscow), ("9.9.9.9", newYork));
        using var factory = CreateFactory(geo);
        using var scope = CreateScope(factory, "8.8.8.8");
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await LoginAsync(scope, 1, "travel-baseline");
        var baseline = await db.UserLoginLocations.SingleAsync(item => item.UserId == 1);
        baseline.LastSeenAtUtc = DateTime.UtcNow.AddHours(-2);
        await db.SaveChangesAsync();
        SetRemoteIp(scope, "9.9.9.9");
        await LoginAsync(scope, 1, "travel-new");

        var travel = Assert.Single(await db.SecurityEventLogs.Where(item =>
            item.UserId == 1 && item.EventType == SecurityEventTypes.ImpossibleTravel).ToListAsync());
        Assert.Contains("расстояние", travel.Description);
        Assert.True(LoginSecurityService.CalculateDistanceKm(55.7558, 37.6173, 40.7128, -74.0060) > 500);
    }

    [Fact]
    public async Task ThreeNewNetworksWithinWindow_CreateOnlyOneFrequentChangeEvent()
    {
        var geo = Geo(
            ("8.8.8.8", Ru()),
            ("1.1.1.1", Ru()),
            ("9.9.9.9", Ru()),
            ("4.2.2.2", Ru()),
            ("208.67.222.222", Ru()));
        using var factory = CreateFactory(geo);
        using var scope = CreateScope(factory, "8.8.8.8");
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await LoginAsync(scope, 1, "frequent-baseline");
        foreach (var (ip, session) in new[]
                 {
                     ("1.1.1.1", "frequent-1"),
                     ("9.9.9.9", "frequent-2"),
                     ("4.2.2.2", "frequent-3"),
                     ("208.67.222.222", "frequent-4")
                 })
        {
            SetRemoteIp(scope, ip);
            await LoginAsync(scope, 1, session);
        }

        Assert.Single(await db.SecurityEventLogs.Where(item =>
            item.UserId == 1 && item.EventType == SecurityEventTypes.FrequentNetworkChanges).ToListAsync());
    }

    [Fact]
    public async Task NewNetworkAfterThreeFailedPasswords_CreatesHighEvent()
    {
        var geo = Geo(("8.8.8.8", Ru()), ("1.1.1.1", Ru()));
        using var factory = CreateFactory(geo);
        using var scope = CreateScope(factory, "8.8.8.8");
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<LoginSecurityService>();
        var user = await db.Users.SingleAsync(item => item.Id == 1);

        await service.RecordSuccessfulLoginAsync(user, "failed-baseline");
        SetRemoteIp(scope, "1.1.1.1");
        for (var attempt = 0; attempt < 3; attempt++)
            await service.RecordFailedLoginAsync(user, user.UserName!, locked: false);
        await service.RecordSuccessfulLoginAsync(user, "failed-success");

        var correlated = Assert.Single(await db.SecurityEventLogs.Where(item =>
            item.UserId == 1 && item.EventType == SecurityEventTypes.NewNetworkAfterFailedLogins).ToListAsync());
        Assert.Equal(SecurityEventSeverities.High, correlated.Severity);
        Assert.Contains("3", correlated.Description);
    }

    [Fact]
    public async Task ConcurrentActivityFromDifferentCountries_IsRecorded()
    {
        var geo = Geo(("8.8.8.8", Ru()), ("9.9.9.9", Us()));
        using var factory = CreateFactory(geo);
        using var scope = CreateScope(factory, "8.8.8.8");
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await LoginAsync(scope, 1, "concurrent-ru");
        SetRemoteIp(scope, "9.9.9.9");
        await LoginAsync(scope, 1, "concurrent-us");

        var concurrent = Assert.Single(await db.SecurityEventLogs.Where(item =>
            item.UserId == 1 && item.EventType == SecurityEventTypes.ConcurrentForeignSessions).ToListAsync());
        Assert.Equal(SecurityEventSeverities.High, concurrent.Severity);
    }

    [Fact]
    public async Task ForeignBlocking_BlocksOrdinaryUserButNeverAdministrator()
    {
        var geo = Geo(("8.8.8.8", Ru()), ("9.9.9.9", Us()));
        using var factory = CreateFactory(geo, blockForeign: true);
        using var scope = CreateScope(factory, "8.8.8.8", authenticated: false);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await LoginAsync(scope, 1, "blocked-user-baseline");
        SetRemoteIp(scope, "9.9.9.9");
        var blockedResult = await LoginAsync(scope, 1, "blocked-user-foreign");
        Assert.True(blockedResult.AccountBlocked);
        Assert.NotNull((await db.Users.SingleAsync(item => item.Id == 1)).SecurityBlockedAtUtc);
        Assert.DoesNotContain(await db.UserLoginSessions.Where(item => item.UserId == 1).ToListAsync(), item => item.IsActive);

        SetRemoteIp(scope, "8.8.8.8");
        await LoginAsync(scope, 4, "admin-baseline");
        SetRemoteIp(scope, "9.9.9.9");
        var administratorResult = await LoginAsync(scope, 4, "admin-foreign");
        Assert.False(administratorResult.AccountBlocked);
        Assert.Null((await db.Users.SingleAsync(item => item.Id == 4)).SecurityBlockedAtUtc);
        Assert.Contains(await db.UserLoginSessions.Where(item => item.UserId == 4).ToListAsync(), item => item.IsActive);
        Assert.Contains(await db.SecurityEventLogs.ToListAsync(), item =>
            item.UserId == 4 && item.EventType == SecurityEventTypes.ForeignLogin &&
            item.Severity == SecurityEventSeverities.Critical);
    }

    [Fact]
    public async Task AdminNetworkMutation_RejectsRecordOwnedByAnotherUser()
    {
        using var factory = CreateFactory(Geo(("8.8.8.8", Ru())));
        using var scope = CreateScope(factory, "127.0.0.1");
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var location = new UserLoginLocation
        {
            UserId = 1,
            IpAddress = "8.8.8.8",
            NetworkAddress = "8.8.8.0",
            NetworkPrefixLength = 24,
            FirstSeenAtUtc = DateTime.UtcNow,
            LastSeenAtUtc = DateTime.UtcNow,
            SuccessfulLoginCount = 1
        };
        db.UserLoginLocations.Add(location);
        await db.SaveChangesAsync();

        var controller = ActivatorUtilities.CreateInstance<AdministrationController>(scope.ServiceProvider);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext!
        };
        var result = await controller.SetNetworkTrust(2, location.Id, true, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        Assert.False(location.IsTrusted);
    }

    [Fact]
    public async Task LoginLocationData_SurvivesContextRecreation()
    {
        var databaseName = $"login-location-{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        await using (var first = new ApplicationDbContext(options))
        {
            await first.Database.EnsureCreatedAsync();
            first.UserLoginLocations.Add(new UserLoginLocation
            {
                UserId = 1,
                IpAddress = "8.8.8.8",
                NetworkAddress = "8.8.8.0",
                NetworkPrefixLength = 24,
                FirstSeenAtUtc = DateTime.UtcNow,
                LastSeenAtUtc = DateTime.UtcNow,
                SuccessfulLoginCount = 1
            });
            await first.SaveChangesAsync();
        }

        await using var second = new ApplicationDbContext(options);
        Assert.Contains(await second.UserLoginLocations.ToListAsync(), item =>
            item.UserId == 1 && item.NetworkAddress == "8.8.8.0");
    }

    private static CustomWebApplicationFactory CreateFactory(
        IIpGeolocationService geolocation,
        bool blockForeign = false) =>
        new(services =>
        {
            services.RemoveAll<IIpGeolocationService>();
            services.AddSingleton(geolocation);
            services.PostConfigure<SecurityMonitoringOptions>(options =>
            {
                options.BlockNewForeignLogin = blockForeign;
                options.NewNetworksWarningCount = 3;
                options.NewNetworksWindowHours = 24;
                options.FailedLoginCorrelationCount = 3;
                options.FailedLoginCorrelationMinutes = 15;
            });
        });

    private static IServiceScope CreateScope(
        CustomWebApplicationFactory factory,
        string ipAddress,
        bool authenticated = true)
    {
        _ = factory.Services;
        var scope = factory.Services.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Connection.RemoteIpAddress = IPAddress.Parse(ipAddress);
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/Account/Login";
        if (authenticated)
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "4"),
                new Claim(ClaimTypes.Name, "Администратор"),
                new Claim(ClaimTypes.Role, AppRoles.Admin),
                new Claim("Username", "admin")
            ], "Test"));
        }
        accessor.HttpContext = context;
        return scope;
    }

    private static void SetRemoteIp(IServiceScope scope, string ipAddress) =>
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>()
            .HttpContext!.Connection.RemoteIpAddress = IPAddress.Parse(ipAddress);

    private static async Task<LoginSecurityResult> LoginAsync(
        IServiceScope scope,
        int userId,
        string sessionId)
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.SingleAsync(item => item.Id == userId);
        return await scope.ServiceProvider.GetRequiredService<LoginSecurityService>()
            .RecordSuccessfulLoginAsync(user, sessionId);
    }

    private static StubGeolocationService Geo(params (string Ip, IpCountryLookup Result)[] values) =>
        new(address => values.FirstOrDefault(item => item.Ip == address.ToString()).Result ?? Ru());

    private static IpCountryLookup Ru() => new(true, true, "RU", "Russia", 55.7558, 37.6173);
    private static IpCountryLookup Us() => new(true, true, "US", "United States", 40.7128, -74.0060);

    private static bool IsUnusualLoginEvent(SecurityEventLog item) => item.EventType is
        SecurityEventTypes.NewLoginNetwork or
        SecurityEventTypes.ForeignLogin or
        SecurityEventTypes.LoginCountryUnknown or
        SecurityEventTypes.ImpossibleTravel or
        SecurityEventTypes.FrequentNetworkChanges or
        SecurityEventTypes.NewNetworkAfterFailedLogins or
        SecurityEventTypes.ConcurrentForeignSessions;

    private sealed class StubGeolocationService : IIpGeolocationService
    {
        private readonly Func<IPAddress, IpCountryLookup> _lookup;

        public StubGeolocationService(Func<IPAddress, IpCountryLookup> lookup)
        {
            _lookup = lookup;
        }

        public Task<IpCountryLookup> LookupAsync(
            IPAddress? ipAddress,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_lookup(ipAddress ?? IPAddress.None));
    }
}
