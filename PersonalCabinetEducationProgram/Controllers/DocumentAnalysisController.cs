using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PersonalCabinetEducationProgram.Data;
using PersonalCabinetEducationProgram.Models;
using PersonalCabinetEducationProgram.Services;
using PersonalCabinetEducationProgram.Services.Documents;
using PersonalCabinetEducationProgram.ViewModels;

namespace PersonalCabinetEducationProgram.Controllers;

[Authorize(Roles = "Manager,Admin")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class DocumentAnalysisController(ApplicationDbContext db, DocumentAnalysisAccessService access,
    DocumentAnalysisService service, IOptions<DocumentAnalysisOptions> options) : Controller
{
    [HttpGet]
    [AppRateLimit(AppRateLimitPolicies.Search)]
    public async Task<IActionResult> Index(int fileId, long? analysisId, int historyPage = 1, CancellationToken cancellationToken = default)
    {
        var file = await access.GetFileAsync(User, fileId);
        if (file == null) return NotFound();
        historyPage = Math.Clamp(historyPage, 1, 10000);
        var history = await db.DocumentAnalyses.AsNoTracking().Where(a => a.FileId == fileId && a.Purpose == DocumentAnalysisPurpose.ManagerPreflight)
            .OrderByDescending(a => a.CreatedAtUtc).Skip((historyPage - 1) * 10).Take(11).ToListAsync(cancellationToken);
        var selectedId = analysisId ?? history.FirstOrDefault()?.Id;
        var selected = selectedId.HasValue ? await db.DocumentAnalyses.AsNoTracking()
            .Include(a => a.Findings).ThenInclude(f => f.Feedback)
            .Include(a => a.Pages).Include(a => a.Changes).AsSplitQuery()
            .SingleOrDefaultAsync(a => a.Id == selectedId && a.FileId == fileId && a.Purpose == DocumentAnalysisPurpose.ManagerPreflight, cancellationToken) : null;
        if (analysisId.HasValue && selected == null) return NotFound();
        return View(new DocumentAnalysisViewModel
        {
            File = file, History = history.Take(10).ToList(), Selected = selected,
            HistoryPage = historyPage, HasMoreHistory = history.Count > 10, CanStart = DocumentAnalysisService.CanStart(file),
            CloudEnabled = options.Value.SemanticAnalysisEnabled, MaxFileBytes = options.Value.MaxFileBytes,
            PreviousFiles = await db.EducationalProgramElementFiles.AsNoTracking()
                .Where(f => f.EducationalProgramElementId == file.EducationalProgramElementId && f.Id != fileId && !f.IsCurrent && f.UploadedAt <= file.UploadedAt)
                .OrderByDescending(f => f.UploadedAt).ToListAsync(cancellationToken)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [AppRateLimit(AppRateLimitPolicies.AiAssistant)]
    public async Task<IActionResult> Start(int fileId, int? previousFileId, CancellationToken cancellationToken)
    {
        try
        {
            var analysis = await service.EnqueueAsync(User, fileId, previousFileId, cancellationToken);
            return RedirectToAction(nameof(Index), new { fileId, analysisId = analysis.Id });
        }
        catch (UnauthorizedAccessException) { return NotFound(); }
        catch (DocumentAnalysisException error) { TempData["ErrorMessage"] = error.Message; }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return StatusCode(499); }
        catch { TempData["ErrorMessage"] = "Не удалось поставить проверку в очередь. Хранилище или база временно недоступны."; }
        return RedirectToAction(nameof(Index), new { fileId });
    }

    [HttpGet]
    [AppRateLimit(AppRateLimitPolicies.Search)]
    public async Task<IActionResult> Status(long id)
    {
        var analysis = await db.DocumentAnalyses.AsNoTracking().SingleOrDefaultAsync(a => a.Id == id);
        if (analysis == null || analysis.Purpose != DocumentAnalysisPurpose.ManagerPreflight || await access.GetFileAsync(User, analysis.FileId) == null) return NotFound();
        return Json(new { pending = analysis.Status is DocumentAnalysisStatus.Queued or DocumentAnalysisStatus.Running, message = analysis.Message });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [AppRateLimit(AppRateLimitPolicies.CommentStatus)]
    public async Task<IActionResult> Feedback(long findingId, string decision, string? comment, Guid version, CancellationToken cancellationToken)
    {
        if (decision is not ("Accepted" or "Dismissed") || (comment?.Length ?? 0) > 1000) return BadRequest();
        var finding = await db.DocumentFindings.Include(f => f.Analysis).SingleOrDefaultAsync(f => f.Id == findingId, cancellationToken);
        if (finding == null || finding.Analysis.Purpose != DocumentAnalysisPurpose.ManagerPreflight ||
            await access.GetFileAsync(User, finding.Analysis.FileId) == null) return NotFound();
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        if (finding.ConcurrencyToken != version) return Conflict("Реакция уже изменена. Обновите страницу.");
        finding.Status = decision == "Accepted" ? DocumentFindingStatus.Accepted : DocumentFindingStatus.Dismissed;
        finding.ConcurrencyToken = Guid.NewGuid();
        db.DocumentFindingFeedback.Add(new()
        {
            DocumentFindingId = findingId, UserId = userId, UserName = (User.Identity?.Name ?? userId.ToString())[..Math.Min(300, (User.Identity?.Name ?? userId.ToString()).Length)],
            Decision = finding.Status, Comment = comment?.Trim() ?? ""
        });
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Conflict("Реакция уже изменена. Обновите страницу."); }
        return RedirectToAction(nameof(Index), new { fileId = finding.Analysis.FileId, analysisId = finding.DocumentAnalysisId });
    }
}
