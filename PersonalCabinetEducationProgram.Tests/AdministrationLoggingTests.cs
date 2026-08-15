using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PersonalCabinetEducationProgram.Data;
using PersonalCabinetEducationProgram.Models;
using PersonalCabinetEducationProgram.Services;

namespace PersonalCabinetEducationProgram.Tests;

public sealed class AdministrationLoggingTests
{
    [Fact]
    public async Task Pipeline_LogsStaticFilesAndBlockedUploadsAsServerRequests()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Add("X-Test-UserId", "4");
        client.DefaultRequestHeaders.Add("X-Test-Role", AppRoles.Admin);

        using var staticFile = await client.GetAsync("/css/site.css");
        using var blockedUpload = await client.GetAsync("/uploads/probe.pdf");

        Assert.Equal(HttpStatusCode.OK, staticFile.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, blockedUpload.StatusCode);

        await WaitUntilAsync(async () =>
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await context.SystemRequestLogs.CountAsync(log =>
                log.Path == "/css/site.css" || log.Path == "/uploads/probe.pdf") == 2;
        });

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logs = await verificationContext.SystemRequestLogs
            .Where(log => log.Path == "/css/site.css" || log.Path == "/uploads/probe.pdf")
            .OrderBy(log => log.Path)
            .ToListAsync();
        Assert.All(logs, log => Assert.Equal(4, log.UserId));
        Assert.Contains(logs, log => log.Path == "/css/site.css" && log.StatusCode == 200);
        Assert.Contains(logs, log => log.Path == "/uploads/probe.pdf" && log.StatusCode == 404);
    }

    [Theory]
    [InlineData(200, SystemRequestResults.Success, null)]
    [InlineData(302, SystemRequestResults.Redirect, null)]
    [InlineData(400, SystemRequestResults.ClientError, SecurityEventTypes.InvalidRequest)]
    [InlineData(401, SystemRequestResults.ClientError, SecurityEventTypes.Unauthorized)]
    [InlineData(403, SystemRequestResults.ClientError, SecurityEventTypes.AccessDenied)]
    [InlineData(404, SystemRequestResults.ClientError, null)]
    [InlineData(429, SystemRequestResults.ClientError, SecurityEventTypes.RateLimitExceeded)]
    [InlineData(500, SystemRequestResults.ServerError, SecurityEventTypes.ServerError)]
    public async Task Middleware_LogsEveryResponseClass_AndRaisesExpectedSecurityEvent(
        int statusCode,
        string expectedResult,
        string? expectedSecurityEvent)
    {
        var queue = new SystemLogQueue();
        var middleware = new RequestLoggingMiddleware(
            context =>
            {
                context.Response.StatusCode = statusCode;
                return Task.CompletedTask;
            },
            queue,
            new RequestActivityTracker(TimeProvider.System),
            NullLogger<RequestLoggingMiddleware>.Instance);
        var context = CreateContext("/security/status", null, statusCode);
        if (statusCode == StatusCodes.Status400BadRequest)
            context.Request.Method = HttpMethods.Post;

        await middleware.InvokeAsync(context);

        Assert.True(queue.Requests.TryRead(out var request));
        Assert.NotNull(request);
        Assert.Equal(statusCode, request!.StatusCode);
        Assert.Equal(expectedResult, request.Result);
        Assert.False(queue.Requests.TryRead(out _));

        if (expectedSecurityEvent == null)
        {
            Assert.False(queue.SecurityEvents.TryRead(out _));
        }
        else
        {
            Assert.True(queue.SecurityEvents.TryRead(out var securityEvent));
            Assert.Equal(expectedSecurityEvent, securityEvent!.EventType);
            Assert.False(queue.SecurityEvents.TryRead(out _));
        }
    }

    [Fact]
    public async Task Middleware_LogsUnhandledExceptionAsServerError_AndRethrowsIt()
    {
        var queue = new SystemLogQueue();
        var middleware = new RequestLoggingMiddleware(
            _ => throw new InvalidOperationException("simulated failure"),
            queue,
            new RequestActivityTracker(TimeProvider.System),
            NullLogger<RequestLoggingMiddleware>.Instance);
        var context = CreateContext("/security/failure", null, StatusCodes.Status200OK);

        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));

        Assert.True(queue.Requests.TryRead(out var request));
        Assert.Equal(StatusCodes.Status500InternalServerError, request!.StatusCode);
        Assert.Equal(SystemRequestResults.ServerError, request.Result);
        Assert.Equal(nameof(InvalidOperationException), request.ErrorType);
        Assert.True(queue.SecurityEvents.TryRead(out var securityEvent));
        Assert.Equal(SecurityEventTypes.ServerError, securityEvent!.EventType);
        Assert.Null(securityEvent.UserId);
        Assert.Null(securityEvent.UserLogin);
        Assert.Null(securityEvent.UserFullName);
    }

    [Fact]
    public async Task Middleware_RecordsUserIpAndMasksSensitiveQueryValues()
    {
        var queue = new SystemLogQueue();
        var tracker = new RequestActivityTracker(TimeProvider.System);
        var middleware = new RequestLoggingMiddleware(
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
                return Task.CompletedTask;
            },
            queue,
            tracker,
            NullLogger<RequestLoggingMiddleware>.Instance);
        var context = CreateContext("/Administration/Logs", "token=top-secret&filter=Иванов", StatusCodes.Status200OK);

        await middleware.InvokeAsync(context);

        Assert.True(queue.Requests.TryRead(out var entry));
        Assert.NotNull(entry);
        Assert.Equal(4, entry!.UserId);
        Assert.Equal("10.20.30.40", entry.IpAddress);
        Assert.Contains("%5BREDACTED%5D", entry.QueryString);
        Assert.DoesNotContain("top-secret", entry.QueryString);
        Assert.Contains("filter=", entry.QueryString);
        Assert.Equal(SystemRequestResults.Success, entry.Result);
    }

    [Fact]
    public async Task Middleware_CreatesSecurityEventForForbiddenRequest()
    {
        var queue = new SystemLogQueue();
        var middleware = new RequestLoggingMiddleware(
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            },
            queue,
            new RequestActivityTracker(TimeProvider.System),
            NullLogger<RequestLoggingMiddleware>.Instance);
        var context = CreateContext("/Admin/Users", null, StatusCodes.Status403Forbidden);

        await middleware.InvokeAsync(context);

        Assert.True(queue.SecurityEvents.TryRead(out var securityEvent));
        Assert.NotNull(securityEvent);
        Assert.Equal(SecurityEventTypes.AccessDenied, securityEvent!.EventType);
        Assert.Equal(SecurityEventSeverities.High, securityEvent.Severity);
        Assert.Equal(4, securityEvent.UserId);
        Assert.Equal("10.20.30.40", securityEvent.IpAddress);
    }

    [Fact]
    public async Task InvalidFileUpload_CreatesSecurityEventWithoutReadingItsBody()
    {
        var queue = new SystemLogQueue();
        var accessor = new HttpContextAccessor { HttpContext = CreateContext("/ManagerHome/Upload", null, 200) };
        var securityEvents = new SecurityEventService(
            queue,
            accessor,
            NullLogger<SecurityEventService>.Instance);
        var storage = new FileSystemStorageService(
            Options.Create(new FileStorageSettings { StoragePath = Path.GetTempPath() }),
            securityEvents);
        await using var stream = new MemoryStream("not an executable"u8.ToArray());
        var file = new FormFile(stream, 0, stream.Length, "file", "payload.exe");

        await Assert.ThrowsAsync<InvalidOperationException>(() => storage.ValidateFileAsync(file));

        Assert.True(queue.SecurityEvents.TryRead(out var securityEvent));
        Assert.NotNull(securityEvent);
        Assert.Equal(SecurityEventTypes.InvalidFileUpload, securityEvent!.EventType);
        Assert.Contains("payload.exe", securityEvent.Description);
        Assert.DoesNotContain("not an executable", securityEvent.Description);
    }

    [Fact]
    public void InformationalSecurityEvent_IsRecordedAsAlreadyHandled()
    {
        var queue = new SystemLogQueue();
        var service = new SecurityEventService(
            queue,
            new HttpContextAccessor { HttpContext = CreateContext("/Account/Login", null, 200) },
            NullLogger<SecurityEventService>.Instance);

        service.Record(SecurityEventTypes.LoginSucceeded, SecurityEventSeverities.Information, "Успешный вход");

        Assert.True(queue.SecurityEvents.TryRead(out var securityEvent));
        Assert.Equal(SecurityEventStatuses.Resolved, securityEvent!.Status);
        Assert.NotNull(securityEvent.ReviewedAtUtc);
    }

    private static DefaultHttpContext CreateContext(string path, string? query, int statusCode)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Method = HttpMethods.Get;
        context.Response.StatusCode = statusCode;
        if (query != null)
            context.Request.QueryString = new QueryString("?" + query);
        context.Connection.RemoteIpAddress = IPAddress.Parse("10.20.30.40");
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "4"),
            new Claim(ClaimTypes.Name, "Козлова Мария Ивановна"),
            new Claim(ClaimTypes.Role, AppRoles.Admin),
            new Claim("Username", "admin")
        ], "Test"));
        return context;
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

        Assert.True(await condition(), "The request log writer did not persist the expected entries.");
    }
}
