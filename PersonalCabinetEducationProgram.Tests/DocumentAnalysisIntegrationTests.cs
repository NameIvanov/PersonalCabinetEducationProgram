using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PersonalCabinetEducationProgram.Data;
using PersonalCabinetEducationProgram.Models;
using PersonalCabinetEducationProgram.Services.Documents;

namespace PersonalCabinetEducationProgram.Tests;

public sealed class DocumentAnalysisIntegrationTests
{
    [Theory]
    [InlineData(99, AppRoles.Manager, HttpStatusCode.NotFound)]
    [InlineData(2, AppRoles.Approver, HttpStatusCode.Forbidden)]
    [InlineData(3, AppRoles.Moderator, HttpStatusCode.Forbidden)]
    [InlineData(1, AppRoles.Manager, HttpStatusCode.OK)]
    [InlineData(4, AppRoles.Admin, HttpStatusCode.OK)]
    public async Task MvpRoleAndFileIsolation(int userId, string role, HttpStatusCode expected)
    {
        using var fixture = new Fixture();
        await fixture.SeedAsync();
        using var client = fixture.Client(userId, role);
        Assert.Equal(expected, (await client.GetAsync("/DocumentAnalysis/Index?fileId=101")).StatusCode);
    }

    [Fact]
    public async Task AnonymousAndCsrf_AreRejected()
    {
        using var fixture = new Fixture();
        await fixture.SeedAsync();
        using var anonymous = fixture.Factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/DocumentAnalysis/Index?fileId=101")).StatusCode);
        using var client = fixture.Client();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/DocumentAnalysis/Start", new FormUrlEncodedContent(new Dictionary<string, string> { ["fileId"] = "101" }))).StatusCode);
    }

    [Fact]
    public async Task StartWorkerHistoryFeedback_StoresEvidenceAndDecisionsWithoutWorkflowMutations()
    {
        using var fixture = new Fixture();
        await fixture.SeedAsync();
        using var client = fixture.Client();
        var token = await TokenAsync(client);
        var response = await Post(client, "/DocumentAnalysis/Start", token, ("fileId", "101"), ("previousFileId", "100"));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.True(await fixture.Worker.RunOneAsync(default));
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var analysis = await db.DocumentAnalyses.Include(a => a.Findings).Include(a => a.Changes).SingleAsync();
        Assert.Equal(DocumentAnalysisStatus.Completed, analysis.Status);
        Assert.Equal(64, analysis.FileHash.Length);
        Assert.Equal(1, analysis.FileRevision);
        Assert.Null(analysis.QueueSlot);
        Assert.Equal(1, analysis.RequestedByUserId);
        Assert.NotEmpty(analysis.Changes);
        Assert.Single(fixture.Semantic.Received!);
        Assert.DoesNotContain("PREVIOUS_ONLY", fixture.Semantic.Received![0].Text);
        var finding = Assert.Single(analysis.Findings);
        Assert.Equal("31.02.2026", finding.Quote);
        var feedback = await Post(client, "/DocumentAnalysis/Feedback", token, ("findingId", finding.Id.ToString()),
            ("version", finding.ConcurrencyToken.ToString()), ("decision", "Accepted"), ("comment", "Исправлено в исходнике"));
        Assert.Equal(HttpStatusCode.Redirect, feedback.StatusCode);
        db.ChangeTracker.Clear();
        finding = await db.DocumentFindings.SingleAsync();
        Assert.Equal(DocumentFindingStatus.Accepted, finding.Status);
        Assert.Equal("Исправлено в исходнике", (await db.DocumentFindingFeedback.SingleAsync()).Comment);
        Assert.Equal(HttpStatusCode.Redirect, (await Post(client, "/DocumentAnalysis/Feedback", token, ("findingId", finding.Id.ToString()),
            ("version", finding.ConcurrencyToken.ToString()), ("decision", "Dismissed"))).StatusCode);
        Assert.Equal(2, await db.DocumentFindingFeedback.CountAsync());
        Assert.Equal(ElementApprovalStatus.Uploaded, (await db.EducationalProgramElements.FindAsync(1))!.StatusApprovals);
        Assert.False((await db.EducationalProgramElementFiles.FindAsync(101))!.IsSubmitted);
        Assert.Empty(await db.AdminAiConversationMessages.ToListAsync());
        var html = WebUtility.HtmlDecode(await client.GetStringAsync(response.Headers.Location));
        Assert.Contains("Почему?", html);
        Assert.Contains("Учесть", html);
        Assert.Contains("Не является ошибкой", html);
    }

    [Fact]
    public async Task ForeignFileHistoryStatusFeedbackAndComparison_AreIsolated()
    {
        using var fixture = new Fixture();
        await fixture.SeedAsync();
        var id = await fixture.EnqueueAsync();
        await fixture.Worker.RunOneAsync(default);
        using var owner = fixture.Client();
        var token = await TokenAsync(owner);
        Assert.Equal(HttpStatusCode.NotFound, (await Post(owner, "/DocumentAnalysis/Start", token, ("fileId", "101"), ("previousFileId", "103"))).StatusCode);
        using var stranger = fixture.Client(99);
        var strangerTokenHtml = await stranger.GetStringAsync("/ManagerHome/Index");
        var strangerToken = ExtractToken(strangerTokenHtml);
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var finding = await db.DocumentFindings.SingleAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/DocumentAnalysis/Status?id={id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/DocumentAnalysis/Index?fileId=101&analysisId={id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Post(stranger, "/DocumentAnalysis/Feedback", strangerToken,
            ("findingId", finding.Id.ToString()), ("decision", "Accepted"), ("version", finding.ConcurrencyToken.ToString()))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Post(stranger, "/DocumentAnalysis/Start", strangerToken, ("fileId", "101"))).StatusCode);
        Assert.Empty(await db.DocumentFindingFeedback.ToListAsync());
    }

    [Theory]
    [InlineData("assignment")]
    [InlineData("role")]
    [InlineData("blocked")]
    [InlineData("archived")]
    [InlineData("hash")]
    [InlineData("submitted")]
    public async Task Worker_RevalidatesLivePermissionsAndVersionBeforeExtraction(string change)
    {
        using var fixture = new Fixture();
        await fixture.SeedAsync();
        await fixture.EnqueueAsync();
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            if (change == "assignment")
            {
                (await db.EducationalPrograms.FindAsync(1))!.UserId = null;
                db.EducationalProgramManagers.RemoveRange(db.EducationalProgramManagers.Where(m => m.EducationalProgramId == 1));
            }
            if (change == "role") db.UserRoles.RemoveRange(db.UserRoles.Where(r => r.UserId == 1));
            if (change == "blocked") (await db.Users.FindAsync(1))!.SecurityBlockedAtUtc = DateTime.UtcNow;
            if (change == "archived") (await db.EducationalPrograms.FindAsync(1))!.IsArchived = true;
            if (change == "submitted") (await db.EducationalProgramElementFiles.FindAsync(101))!.IsSubmitted = true;
            if (change == "hash") await File.AppendAllTextAsync(Path.Combine(fixture.Directory, "current.pdf"), "changed");
            await db.SaveChangesAsync();
        }
        await fixture.Worker.RunOneAsync(default);
        await using var check = fixture.Factory.Services.CreateAsyncScope();
        var result = await check.ServiceProvider.GetRequiredService<ApplicationDbContext>().DocumentAnalyses.Include(a => a.Findings).SingleAsync();
        Assert.Contains(result.Status, new[] { DocumentAnalysisStatus.Cancelled, DocumentAnalysisStatus.Failed });
        Assert.Empty(result.Findings);
        Assert.Equal(0, fixture.Extractor.Calls);
        Assert.Null(fixture.Semantic.Received);
    }

    [Fact]
    public async Task ScansWithoutOcr_AreManualReviewWithoutInventedFindings()
    {
        using var fixture = new Fixture();
        await fixture.SeedAsync();
        fixture.Extractor.Result = new([new(1, "", false, false, false, 90)], "Требуется OCR или ручная проверка");
        await fixture.EnqueueAsync();
        await fixture.Worker.RunOneAsync(default);
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var analysis = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().DocumentAnalyses.Include(a => a.Findings).Include(a => a.Pages).SingleAsync();
        Assert.Equal(DocumentAnalysisStatus.NeedsManualReview, analysis.Status);
        Assert.Empty(analysis.Findings);
        Assert.Contains("OCR", analysis.Message);
        Assert.False(Assert.Single(analysis.Pages).HasTextLayer);
        Assert.Null(fixture.Semantic.Received);
    }

    [Fact]
    public async Task QueueIsBounded_DeduplicatesActiveFile_AndReleasesExpiredJobs()
    {
        using var fixture = new Fixture(capacity: 1);
        await fixture.SeedAsync();
        var first = await fixture.EnqueueAsync();
        Assert.Equal(first, await fixture.EnqueueAsync());
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<DocumentAnalysisService>();
            await Assert.ThrowsAsync<DocumentAnalysisException>(() => service.EnqueueAsync(Principal(), 102, null, default));
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.DocumentAnalyses.SingleAsync()).DeadlineUtc = DateTime.UtcNow.AddSeconds(-1);
            await db.SaveChangesAsync();
        }
        await fixture.Worker.RunOneAsync(default);
        Assert.NotEqual(first, await fixture.EnqueueAsync());
    }

    [Fact]
    public async Task CancelledExtraction_ReleasesQueueSlotAndStoresSafeStatus()
    {
        using var fixture = new Fixture();
        await fixture.SeedAsync();
        fixture.Extractor.Cancel = true;
        await fixture.EnqueueAsync();
        await fixture.Worker.RunOneAsync(default);
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var analysis = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().DocumentAnalyses.SingleAsync();
        Assert.Equal(DocumentAnalysisStatus.Failed, analysis.Status);
        Assert.Null(analysis.QueueSlot);
        Assert.Null(analysis.UserQueueKey);
        Assert.Contains("таймауту", analysis.Message);
        Assert.Null(fixture.Semantic.Received);
    }

    [Fact]
    public async Task OversizedFile_IsRejectedBeforeQueueAndExtraction()
    {
        using var fixture = new Fixture();
        await fixture.SeedAsync();
        using (var stream = File.OpenWrite(Path.Combine(fixture.Directory, "current.pdf"))) stream.SetLength(21 * 1024 * 1024);
        await Assert.ThrowsAsync<DocumentAnalysisException>(() => fixture.EnqueueAsync());
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().DocumentAnalyses.ToListAsync());
        Assert.Equal(0, fixture.Extractor.Calls);
    }

    [Fact]
    public async Task FuturePurposePolicies_ReuseExistingApproverAssignmentsAndModeratorAccess()
    {
        using var fixture = new Fixture();
        await fixture.SeedAsync();
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var access = scope.ServiceProvider.GetRequiredService<DocumentAnalysisAccessService>();
        Assert.Null(await access.GetFileAsync(Principal(2, AppRoles.Approver), 101, DocumentAnalysisPurpose.ApproverReview));
        db.ApproverAssignments.Add(new() { ApproverUserId = 2, FacultyId = 1 });
        await db.SaveChangesAsync();
        Assert.NotNull(await access.GetFileAsync(Principal(2, AppRoles.Approver), 101, DocumentAnalysisPurpose.ApproverReview));
        Assert.Null(await access.GetFileAsync(Principal(2, AppRoles.Approver), 103, DocumentAnalysisPurpose.ApproverReview));
        Assert.NotNull(await access.GetFileAsync(Principal(3, AppRoles.Moderator), 101, DocumentAnalysisPurpose.ModeratorPublication));
        Assert.Null(await access.GetFileAsync(Principal(), 101, DocumentAnalysisPurpose.ModeratorPublication));
    }

    [Fact]
    public async Task AssistantSelector_ShowsOnlyAssignedProgramsAndFiles_AndDoesNotEnableFutureAnalysis()
    {
        using var fixture = new Fixture();
        await fixture.SeedAsync();
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.ApproverAssignments.Add(new() { ApproverUserId = 2, FacultyId = 1, AssignedByUserId = 4 });
            await db.SaveChangesAsync();
        }
        using var manager = fixture.Client();
        var managerHtml = WebUtility.HtmlDecode(await manager.GetStringAsync("/DocumentAssistant/Index?programId=1"));
        Assert.Contains("current.pdf", managerHtml);
        Assert.DoesNotContain("Чужая ОПОП", managerHtml);
        Assert.DoesNotContain("foreign.pdf", managerHtml);
        Assert.Equal(HttpStatusCode.Redirect, (await manager.GetAsync("/DocumentAssistant/Index?fileId=101")).StatusCode);
        using var approver = fixture.Client(2, AppRoles.Approver);
        var html = WebUtility.HtmlDecode(await approver.GetStringAsync("/DocumentAssistant/Index?fileId=101"));
        Assert.Contains("Выбран PDF: current.pdf", html);
        Assert.Contains("ещё не включён", html);
        Assert.DoesNotContain("/DocumentAnalysis/Start", html);
        Assert.DoesNotContain("Чужая ОПОП", html);
        Assert.Equal(HttpStatusCode.NotFound, (await approver.GetAsync("/DocumentAssistant/Index?fileId=103")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await approver.GetAsync("/DocumentAssistant/Index?programId=90")).StatusCode);
        using var moderator = fixture.Client(3, AppRoles.Moderator);
        Assert.Equal(HttpStatusCode.OK, (await moderator.GetAsync("/DocumentAssistant/Index?fileId=101")).StatusCode);
        await using var check = fixture.Factory.Services.CreateAsyncScope();
        Assert.Empty(await check.ServiceProvider.GetRequiredService<ApplicationDbContext>().DocumentAnalyses.ToListAsync());
        Assert.Equal(0, fixture.Extractor.Calls);
    }

    private static ClaimsPrincipal Principal(int id = 1, string role = AppRoles.Manager) => new(new ClaimsIdentity(
        [new Claim(ClaimTypes.NameIdentifier, id.ToString()), new Claim(ClaimTypes.Role, role), new Claim(ClaimTypes.Name, "Руководитель")], "test"));
    private static async Task<string> TokenAsync(HttpClient client) => ExtractToken(await client.GetStringAsync("/DocumentAnalysis/Index?fileId=101"));
    private static string ExtractToken(string html) => WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
    private static Task<HttpResponseMessage> Post(HttpClient client, string path, string token, params (string Key, string Value)[] values)
    {
        var form = values.ToDictionary(x => x.Key, x => x.Value);
        form["__RequestVerificationToken"] = token;
        return client.PostAsync(path, new FormUrlEncodedContent(form));
    }

    private sealed class Fixture : IDisposable
    {
        public string Directory { get; } = Path.Combine(Path.GetTempPath(), "document-tests-" + Guid.NewGuid().ToString("N"));
        public FakeExtractor Extractor { get; } = new();
        public FakeSemantic Semantic { get; } = new();
        public CustomWebApplicationFactory Factory { get; }
        public DocumentAnalysisWorker Worker => new(Factory.Services.GetRequiredService<IServiceScopeFactory>(),
            Factory.Services.GetRequiredService<IOptions<DocumentAnalysisOptions>>(), NullLogger<DocumentAnalysisWorker>.Instance);
        public Fixture(int capacity = 20)
        {
            System.IO.Directory.CreateDirectory(Directory);
            Factory = new(services =>
            {
                foreach (var descriptor in services.Where(s => s.ServiceType == typeof(IHostedService) && s.ImplementationType == typeof(DocumentAnalysisWorker)).ToList()) services.Remove(descriptor);
                services.Configure<FileStorageSettings>(o => o.StoragePath = Directory);
                services.Configure<DocumentAnalysisOptions>(o => { o.QueueCapacity = capacity; o.Templates = [new() { Id = "test" }]; });
                services.RemoveAll<IPdfTextExtractor>(); services.AddSingleton<IPdfTextExtractor>(Extractor);
                services.RemoveAll<IDocumentSemanticAnalyzer>(); services.AddSingleton<IDocumentSemanticAnalyzer>(Semantic);
            });
        }
        public HttpClient Client(int id = 1, string role = AppRoles.Manager)
        {
            var client = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            client.DefaultRequestHeaders.Add("X-Test-UserId", id.ToString()); client.DefaultRequestHeaders.Add("X-Test-Role", role);
            return client;
        }
        public async Task SeedAsync()
        {
            await using var scope = Factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.EducationalProgramElements.FindAsync(1))!.StatusApprovals = ElementApprovalStatus.Uploaded;
            db.EducationalPrograms.Add(new() { Id = 90, Name = "Чужая ОПОП", UserId = 4 });
            db.EducationalProgramElements.Add(new() { Id = 90, EducationalProgramId = 90, StatusApprovals = ElementApprovalStatus.Uploaded });
            foreach (var (id, name, element, current) in new[] { (100, "previous.pdf", 1, false), (101, "current.pdf", 1, true), (102, "another.pdf", 1, true), (103, "foreign.pdf", 90, false) })
            {
                await File.WriteAllTextAsync(Path.Combine(Directory, name), "%PDF-test-" + name);
                db.EducationalProgramElementFiles.Add(new() { Id = id, EducationalProgramElementId = element, StoredFileName = name, OriginalFileName = name,
                    IsCurrent = current, IsRemoved = !current, RevisionNumber = 1, UploadedByUserId = 1, UploadedAt = DateTime.UtcNow.AddMinutes(id - 110) });
            }
            await db.SaveChangesAsync();
        }
        public async Task<long> EnqueueAsync()
        {
            await using var scope = Factory.Services.CreateAsyncScope();
            return (await scope.ServiceProvider.GetRequiredService<DocumentAnalysisService>().EnqueueAsync(Principal(), 101, null, default)).Id;
        }
        public void Dispose()
        {
            Factory.Dispose();
            foreach (var name in new[] { "previous.pdf", "current.pdf", "another.pdf", "foreign.pdf" }) File.Delete(Path.Combine(Directory, name));
            System.IO.Directory.Delete(Directory);
        }
    }
    private sealed class FakeExtractor : IPdfTextExtractor
    {
        public int Calls;
        public bool Cancel;
        public PdfTextResult Result { get; set; } = new([DocumentPdfTests.Page(1, "Дата 31.02.2026. Только выбранный PDF.")]);
        public Task<PdfTextResult> ExtractAsync(string path, CancellationToken ct)
        {
            Calls++;
            if (Cancel) throw new OperationCanceledException();
            return Task.FromResult(Path.GetFileName(path) == "previous.pdf" ? new PdfTextResult([DocumentPdfTests.Page(1, "PREVIOUS_ONLY 01.02.2026")]) : Result);
        }
    }
    private sealed class FakeSemantic : IDocumentSemanticAnalyzer
    {
        public IReadOnlyList<PdfPageText>? Received;
        public Task<SemanticAnalysisResult> AnalyzeAsync(IReadOnlyList<PdfPageText> selectedPdf, CancellationToken ct)
        { Received = selectedPdf; return Task.FromResult(new SemanticAnalysisResult([], "Выполнено", true)); }
    }
}
