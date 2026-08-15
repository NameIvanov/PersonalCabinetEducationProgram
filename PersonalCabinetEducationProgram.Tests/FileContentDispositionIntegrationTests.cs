using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using PersonalCabinetEducationProgram.Data;
using PersonalCabinetEducationProgram.Models;
using PersonalCabinetEducationProgram.Services;

namespace PersonalCabinetEducationProgram.Tests;

public sealed class FileContentDispositionIntegrationTests
{
    private const string OriginalFileName = "Б1.В.ДВ.03.01 1С предприятие.pdf";

    [Fact]
    public async Task Preview_UsesSafeUtf8InlineHeader_ForEveryAuthorizedRole()
    {
        var storagePath = Path.Combine(Path.GetTempPath(), $"file-preview-{Guid.NewGuid():N}");
        Directory.CreateDirectory(storagePath);
        try
        {
            const string storedFileName = "document.pdf";
            await File.WriteAllBytesAsync(Path.Combine(storagePath, storedFileName), "%PDF-1.4\npreview"u8.ToArray());
            using var factory = CreateFactory(storagePath);
            var files = await SeedFilesAsync(factory, storedFileName);

            foreach (var (userId, role, url) in new[]
            {
                (1, AppRoles.Manager, "/ManagerHome/Preview?elementId=1"),
                (2, AppRoles.Approver, "/ApproverHome/Preview?elementId=1"),
                (3, AppRoles.Moderator, "/ModeratorHome/Preview?elementId=1"),
                (4, AppRoles.Admin, "/Admin/PreviewElement?elementId=1"),
                (1, AppRoles.Manager, $"/ElementFiles/Preview?id={files.FileId}"),
                (2, AppRoles.Approver, $"/ElementFiles/Preview?id={files.FileId}"),
                (3, AppRoles.Moderator, $"/ElementFiles/Preview?id={files.FileId}"),
                (4, AppRoles.Admin, $"/ElementFiles/Preview?id={files.FileId}"),
                (1, AppRoles.Manager, $"/HistoryFiles/Preview?historyId={files.HistoryId}"),
                (2, AppRoles.Approver, $"/HistoryFiles/Preview?historyId={files.HistoryId}"),
                (3, AppRoles.Moderator, $"/HistoryFiles/Preview?historyId={files.HistoryId}"),
                (4, AppRoles.Admin, $"/HistoryFiles/Preview?historyId={files.HistoryId}")
            })
            {
                using var client = CreateClient(factory, userId, role);
                using var response = await client.GetAsync(url);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                AssertSafeInlineFileName(response);
            }
        }
        finally
        {
            Directory.Delete(storagePath, recursive: true);
        }
    }

    [Fact]
    public async Task Download_PreservesRussianFileName_UsingSafeUtf8Header()
    {
        var storagePath = Path.Combine(Path.GetTempPath(), $"file-download-{Guid.NewGuid():N}");
        Directory.CreateDirectory(storagePath);
        try
        {
            const string storedFileName = "document.pdf";
            await File.WriteAllBytesAsync(Path.Combine(storagePath, storedFileName), "%PDF-1.4\ndownload"u8.ToArray());
            using var factory = CreateFactory(storagePath);
            var files = await SeedFilesAsync(factory, storedFileName);
            using var client = CreateClient(factory, 1, AppRoles.Manager);
            var token = AntiforgeryTokenExtractor.Extract(
                await client.GetStringAsync("/ManagerHome/Index?programId=1"));

            using var response = await client.PostAsync("/ElementFiles/Download",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = token,
                    ["id"] = files.FileId.ToString()
                }));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            AssertSafeFileName(response, "attachment");

            using var historyResponse = await client.PostAsync("/HistoryFiles/Download",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = token,
                    ["historyId"] = files.HistoryId.ToString()
                }));
            Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);
            AssertSafeFileName(historyResponse, "attachment");
        }
        finally
        {
            Directory.Delete(storagePath, recursive: true);
        }
    }

    private static CustomWebApplicationFactory CreateFactory(string storagePath) =>
        new(services => services.PostConfigure<FileStorageSettings>(options => options.StoragePath = storagePath));

    private static async Task<(int FileId, int HistoryId)> SeedFilesAsync(
        CustomWebApplicationFactory factory,
        string storedFileName)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var element = await context.EducationalProgramElements.SingleAsync(item => item.Id == 1);
        element.FilePath = storedFileName;
        element.FileName = OriginalFileName;
        context.ApproverAssignments.Add(new ApproverAssignment
        {
            ApproverUserId = 2,
            FacultyId = 1,
            AssignedByUserId = 4,
            AssignedAt = DateTime.UtcNow
        });
        var file = new EducationalProgramElementFile
        {
            EducationalProgramElementId = element.Id,
            StoredFileName = storedFileName,
            OriginalFileName = OriginalFileName,
            RevisionNumber = 1,
            IsCurrent = true,
            UploadedAt = DateTime.UtcNow,
            UploadedByUserId = 1
        };
        context.EducationalProgramElementFiles.Add(file);
        var history = new ElementStatusHistory
        {
            EducationalProgramElementId = element.Id,
            UserId = 1,
            OldStatus = ElementApprovalStatus.Uploaded,
            NewStatus = ElementApprovalStatus.OnApproval,
            ChangeDate = DateTime.UtcNow,
            FilePath = storedFileName,
            FileName = OriginalFileName
        };
        context.ElementStatusHistory.Add(history);
        await context.SaveChangesAsync();
        return (file.Id, history.Id);
    }

    private static HttpClient CreateClient(CustomWebApplicationFactory factory, int userId, string role)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Role", role);
        return client;
    }

    private static void AssertSafeInlineFileName(HttpResponseMessage response) =>
        AssertSafeFileName(response, "inline");

    private static void AssertSafeFileName(HttpResponseMessage response, string dispositionType)
    {
        var header = Assert.Single(response.Content.Headers.GetValues("Content-Disposition"));
        Assert.StartsWith(dispositionType + ";", header, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("filename*=UTF-8''" + Uri.EscapeDataString(OriginalFileName), header,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(OriginalFileName, header, StringComparison.Ordinal);
        Assert.DoesNotContain('\r', header);
        Assert.DoesNotContain('\n', header);
    }
}
