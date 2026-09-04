using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PersonalCabinetEducationProgram.Data;
using PersonalCabinetEducationProgram.Models;
using PersonalCabinetEducationProgram.Services;

namespace PersonalCabinetEducationProgram.Tests;

public sealed class AdminAiAssistantTests
{
    [Fact]
    public async Task AssistantPage_IsAvailableToAdmin_AndShowsConfigurationNoticeWhenKeyMissing()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-UserId", "4");
        client.DefaultRequestHeaders.Add("X-Test-Role", AppRoles.Admin);

        var response = await client.GetAsync("/Admin/AiAssistant");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("ИИ-помощник пока не настроен", html);
    }

    [Fact]
    public async Task AssistantPage_IsNotAvailableToNonAdmin()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-UserId", "1");
        client.DefaultRequestHeaders.Add("X-Test-Role", AppRoles.Manager);

        var response = await client.GetAsync("/Admin/AiAssistant");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AssistantApi_RejectsPostWithoutAntiforgeryToken()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-UserId", "4");
        client.DefaultRequestHeaders.Add("X-Test-Role", AppRoles.Admin);

        var response = await client.PostAsJsonAsync("/Admin/AiAssistant/Ask", new { question = "Проверка" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SafeContext_ExcludesSecretsPersonalDataAndDoesNotBlameUserForServerError()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"ai-context-{Guid.NewGuid():N}").Options;
        await using var context = new ApplicationDbContext(options);
        context.SecurityEventLogs.AddRange(
            new SecurityEventLog
            {
                LastOccurredAtUtc = DateTime.UtcNow,
                FirstOccurredAtUtc = DateTime.UtcNow,
                Severity = SecurityEventSeverities.Critical,
                EventType = SecurityEventTypes.ServerError,
                Title = "Ошибка базы данных",
                Description = "password=very-secret; SELECT * FROM users",
                UserLogin = "private-login",
                UserFullName = "Иванов Иван",
                Path = "/Admin/Users?token=secret"
            },
            new SecurityEventLog
            {
                LastOccurredAtUtc = DateTime.UtcNow,
                FirstOccurredAtUtc = DateTime.UtcNow,
                Severity = SecurityEventSeverities.High,
                EventType = SecurityEventTypes.AccessDenied,
                Title = "Отказ в доступе",
                Description = "email=test@example.org token=abc",
                Path = "/Admin/Users"
            });
        context.SystemRequestLogs.Add(new SystemRequestLog
        {
            OccurredAtUtc = DateTime.UtcNow,
            Path = "/Admin/Users",
            HttpMethod = "GET",
            StatusCode = 500,
            Result = SystemRequestResults.ServerError,
            IpAddress = "192.0.2.10",
            ErrorMessage = "connection string should never be copied"
        });
        await context.SaveChangesAsync();

        var summary = await new AdminAiContextService(context).BuildSummaryAsync();

        Assert.DoesNotContain("very-secret", summary);
        Assert.DoesNotContain("test@example.org", summary);
        Assert.DoesNotContain("private-login", summary);
        Assert.DoesNotContain("SELECT", summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("не является виной пользователя", summary);
        Assert.Contains("Ошибка приложения", summary);
    }

    [Fact]
    public async Task SafeContext_BoundsAndAggregatesSecurityEvents()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"ai-context-limit-{Guid.NewGuid():N}").Options;
        await using var context = new ApplicationDbContext(options);
        for (var index = 0; index < 120; index++)
        {
            context.SecurityEventLogs.Add(new SecurityEventLog
            {
                LastOccurredAtUtc = DateTime.UtcNow.AddMinutes(-index),
                FirstOccurredAtUtc = DateTime.UtcNow.AddMinutes(-index),
                Severity = SecurityEventSeverities.Warning,
                EventType = SecurityEventTypes.RateLimitExceeded,
                Title = $"Ограничение {index}",
                Path = "/Account/Login"
            });
        }
        await context.SaveChangesAsync();

        var summary = await new AdminAiContextService(context).BuildSummaryAsync();

        Assert.Contains("События безопасности (35, максимум 35)", summary);
        Assert.DoesNotContain("Ограничение 100", summary);
    }

    [Fact]
    public async Task SafeContext_ReplacesLoginDescriptionThatContainsPersonalData()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"ai-context-login-{Guid.NewGuid():N}").Options;
        await using var context = new ApplicationDbContext(options);
        context.SecurityEventLogs.Add(new SecurityEventLog
        {
            LastOccurredAtUtc = DateTime.UtcNow,
            FirstOccurredAtUtc = DateTime.UtcNow,
            Severity = SecurityEventSeverities.Information,
            EventType = SecurityEventTypes.LoginSucceeded,
            Title = "Успешный вход",
            Description = "Пользователь: Козлова Мария; логин: admin; ID: 4; IP: 192.0.2.10"
        });
        await context.SaveChangesAsync();

        var summary = await new AdminAiContextService(context).BuildSummaryAsync();

        Assert.DoesNotContain("Козлова", summary);
        Assert.DoesNotContain("admin", summary);
        Assert.Contains("данные учётной записи исключены", summary);
    }

    [Theory]
    [InlineData("/ManagerHome/Index", "раздел руководителя ОПОП")]
    [InlineData("/ApproverHome/Index", "раздел согласования")]
    [InlineData("/ModeratorHome/Index", "раздел публикации")]
    [InlineData("/Administration/Security", "раздел администрирования и журналов")]
    [InlineData("/unknown", "текущий административный раздел")]
    public void PageArea_UsesOnlyKnownPageCategories(string path, string expected)
    {
        Assert.Equal(expected, AdminAiContextService.ResolvePageArea(path));
    }

    [Fact]
    public async Task SafeContext_IncludesOperationalReportForCurrentPage()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"ai-context-operation-{Guid.NewGuid():N}").Options;
        await using var context = new ApplicationDbContext(options);
        context.EducationalPrograms.Add(new EducationalProgram { Id = 100, CodeReferral = "XX", Name = "Тестовая ОПОП" });
        context.EducationalProgramElements.AddRange(
            new EducationalProgramElement { Id = 101, EducationalProgramId = 100, StatusApprovals = ElementApprovalStatus.OnApproval },
            new EducationalProgramElement { Id = 102, EducationalProgramId = 100, StatusApprovals = ElementApprovalStatus.RevisionRequired });
        context.ElementStatusHistory.Add(new ElementStatusHistory
        {
            EducationalProgramElementId = 102,
            UserId = 1,
            OldStatus = ElementApprovalStatus.OnApproval,
            NewStatus = ElementApprovalStatus.RevisionRequired,
            ChangeDate = DateTime.UtcNow
        });
        context.Notifications.AddRange(
            new Notification { UserId = 77, Type = NotificationType.FileUploaded, CreatedAt = DateTime.UtcNow, IsRead = false },
            new Notification { UserId = 77, Type = NotificationType.CommentAdded, CreatedAt = DateTime.UtcNow, IsRead = true });
        await context.SaveChangesAsync();

        var summary = await new AdminAiContextService(context).BuildSummaryAsync("раздел руководителя ОПОП", 77);

        Assert.Contains("Краткий операционный отчёт для «раздел руководителя ОПОП»", summary);
        Assert.Contains("активных элементов: 2", summary);
        Assert.Contains("на доработке — 1", summary);
        Assert.Contains("переведено на доработку — 1", summary);
        Assert.Contains("Аналитика согласующего: ожидают решения — 1", summary);
        Assert.Contains("Аналитика модератора: готовы к публикации — 0", summary);
        Assert.Contains("Обычные уведомления текущего администратора: всего — 2; непрочитанные — 1", summary);
    }

    [Fact]
    public async Task GroqProvider_UsesConfiguredUrlBearerAndModelWithoutTools()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, """{"choices":[{"message":{"content":"Краткий ответ."}}]}""");
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.groq.com/openai/v1/") };
        var service = CreateService(client);

        var result = await service.AskAsync("Вопрос", "Сводка", CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("https://api.groq.com/openai/v1/chat/completions", handler.Request!.RequestUri!.ToString());
        Assert.Equal("Bearer", handler.Request.Headers.Authorization!.Scheme);
        Assert.Equal("api-test-key", handler.Request.Headers.Authorization.Parameter);
        Assert.Contains("\"model\":\"configured-model\"", handler.Body!);
        Assert.Contains("\"reasoning_effort\":\"none\"", handler.Body!);
        Assert.DoesNotContain("tools", handler.Body!, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(429, "Лимит Groq")]
    [InlineData(503, "временно недоступен")]
    public async Task GroqProvider_HandlesApiErrors(int statusCode, string expectedMessage)
    {
        var service = CreateService(new HttpClient(new RecordingHandler((HttpStatusCode)statusCode, "{}"))
        {
            BaseAddress = new Uri("https://api.groq.com/openai/v1/")
        });

        var result = await service.AskAsync("Вопрос", "Сводка", CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains(expectedMessage, result.Message);
    }

    [Fact]
    public async Task GroqProvider_HandlesTimeoutWithoutThrowing()
    {
        var service = CreateService(new HttpClient(new TimeoutHandler()) { BaseAddress = new Uri("https://api.groq.com/openai/v1/") });

        var result = await service.AskAsync("Вопрос", "Сводка", CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("время ожидания", result.Message);
    }

    [Fact]
    public async Task GroqProvider_HidesUnclosedThinkingOutputAndDoesNotExposeIt()
    {
        var service = CreateService(new HttpClient(new RecordingHandler(HttpStatusCode.OK,
            """{"choices":[{"message":{"content":"<think>private safe summary and internal reasoning"}}]}"""))
        {
            BaseAddress = new Uri("https://api.groq.com/openai/v1/")
        });

        var result = await service.AskAsync("Вопрос", "Сводка", CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.DoesNotContain("private", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("think", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GroqProvider_DoesNotCallHttpWhenKeyIsMissing()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, "{}");
        var service = new GroqAiAssistantService(
            new SingleClientFactory(new HttpClient(handler) { BaseAddress = new Uri("https://api.groq.com/openai/v1/") }),
            Options.Create(new AiOptions { Provider = "Groq", Model = "configured-model" }),
            NullLogger<GroqAiAssistantService>.Instance);

        var result = await service.AskAsync("Вопрос", "Сводка", CancellationToken.None);

        Assert.False(result.IsConfigured);
        Assert.Null(handler.Request);
    }

    private static GroqAiAssistantService CreateService(HttpClient client) => new(
        new SingleClientFactory(client),
        Options.Create(new AiOptions
        {
            Provider = "Groq",
            ApiKey = "api-test-key",
            Model = "configured-model",
            BaseUrl = "https://api.groq.com/openai/v1/",
            MaxAnswerCharacters = 500
        }),
        NullLogger<GroqAiAssistantService>.Instance);

    private sealed class SingleClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class RecordingHandler(HttpStatusCode statusCode, string content) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(statusCode) { Content = new StringContent(content) };
        }
    }

    private sealed class TimeoutHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new TaskCanceledException("simulated timeout");
    }
}
