using System.Net;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Http;
using PersonalCabinetEducationProgram.Models;
using PersonalCabinetEducationProgram.Services;

namespace PersonalCabinetEducationProgram.Tests;

public sealed class SecurityMonitoringTests
{
    [Fact]
    public void DownloadResponses_DisableBrowserCaching()
    {
        var context = new DefaultHttpContext();

        DownloadQuotaFilter.PreventDownloadCaching(context.Response);

        Assert.Contains("no-store", context.Response.Headers.CacheControl.ToString());
        Assert.Equal("no-cache", context.Response.Headers.Pragma.ToString());
        Assert.Equal("0", context.Response.Headers.Expires.ToString());
    }

    [Fact]
    public void RequestMonitor_WarnsForUserAfterOneHundredRequestsPerMinute()
    {
        var options = CreateOptions();
        var monitor = new SuspiciousActivityMonitor(options, TimeProvider.System);
        IReadOnlyList<RequestRateSignal> signals = [];

        for (var i = 0; i < 101; i++)
            signals = monitor.RecordRequest("10.20.30.40", userId: 7);

        var signal = Assert.Single(signals);
        Assert.Equal(SecurityEventTypes.SuspiciousRequestVolume, signal.EventType);
        Assert.Contains("101", signal.Description);
        Assert.Contains("ID 7", signal.Description);
    }

    [Fact]
    public void RequestMonitor_WarnsForAnonymousIpAtConfiguredThreshold()
    {
        var settings = CreateSettings();
        settings.AnonymousIpRequestWarningPerMinute = 2;
        settings.AnonymousIpRequestWarningPerHour = 0;
        var monitor = new SuspiciousActivityMonitor(Options.Create(settings), TimeProvider.System);

        monitor.RecordRequest("203.0.113.10", userId: null);
        monitor.RecordRequest("203.0.113.10", userId: null);
        var signals = monitor.RecordRequest("203.0.113.10", userId: null);

        var signal = Assert.Single(signals);
        Assert.Contains("203.0.113.10", signal.Description);
        Assert.Contains("3", signal.Description);
    }

    [Fact]
    public void RequestMonitor_UsesHourlyWindowAcrossMinutes()
    {
        var settings = CreateSettings();
        settings.UserRequestWarningPerMinute = 0;
        settings.UserRequestWarningPerHour = 3;
        settings.AuthenticatedIpRequestWarningPerMinute = 0;
        settings.AuthenticatedIpRequestWarningPerHour = 0;
        var time = new MutableTimeProvider(DateTimeOffset.Parse("2026-08-12T10:00:00Z"));
        var monitor = new SuspiciousActivityMonitor(Options.Create(settings), time);

        monitor.RecordRequest("203.0.113.10", userId: 7);
        monitor.RecordRequest("203.0.113.10", userId: 7);
        time.Advance(TimeSpan.FromMinutes(1));
        monitor.RecordRequest("203.0.113.10", userId: 7);
        var signals = monitor.RecordRequest("203.0.113.10", userId: 7);

        var signal = Assert.Single(signals);
        Assert.Contains("4", signal.Description);
        Assert.Contains("час", signal.Description);
    }

    [Fact]
    public void RequestMonitor_SeparatelyTracksAuthenticatedIpThreshold()
    {
        var settings = CreateSettings();
        settings.UserRequestWarningPerMinute = 0;
        settings.UserRequestWarningPerHour = 0;
        settings.AuthenticatedIpRequestWarningPerMinute = 2;
        settings.AuthenticatedIpRequestWarningPerHour = 0;
        var monitor = new SuspiciousActivityMonitor(Options.Create(settings), TimeProvider.System);

        monitor.RecordRequest("203.0.113.20", userId: 1);
        monitor.RecordRequest("203.0.113.20", userId: 2);
        var signals = monitor.RecordRequest("203.0.113.20", userId: 3);

        var signal = Assert.Single(signals);
        Assert.Contains("203.0.113.20", signal.Description);
        Assert.Contains("3", signal.Description);
    }

    [Fact]
    public void DownloadMonitor_WarnsAtElevenAndBlocksAtTwentyOne()
    {
        var monitor = new SuspiciousActivityMonitor(CreateOptions(), TimeProvider.System);
        DownloadRateObservation observation = new(0, false, false);

        for (var i = 0; i < 11; i++)
            observation = monitor.RecordDownload(9);

        Assert.Equal(11, observation.Count);
        Assert.True(observation.ShouldWarn);
        Assert.False(observation.ShouldBlock);

        for (var i = 11; i < 21; i++)
            observation = monitor.RecordDownload(9);

        Assert.Equal(21, observation.Count);
        Assert.False(observation.ShouldWarn);
        Assert.True(observation.ShouldBlock);
    }

    [Fact]
    public void LargeDocumentGroupAndPlxThresholds_CreateExpectedSeverities()
    {
        using var factory = new CustomWebApplicationFactory(services =>
        {
            services.RemoveAll<IHostedService>();
            services.PostConfigure<SecurityMonitoringOptions>(settings =>
            {
                settings.LargeDocumentWarningBytes = 100;
                settings.LargeDocumentHighRiskBytes = 150;
                settings.LargeDocumentGroupWarningBytes = 300;
                settings.LargePlxWarningBytes = 50;
                settings.LargePlxHighRiskBytes = 80;
            });
        });
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<AccountSecurityService>();

        service.RecordDocumentUpload([File("warning.pdf", 100)]);
        service.RecordDocumentUpload([File("high.pdf", 150)]);
        service.RecordDocumentUpload([
            File("group-a.pdf", 100),
            File("group-b.pdf", 100),
            File("group-c.pdf", 100)
        ]);
        service.RecordPlxUpload(File("warning.plx", 50));
        service.RecordPlxUpload(File("high.plx", 80));

        var queue = scope.ServiceProvider.GetRequiredService<SystemLogQueue>();
        var events = new List<SecurityEventLog>();
        while (queue.SecurityEvents.TryRead(out var securityEvent))
            events.Add(securityEvent);

        Assert.Equal(8, events.Count);
        Assert.All(events, item => Assert.Equal(SecurityEventTypes.LargeFileUpload, item.EventType));
        Assert.Equal(5, events.Count(item => item.Severity == SecurityEventSeverities.Warning));
        Assert.Equal(3, events.Count(item => item.Severity == SecurityEventSeverities.High));
        Assert.Contains(events, item => item.Description!.Contains("group-c.pdf"));
        Assert.Contains(events, item => item.Description!.Contains("high.plx"));
    }

    [Fact]
    public async Task IpGeolocation_UsesCacheAndReturnsCountryForPublicAddress()
    {
        var handler = new StubHttpMessageHandler(
            "{\"success\":true,\"country\":\"United States\",\"country_code\":\"US\"}");
        using var httpClient = new HttpClient(handler);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new IpGeolocationService(
            httpClient,
            cache,
            CreateOptions(),
            NullLogger<IpGeolocationService>.Instance);

        var first = await service.LookupAsync(IPAddress.Parse("8.8.8.8"));
        var second = await service.LookupAsync(IPAddress.Parse("8.8.8.8"));

        Assert.True(first.IsPublicAddress);
        Assert.True(first.WasResolved);
        Assert.Equal("US", first.CountryCode);
        Assert.Equal("United States", first.CountryName);
        Assert.Equal(first, second);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task IpGeolocation_DoesNotSendPrivateAddressToExternalService()
    {
        var handler = new StubHttpMessageHandler(
            "{\"success\":true,\"country\":\"United States\",\"country_code\":\"US\"}");
        using var httpClient = new HttpClient(handler);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new IpGeolocationService(
            httpClient,
            cache,
            CreateOptions(),
            NullLogger<IpGeolocationService>.Instance);

        var result = await service.LookupAsync(IPAddress.Parse("192.168.1.10"));

        Assert.False(result.IsPublicAddress);
        Assert.False(result.WasResolved);
        Assert.Equal(0, handler.RequestCount);
    }

    private static IOptions<SecurityMonitoringOptions> CreateOptions() =>
        Options.Create(CreateSettings());

    private static SecurityMonitoringOptions CreateSettings() => new()
    {
        InvalidFileBlockThreshold = 5,
        DownloadWarningThresholdPerMinute = 10,
        DownloadBlockThresholdPerMinute = 20,
        UserRequestWarningPerMinute = 100,
        UserRequestWarningPerHour = 1000,
        AnonymousIpRequestWarningPerMinute = 25,
        AnonymousIpRequestWarningPerHour = 200,
        AuthenticatedIpRequestWarningPerMinute = 300,
        AuthenticatedIpRequestWarningPerHour = 5000,
        IpGeolocation = new IpGeolocationOptions
        {
            Enabled = true,
            AllowedCountryCode = "RU",
            EndpointTemplate = "https://ipwho.is/{0}",
            TimeoutMilliseconds = 2000,
            CacheHours = 24,
            FailureCacheMinutes = 15
        }
    };

    private static IFormFile File(string fileName, long length) =>
        new FormFile(Stream.Null, 0, length, "file", fileName);

    private sealed class MutableTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public MutableTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan amount) => _utcNow += amount;
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _json;

        public StubHttpMessageHandler(string json)
        {
            _json = json;
        }

        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_json, Encoding.UTF8, "application/json")
            });
        }
    }
}
