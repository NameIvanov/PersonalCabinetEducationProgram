using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalCabinetEducationProgram.Data;
using PersonalCabinetEducationProgram.Models;
using PersonalCabinetEducationProgram.Services;

namespace PersonalCabinetEducationProgram.Tests;

public sealed class IdorRiskAndIpBlockingTests
{
    [Fact]
    public async Task ExistingForeignObject_CreatesCriticalIdor_ButMissingObjectDoesNot()
    {
        using var factory = new CustomWebApplicationFactory();
        var foreignElementId = await SeedForeignElementAsync(factory);
        using var client = CreateClient(factory, 1, AppRoles.Manager);

        Assert.Equal(HttpStatusCode.OK,
            (await client.GetAsync("/ManagerHome/History?elementId=1")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.GetAsync($"/ManagerHome/History?elementId={foreignElementId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.GetAsync("/ManagerHome/History?elementId=999999")).StatusCode);

        await WaitUntilAsync(async () =>
        {
            await using var scope = factory.Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
                .SecurityEventLogs.AnyAsync(item =>
                    item.UserId == 1 && item.EventType == SecurityEventTypes.IdorAttempt);
        });

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var context = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var idorEvents = await context.SecurityEventLogs
            .Where(item => item.UserId == 1 && item.EventType == SecurityEventTypes.IdorAttempt)
            .ToListAsync();
        var idor = Assert.Single(idorEvents);
        Assert.Equal(SecurityEventSeverities.Critical, idor.Severity);
        Assert.Contains(foreignElementId.ToString(), idor.Description);
        Assert.DoesNotContain("999999", idor.Description);
    }

    [Fact]
    public async Task ThreeHttpIdorAttempts_AccumulateSixPoints_AndBlockAccount()
    {
        using var factory = new CustomWebApplicationFactory();
        var foreignElementId = await SeedForeignElementAsync(factory);
        using var client = CreateClient(factory, 1, AppRoles.Manager);

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var response = await client.GetAsync($"/ManagerHome/History?elementId={foreignElementId}");
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        await WaitUntilAsync(async () =>
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return (await context.Users.AsNoTracking().SingleAsync(item => item.Id == 1))
                .SecurityBlockedAtUtc.HasValue;
        });

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var idor = Assert.Single(await verificationContext.SecurityEventLogs
            .Where(item => item.UserId == 1 && item.EventType == SecurityEventTypes.IdorAttempt)
            .ToListAsync());
        Assert.Equal(3, idor.OccurrenceCount);
        Assert.True(verificationScope.ServiceProvider.GetRequiredService<SecurityBlockedAccountRegistry>().IsBlocked(1));
    }

    [Fact]
    public async Task SixRiskPoints_BlockOrdinaryUserIndefinitely()
    {
        using var factory = new CustomWebApplicationFactory();
        await SeedRiskEventsAsync(factory, userId: 1,
            (SecurityEventSeverities.Critical, SecurityEventStatuses.New, 2),
            (SecurityEventSeverities.High, SecurityEventStatuses.New, 2));

        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<AccountSecurityService>();
        var score = await service.EvaluateAccumulatedRiskAsync(1);

        Assert.Equal(6, score);
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.ChangeTracker.Clear();
        var user = await context.Users.SingleAsync(item => item.Id == 1);
        Assert.NotNull(user.SecurityBlockedAtUtc);
        Assert.True(user.LockoutEnd > DateTimeOffset.UtcNow.AddYears(90));
        Assert.Contains("6 баллов", user.SecurityBlockReason);
        Assert.True(scope.ServiceProvider.GetRequiredService<SecurityBlockedAccountRegistry>().IsBlocked(1));
    }

    [Fact]
    public async Task FalsePositiveIsSubtracted_AndAdministratorIsNeverAutomaticallyBlocked()
    {
        using var factory = new CustomWebApplicationFactory();
        await SeedRiskEventsAsync(factory, userId: 1,
            (SecurityEventSeverities.Critical, SecurityEventStatuses.New, 2),
            (SecurityEventSeverities.Critical, SecurityEventStatuses.FalsePositive, 1));
        await SeedRiskEventsAsync(factory, userId: 4,
            (SecurityEventSeverities.Critical, SecurityEventStatuses.New, 3));

        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<AccountSecurityService>();
        Assert.Equal(4, await service.EvaluateAccumulatedRiskAsync(1));
        Assert.Equal(6, await service.EvaluateAccumulatedRiskAsync(4));

        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.ChangeTracker.Clear();
        Assert.Null((await context.Users.SingleAsync(item => item.Id == 1)).SecurityBlockedAtUtc);
        Assert.Null((await context.Users.SingleAsync(item => item.Id == 4)).SecurityBlockedAtUtc);
        Assert.False(scope.ServiceProvider.GetRequiredService<SecurityBlockedAccountRegistry>().IsBlocked(4));
    }

    [Fact]
    public async Task ServerError_IsPersistedButExcludedFromAccountRisk_WhileAccessDeniedStillCounts()
    {
        using var factory = new CustomWebApplicationFactory();
        _ = factory.Services;
        const string ipAddress = "198.51.100.92";

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.SecurityEventLogs.Add(new SecurityEventLog
        {
            FirstOccurredAtUtc = DateTime.UtcNow,
            LastOccurredAtUtc = DateTime.UtcNow,
            EventType = SecurityEventTypes.ServerError,
            Severity = SecurityEventSeverities.Critical,
            Status = SecurityEventStatuses.New,
            Title = "Ошибка приложения",
            UserId = 1,
            IpAddress = ipAddress,
            OccurrenceCount = 3
        });
        context.SecurityEventLogs.Add(new SecurityEventLog
        {
            FirstOccurredAtUtc = DateTime.UtcNow,
            LastOccurredAtUtc = DateTime.UtcNow,
            EventType = SecurityEventTypes.AccessDenied,
            Severity = SecurityEventSeverities.High,
            Status = SecurityEventStatuses.New,
            Title = "Отказ в доступе",
            UserId = 1,
            IpAddress = ipAddress
        });
        await context.SaveChangesAsync();

        var score = await scope.ServiceProvider.GetRequiredService<AccountSecurityService>()
            .EvaluateAccumulatedRiskAsync(1);

        Assert.Equal(1, score);
        Assert.True(await context.SecurityEventLogs.AnyAsync(item =>
            item.EventType == SecurityEventTypes.ServerError && item.UserId == 1));
        context.ChangeTracker.Clear();
        Assert.Null((await context.Users.SingleAsync(item => item.Id == 1)).SecurityBlockedAtUtc);
    }

    [Fact]
    public async Task ServerError_IsExcludedFromAccumulatedIpRisk_WhileCriticalIdorStillCounts()
    {
        using var factory = new CustomWebApplicationFactory();
        _ = factory.Services;
        const string ipAddress = "198.51.100.93";

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        AddIpRiskEvent(context, ipAddress, userId: 1, SecurityEventSeverities.Critical,
            occurrenceCount: 3, eventType: SecurityEventTypes.ServerError);
        AddIpRiskEvent(context, ipAddress, userId: 2, SecurityEventSeverities.Critical,
            occurrenceCount: 1, eventType: SecurityEventTypes.IdorAttempt);
        await context.SaveChangesAsync();

        var score = await scope.ServiceProvider.GetRequiredService<IpAddressSecurityService>()
            .EvaluateAccumulatedAccountRiskAsync(ipAddress);

        Assert.Equal(2, score);
        context.ChangeTracker.Clear();
        var state = await context.IpAddressSecurityStates.SingleAsync(item => item.IpAddress == ipAddress);
        Assert.Equal(2, state.AccountRiskScore);
        Assert.False(state.IsBlocked);
    }

    [Fact]
    public async Task AnonymousProbeEscalation_IsThirtyMinutes_ThenDay_ThenPermanent()
    {
        using var factory = new CustomWebApplicationFactory();
        _ = factory.Services;
        const string ipAddress = "203.0.113.42";

        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IpAddressSecurityService>();
        for (var attempt = 0; attempt < 3; attempt++)
            await service.RecordAnonymousObjectProbeAsync(ipAddress, "EducationalProgramElement", 1);
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.ChangeTracker.Clear();
        var state = await context.IpAddressSecurityStates.SingleAsync(item => item.IpAddress == ipAddress);
        Assert.Equal(1, state.EscalationLevel);
        Assert.InRange(state.BlockedUntilUtc!.Value, DateTime.UtcNow.AddMinutes(29), DateTime.UtcNow.AddMinutes(31));

        for (var attempt = 0; attempt < 3; attempt++)
            await service.RecordAnonymousObjectProbeAsync(ipAddress, "EducationalProgramElement", 1);
        context.ChangeTracker.Clear();
        state = await context.IpAddressSecurityStates.SingleAsync(item => item.IpAddress == ipAddress);
        Assert.Equal(2, state.EscalationLevel);
        Assert.InRange(state.BlockedUntilUtc!.Value, DateTime.UtcNow.AddHours(23.9), DateTime.UtcNow.AddHours(24.1));

        await service.RecordAnonymousObjectProbeAsync(ipAddress, "EducationalProgramElement", 1);
        context.ChangeTracker.Clear();
        state = await context.IpAddressSecurityStates.SingleAsync(item => item.IpAddress == ipAddress);
        Assert.Equal(3, state.EscalationLevel);
        Assert.True(state.IsPermanentlyBlocked);
        Assert.Null(state.BlockedUntilUtc);
        Assert.True(scope.ServiceProvider.GetRequiredService<IpAddressBlockRegistry>()
            .IsBlocked(ipAddress, DateTime.UtcNow, out var snapshot));
        Assert.True(snapshot!.Permanent);
    }

    [Fact]
    public async Task AnonymousHttpObjectProbes_AreDetectedBeforeAuthorization_AndBlockIp()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.77");

        for (var attempt = 1; attempt <= 7; attempt++)
        {
            var response = await client.GetAsync("/ManagerHome/History?elementId=1");
            if (attempt >= 3)
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var state = await context.IpAddressSecurityStates.SingleAsync(item => item.IpAddress == "203.0.113.77");
        Assert.Equal(7, state.SuspiciousAttemptCount);
        Assert.Equal(3, state.EscalationLevel);
        Assert.True(state.IsPermanentlyBlocked);
    }

    [Fact]
    public async Task AdministratorCanBlockAndUnblockIp_FromAdministrationPage()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = CreateClient(factory, 4, AppRoles.Admin);
        const string ipAddress = "198.51.100.27";

        var page = await client.GetStringAsync("/Administration/IpAddresses");
        Assert.Contains("Все обращения", page);
        Assert.Contains("Подозрительные", page);
        Assert.Contains("Заблокированные", page);

        var token = AntiforgeryTokenExtractor.Extract(page);
        using var blockResponse = await client.PostAsync(
            "/Administration/BlockIpAddress",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["ipAddress"] = ipAddress,
                ["reviewNote"] = "Ручная проверка администратора"
            }));
        Assert.Equal(HttpStatusCode.Redirect, blockResponse.StatusCode);

        page = await client.GetStringAsync("/Administration/IpAddresses?category=blocked&Activity=all");
        Assert.Contains(ipAddress, page);
        token = AntiforgeryTokenExtractor.Extract(page);
        using var unblockResponse = await client.PostAsync(
            "/Administration/UnblockIpAddress",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["ipAddress"] = ipAddress,
                ["reviewNote"] = "Адрес проверен"
            }));
        Assert.Equal(HttpStatusCode.Redirect, unblockResponse.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var state = await context.IpAddressSecurityStates.SingleAsync(item => item.IpAddress == ipAddress);
        Assert.False(state.IsPermanentlyBlocked);
        Assert.Null(state.BlockedUntilUtc);
        Assert.Equal(0, state.EscalationLevel);
        Assert.Contains(await context.AuditLogs.ToListAsync(), item => item.Action == "IpBlocked");
        Assert.Contains(await context.AuditLogs.ToListAsync(), item => item.Action == "IpUnblocked");
    }

    [Fact]
    public async Task AccountRisk_IsSummedAcrossUsersOnSameIp_AndMarksItSuspiciousAtSixPoints()
    {
        using var factory = new CustomWebApplicationFactory();
        _ = factory.Services;
        const string ipAddress = "198.51.100.61";

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        AddIpRiskEvent(context, ipAddress, userId: 1, SecurityEventSeverities.High, occurrenceCount: 2);
        AddIpRiskEvent(context, ipAddress, userId: 2, SecurityEventSeverities.Critical, occurrenceCount: 2);
        await context.SaveChangesAsync();

        var service = scope.ServiceProvider.GetRequiredService<IpAddressSecurityService>();
        var score = await service.EvaluateAccumulatedAccountRiskAsync(ipAddress);

        context.ChangeTracker.Clear();
        var state = await context.IpAddressSecurityStates.SingleAsync(item => item.IpAddress == ipAddress);
        Assert.Equal(6, score);
        Assert.Equal(6, state.AccountRiskScore);
        Assert.NotNull(state.AccountRiskMarkedAtUtc);
        Assert.False(state.IsBlocked);
        Assert.Equal(0, state.AccountRiskEscalationLevel);

        var reviewedEvents = await context.SecurityEventLogs
            .Where(item => item.IpAddress == ipAddress)
            .ToListAsync();
        foreach (var entry in reviewedEvents)
            entry.Status = SecurityEventStatuses.FalsePositive;
        await context.SaveChangesAsync();
        Assert.Equal(0, await service.EvaluateAccumulatedAccountRiskAsync(ipAddress));
        context.ChangeTracker.Clear();
        state = await context.IpAddressSecurityStates.SingleAsync(item => item.IpAddress == ipAddress);
        Assert.Null(state.AccountRiskMarkedAtUtc);
        Assert.Equal(0, state.AccountRiskScore);
    }

    [Fact]
    public async Task BackgroundWriter_AutomaticallyCombinesAccountRiskByIp()
    {
        using var factory = new CustomWebApplicationFactory();
        _ = factory.Services;
        const string ipAddress = "198.51.100.64";
        var now = DateTime.UtcNow;
        var queue = factory.Services.GetRequiredService<SystemLogQueue>();

        Assert.True(queue.TryQueue(new SecurityEventLog
        {
            FirstOccurredAtUtc = now,
            LastOccurredAtUtc = now,
            EventType = "QueuedIpRiskTest",
            Severity = SecurityEventSeverities.High,
            Status = SecurityEventStatuses.New,
            Title = "Риск первого аккаунта",
            UserId = 1,
            IpAddress = ipAddress,
            OccurrenceCount = 2
        }));
        Assert.True(queue.TryQueue(new SecurityEventLog
        {
            FirstOccurredAtUtc = now,
            LastOccurredAtUtc = now,
            EventType = "QueuedIpRiskTest",
            Severity = SecurityEventSeverities.Critical,
            Status = SecurityEventStatuses.New,
            Title = "Риск второго аккаунта",
            UserId = 2,
            IpAddress = ipAddress,
            OccurrenceCount = 2
        }));

        await WaitUntilAsync(async () =>
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await context.IpAddressSecurityStates.AnyAsync(item =>
                item.IpAddress == ipAddress &&
                item.AccountRiskScore == 6 &&
                item.AccountRiskMarkedAtUtc != null);
        });
    }

    [Fact]
    public async Task FifteenIpRiskPoints_BlockForHour_ThenDay_ThenPermanently()
    {
        using var factory = new CustomWebApplicationFactory();
        _ = factory.Services;
        const string ipAddress = "198.51.100.62";

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IpAddressSecurityService>();

        AddIpRiskEvent(context, ipAddress, userId: 1, SecurityEventSeverities.High, occurrenceCount: 7);
        AddIpRiskEvent(context, ipAddress, userId: 2, SecurityEventSeverities.Critical, occurrenceCount: 4);
        await context.SaveChangesAsync();
        Assert.Equal(15, await service.EvaluateAccumulatedAccountRiskAsync(ipAddress));

        context.ChangeTracker.Clear();
        var state = await context.IpAddressSecurityStates.SingleAsync(item => item.IpAddress == ipAddress);
        Assert.Equal(1, state.AccountRiskEscalationLevel);
        Assert.InRange(state.BlockedUntilUtc!.Value, DateTime.UtcNow.AddMinutes(59), DateTime.UtcNow.AddMinutes(61));

        var secondWindow = state.AccountRiskWindowResetAtUtc!.Value.AddTicks(1);
        state.BlockedUntilUtc = DateTime.UtcNow.AddSeconds(-1);
        AddIpRiskEvent(context, ipAddress, userId: 3, SecurityEventSeverities.High, occurrenceCount: 15, occurredAtUtc: secondWindow);
        await context.SaveChangesAsync();
        Assert.Equal(15, await service.EvaluateAccumulatedAccountRiskAsync(ipAddress));

        context.ChangeTracker.Clear();
        state = await context.IpAddressSecurityStates.SingleAsync(item => item.IpAddress == ipAddress);
        Assert.Equal(2, state.AccountRiskEscalationLevel);
        Assert.InRange(state.BlockedUntilUtc!.Value, DateTime.UtcNow.AddHours(23.9), DateTime.UtcNow.AddHours(24.1));

        var thirdWindow = state.AccountRiskWindowResetAtUtc!.Value.AddTicks(1);
        state.BlockedUntilUtc = DateTime.UtcNow.AddSeconds(-1);
        AddIpRiskEvent(context, ipAddress, userId: 1, SecurityEventSeverities.Critical, occurrenceCount: 8, occurredAtUtc: thirdWindow);
        await context.SaveChangesAsync();
        Assert.Equal(16, await service.EvaluateAccumulatedAccountRiskAsync(ipAddress));

        context.ChangeTracker.Clear();
        state = await context.IpAddressSecurityStates.SingleAsync(item => item.IpAddress == ipAddress);
        Assert.Equal(3, state.AccountRiskEscalationLevel);
        Assert.True(state.IsPermanentlyBlocked);
        Assert.Null(state.BlockedUntilUtc);
    }

    [Fact]
    public async Task ClearingIpSuspicion_ResetsBothRiskSequences()
    {
        using var factory = new CustomWebApplicationFactory();
        _ = factory.Services;
        const string ipAddress = "198.51.100.63";

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.IpAddressSecurityStates.Add(new IpAddressSecurityState
        {
            IpAddress = ipAddress,
            FirstSeenAtUtc = DateTime.UtcNow,
            LastSeenAtUtc = DateTime.UtcNow,
            SuspiciousAttemptCount = 4,
            AttemptsInWindow = 2,
            EscalationLevel = 1,
            AccountRiskScore = 10,
            AccountRiskMarkedAtUtc = DateTime.UtcNow,
            AccountRiskEscalationLevel = 2,
            AccountRiskLastBlockedAtUtc = DateTime.UtcNow.AddDays(-1)
        });
        await context.SaveChangesAsync();

        var result = await scope.ServiceProvider.GetRequiredService<IpAddressSecurityService>()
            .ClearSuspicionAsync(ipAddress, administratorId: 4, "Проверено администратором");

        Assert.True(result.Succeeded);
        context.ChangeTracker.Clear();
        var state = await context.IpAddressSecurityStates.SingleAsync(item => item.IpAddress == ipAddress);
        Assert.False(state.IsSuspicious);
        Assert.Equal(0, state.SuspiciousAttemptCount);
        Assert.Equal(0, state.AttemptsInWindow);
        Assert.Equal(0, state.EscalationLevel);
        Assert.Equal(0, state.AccountRiskScore);
        Assert.Equal(0, state.AccountRiskEscalationLevel);
        Assert.Null(state.AccountRiskLastBlockedAtUtc);
        Assert.Contains(await context.AuditLogs.ToListAsync(), item => item.Action == "IpSuspicionCleared");
    }

    private static async Task<int> SeedForeignElementAsync(CustomWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var program = new EducationalProgram
        {
            CodeReferral = "FOREIGN",
            Name = "Чужая программа",
            EducationalLevel = "Бакалавриат",
            UserId = 2
        };
        var element = new EducationalProgramElement
        {
            EducationalProgram = program,
            TypeElement = "Main",
            Name = "Чужой элемент",
            Description = "Объект вне области доступа"
        };
        context.Add(element);
        await context.SaveChangesAsync();
        return element.Id;
    }

    private static async Task SeedRiskEventsAsync(
        CustomWebApplicationFactory factory,
        int userId,
        params (string Severity, string Status, int OccurrenceCount)[] events)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = DateTime.UtcNow;
        foreach (var item in events)
        {
            context.SecurityEventLogs.Add(new SecurityEventLog
            {
                FirstOccurredAtUtc = now,
                LastOccurredAtUtc = now,
                EventType = "RiskTest",
                Severity = item.Severity,
                Status = item.Status,
                Title = "Проверка накопительного риска",
                UserId = userId,
                IpAddress = "127.0.0.1",
                OccurrenceCount = item.OccurrenceCount
            });
        }
        await context.SaveChangesAsync();
    }

    private static void AddIpRiskEvent(
        ApplicationDbContext context,
        string ipAddress,
        int userId,
        string severity,
        int occurrenceCount,
        DateTime? occurredAtUtc = null,
        string eventType = "IpRiskTest")
    {
        var occurredAt = occurredAtUtc ?? DateTime.UtcNow;
        context.SecurityEventLogs.Add(new SecurityEventLog
        {
            FirstOccurredAtUtc = occurredAt,
            LastOccurredAtUtc = occurredAt,
            EventType = eventType,
            Severity = severity,
            Status = SecurityEventStatuses.New,
            Title = "Проверка суммарного риска IP",
            UserId = userId,
            IpAddress = ipAddress,
            OccurrenceCount = occurrenceCount
        });
    }

    private static HttpClient CreateClient(CustomWebApplicationFactory factory, int userId, string role)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Role", role);
        return client;
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
        Assert.True(await condition(), "Фоновая запись события безопасности не завершилась вовремя.");
    }
}

internal static class AntiforgeryTokenExtractor
{
    public static string Extract(string html)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        Assert.True(match.Success, "На странице отсутствует antiforgery-токен.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }
}
