using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PersonalCabinetEducationProgram.Models;
using PersonalCabinetEducationProgram.Services;

namespace PersonalCabinetEducationProgram.Tests;

public sealed class AdministrationLoggingTests
{
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
}
