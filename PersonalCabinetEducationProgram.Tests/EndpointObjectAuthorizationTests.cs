using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PersonalCabinetEducationProgram.Data;
using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Tests;

public sealed class EndpointObjectAuthorizationTests
{
    [Fact]
    public async Task Manager_CannotAccessObjectsFromUnassignedProgram()
    {
        using var factory = new CustomWebApplicationFactory();
        var objectIds = await SeedProtectedObjectsAsync(factory);
        using var client = CreateClient(factory, 99, AppRoles.Manager);

        await AssertStatusAsync(client, HttpStatusCode.Forbidden,
            "/ManagerHome/Index?programId=1",
            "/ManagerHome/History?elementId=1",
            "/ManagerHome/Comments?elementId=1",
            "/ManagerHome/EditElement?elementId=1",
            "/ManagerHome/ManageFiles?elementId=1",
            "/ManagerHome/Preview?elementId=1",
            $"/ElementFiles/Preview?id={objectIds.FileId}",
            "/CurriculumImport/Index?programId=1");

        await AssertStatusAsync(client, HttpStatusCode.NotFound,
            $"/HistoryFiles/Preview?historyId={objectIds.HistoryId}");

        var token = await GetAntiforgeryTokenAsync(client, "/ManagerHome/Index");
        await AssertPostStatusAsync(client, "/ElementFiles/Download", HttpStatusCode.Forbidden,
            token, ("id", objectIds.FileId.ToString()));
        await AssertPostStatusAsync(client, "/ManagerHome/Download", HttpStatusCode.Forbidden,
            token, ("elementId", "1"));
        await AssertPostStatusAsync(client, "/HistoryFiles/Download", HttpStatusCode.NotFound,
            token, ("historyId", objectIds.HistoryId.ToString()));
        await AssertPostStatusAsync(client, "/CurriculumImport/Download", HttpStatusCode.Forbidden,
            token, ("id", objectIds.ImportId.ToString()));
        await AssertPostStatusAsync(client, "/ManagerHome/SendForApproval", HttpStatusCode.Forbidden,
            token, ("elementId", "1"));
        await AssertPostStatusAsync(client, "/ManagerHome/AddComment", HttpStatusCode.Forbidden,
            token, ("elementId", "1"), ("commentText", "Чужой комментарий"));
        await AssertPostStatusAsync(client, "/ManagerHome/UpdateCommentStatus", HttpStatusCode.Forbidden,
            token, ("commentId", objectIds.CommentId.ToString()), ("status", CommentStatus.Read));
        await AssertPostStatusAsync(client, "/ManagerHome/EditElement", HttpStatusCode.Forbidden,
            token, ("elementId", "1"), ("version", "1"), ("name", "Подмена"));
        await AssertPostStatusAsync(client, "/ManagerHome/RemoveCurrentFile", HttpStatusCode.Forbidden,
            token, ("fileId", objectIds.FileId.ToString()));
        await AssertPostStatusAsync(client, "/ManagerHome/ReplaceCurrentFile", HttpStatusCode.Forbidden,
            token, ("fileId", objectIds.FileId.ToString()));
        await AssertPostStatusAsync(client, "/ManagerHome/Upload", HttpStatusCode.Forbidden,
            token, ("elementId", "1"));
        await AssertPostStatusAsync(client, "/CurriculumImport/Apply", HttpStatusCode.Forbidden,
            token, ("programId", "1"), ("token", "invalid"));
        await AssertPostStatusAsync(client, "/CurriculumImport/Preview", HttpStatusCode.Forbidden,
            token, ("programId", "1"));
    }

    [Fact]
    public async Task Approver_CannotAccessObjectsOutsideAssignments()
    {
        using var factory = new CustomWebApplicationFactory();
        var objectIds = await SeedProtectedObjectsAsync(factory);
        using var client = CreateClient(factory, 99, AppRoles.Approver);

        await AssertStatusAsync(client, HttpStatusCode.Forbidden,
            "/ApproverHome/Index?programId=1",
            "/ApproverHome/History?elementId=1",
            "/ApproverHome/Comments?elementId=1",
            "/ApproverHome/ManageFiles?elementId=1",
            "/ApproverHome/Preview?elementId=1",
            $"/ElementFiles/Preview?id={objectIds.FileId}");

        await AssertStatusAsync(client, HttpStatusCode.NotFound,
            $"/HistoryFiles/Preview?historyId={objectIds.HistoryId}");

        var token = await GetAntiforgeryTokenAsync(client, "/ApproverHome/Index");
        await AssertPostStatusAsync(client, "/ElementFiles/Download", HttpStatusCode.Forbidden,
            token, ("id", objectIds.FileId.ToString()));
        await AssertPostStatusAsync(client, "/ApproverHome/Download", HttpStatusCode.Forbidden,
            token, ("elementId", "1"));
        await AssertPostStatusAsync(client, "/HistoryFiles/Download", HttpStatusCode.NotFound,
            token, ("historyId", objectIds.HistoryId.ToString()));
        await AssertPostStatusAsync(client, "/ApproverHome/Approve", HttpStatusCode.Forbidden,
            token, ("elementId", "1"));
        await AssertPostStatusAsync(client, "/ApproverHome/Reject", HttpStatusCode.Forbidden,
            token, ("elementId", "1"));
        await AssertPostStatusAsync(client, "/ApproverHome/AddComment", HttpStatusCode.Forbidden,
            token, ("elementId", "1"), ("commentText", "Чужой комментарий"));
        await AssertPostStatusAsync(client, "/ApproverHome/UpdateCommentStatus", HttpStatusCode.Forbidden,
            token, ("commentId", objectIds.CommentId.ToString()), ("status", CommentStatus.Read));
    }

    [Fact]
    public async Task Moderator_CannotUseAdminEndpointsOrArchivedElements()
    {
        using var factory = new CustomWebApplicationFactory();
        await SetElementArchivedAsync(factory, 1);
        using var client = CreateClient(factory, 3, AppRoles.Moderator);

        await AssertStatusAsync(client, HttpStatusCode.Forbidden,
            "/Admin/Programs",
            "/Admin/Departments",
            "/Admin/DepartmentDetails/1",
            "/Admin/Faculties",
            "/Admin/FacultyDetails/1",
            "/Administration/Logs",
            "/Administration/Server",
            "/Administration/Storage",
            "/Administration/Security",
            "/Administration/Audit",
            "/ModeratorHome/History?elementId=1",
            "/ModeratorHome/Comments?elementId=1",
            "/ModeratorHome/ManageFiles?elementId=1",
            "/ModeratorHome/Preview?elementId=1");

        var token = await GetAntiforgeryTokenAsync(client, "/ModeratorHome/Index");
        await AssertPostStatusAsync(client, "/ModeratorHome/Publish", HttpStatusCode.Forbidden,
            token, ("elementId", "1"));
        await AssertPostStatusAsync(client, "/ModeratorHome/Unpublish", HttpStatusCode.Forbidden,
            token, ("elementId", "1"));
        await AssertPostStatusAsync(client, "/ModeratorHome/Download", HttpStatusCode.Forbidden,
            token, ("elementId", "1"));
    }

    [Fact]
    public async Task NotificationEndpoints_OnlyReadCurrentUsersNotifications()
    {
        using var factory = new CustomWebApplicationFactory();
        var notificationId = await SeedNotificationAsync(factory, 1, "Скрытое уведомление владельца");
        using var client = CreateClient(factory, 99, AppRoles.Manager);

        var openResponse = await client.GetAsync($"/Notifications/Open?id={notificationId}");
        Assert.Equal(HttpStatusCode.NotFound, openResponse.StatusCode);

        var page = await client.GetStringAsync("/Notifications/Index");
        Assert.DoesNotContain("Скрытое уведомление владельца", page);

        var token = ExtractAntiforgeryToken(page);
        await AssertPostStatusAsync(client, "/Notifications/MarkAllRead", HttpStatusCode.Redirect, token);

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False((await context.Notifications.SingleAsync(n => n.Id == notificationId)).IsRead);
    }

    [Fact]
    public void Application_RequiresAuthenticationByDefault()
    {
        using var factory = new CustomWebApplicationFactory();
        var options = factory.Services.GetRequiredService<IOptions<AuthorizationOptions>>().Value;

        Assert.NotNull(options.FallbackPolicy);
        Assert.Contains(options.FallbackPolicy!.Requirements, requirement =>
            requirement is DenyAnonymousAuthorizationRequirement);
    }

    [Fact]
    public async Task AuthorizedRoles_RetainAccessToTheirObjects()
    {
        using var factory = new CustomWebApplicationFactory();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.ApproverAssignments.Add(new ApproverAssignment
            {
                ApproverUserId = 2,
                FacultyId = 1,
                AssignedByUserId = 4,
                AssignedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        }

        using var manager = CreateClient(factory, 1, AppRoles.Manager);
        using var approver = CreateClient(factory, 2, AppRoles.Approver);
        using var moderator = CreateClient(factory, 3, AppRoles.Moderator);
        using var admin = CreateClient(factory, 4, AppRoles.Admin);

        Assert.Equal(HttpStatusCode.OK, (await manager.GetAsync("/ManagerHome/History?elementId=1")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await approver.GetAsync("/ApproverHome/History?elementId=1")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await moderator.GetAsync("/ModeratorHome/History?elementId=1")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/Admin/Programs")).StatusCode);

        var managerFiles = await manager.GetStringAsync("/ManagerHome/ManageFiles?elementId=1");
        var approverFiles = await approver.GetStringAsync("/ApproverHome/ManageFiles?elementId=1");
        var moderatorFiles = await moderator.GetStringAsync("/ModeratorHome/ManageFiles?elementId=1");

        Assert.Contains("currentFilesUpload", managerFiles);
        Assert.Contains("Текущий комплект файлов", approverFiles);
        Assert.Contains("Текущий комплект файлов", moderatorFiles);
        Assert.Contains("доступен для просмотра и скачивания", approverFiles);
        Assert.Contains("доступен для просмотра и скачивания", moderatorFiles);
        Assert.DoesNotContain("currentFilesUpload", approverFiles);
        Assert.DoesNotContain("currentFilesUpload", moderatorFiles);
        Assert.DoesNotContain("ReplaceCurrentFile", approverFiles);
        Assert.DoesNotContain("ReplaceCurrentFile", moderatorFiles);
        Assert.DoesNotContain("RemoveCurrentFile", approverFiles);
        Assert.DoesNotContain("RemoveCurrentFile", moderatorFiles);
    }

    private static async Task<(int FileId, int HistoryId, int ImportId, int CommentId)> SeedProtectedObjectsAsync(
        CustomWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var file = new EducationalProgramElementFile
        {
            EducationalProgramElementId = 1,
            StoredFileName = "protected.pdf",
            OriginalFileName = "protected.pdf",
            RevisionNumber = 1,
            IsCurrent = true,
            UploadedAt = DateTime.UtcNow,
            UploadedByUserId = 1
        };
        var history = new ElementStatusHistory
        {
            EducationalProgramElementId = 1,
            UserId = 1,
            OldStatus = ElementApprovalStatus.Uploaded,
            NewStatus = ElementApprovalStatus.OnApproval,
            ChangeDate = DateTime.UtcNow,
            Comment = "Защищённая история",
            FilePath = "protected.pdf",
            FileName = "protected.pdf"
        };
        var import = new CurriculumImport
        {
            EducationalProgramId = 1,
            ImportedByUserId = 1,
            OriginalFileName = "protected.plx",
            StoredFilePath = "plx/imports/protected.plx",
            ImportedAt = DateTime.UtcNow
        };
        var comment = new EducationalProgramElementComment
        {
            EducationalProgramElementId = 1,
            UserId = 1,
            DateTimeComment = DateTime.UtcNow,
            CommentContent = "Защищённый комментарий",
            Status = CommentStatus.New
        };
        context.AddRange(file, history, import, comment);
        await context.SaveChangesAsync();
        return (file.Id, history.Id, import.Id, comment.Id);
    }

    private static async Task SetElementArchivedAsync(CustomWebApplicationFactory factory, int elementId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var element = await context.EducationalProgramElements.SingleAsync(e => e.Id == elementId);
        element.IsArchived = true;
        await context.SaveChangesAsync();
    }

    private static async Task<int> SeedNotificationAsync(
        CustomWebApplicationFactory factory,
        int userId,
        string title)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var notification = new Notification
        {
            UserId = userId,
            EducationalProgramElementId = 1,
            ActorName = "Владелец",
            Type = NotificationType.StatusChanged,
            Title = title,
            Message = "Содержимое доступно только владельцу.",
            CreatedAt = DateTime.UtcNow
        };
        context.Notifications.Add(notification);
        await context.SaveChangesAsync();
        return notification.Id;
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

    private static async Task AssertStatusAsync(
        HttpClient client,
        HttpStatusCode expected,
        params string[] urls)
    {
        foreach (var url in urls)
        {
            var response = await client.GetAsync(url);
            Assert.True(response.StatusCode == expected,
                $"GET {url}: ожидался {(int)expected}, получен {(int)response.StatusCode}.");
        }
    }

    private static async Task AssertPostStatusAsync(
        HttpClient client,
        string url,
        HttpStatusCode expected,
        string antiforgeryToken,
        params (string Name, string Value)[] values)
    {
        var form = values.ToDictionary(pair => pair.Name, pair => pair.Value);
        form["__RequestVerificationToken"] = antiforgeryToken;
        var response = await client.PostAsync(url, new FormUrlEncodedContent(form));
        Assert.True(response.StatusCode == expected,
            $"POST {url}: ожидался {(int)expected}, получен {(int)response.StatusCode}.");
    }

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string pageUrl)
    {
        var html = await client.GetStringAsync(pageUrl);
        return ExtractAntiforgeryToken(html);
    }

    private static string ExtractAntiforgeryToken(string html)
    {
        var match = Regex.Match(
            html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"",
            RegexOptions.CultureInvariant);
        Assert.True(match.Success, "На странице отсутствует antiforgery-токен.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }
}
