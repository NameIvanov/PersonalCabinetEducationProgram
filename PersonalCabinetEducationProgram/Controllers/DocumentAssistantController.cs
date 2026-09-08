using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PersonalCabinetEducationProgram.Data;
using PersonalCabinetEducationProgram.Models;
using PersonalCabinetEducationProgram.Services;
using PersonalCabinetEducationProgram.Services.Documents;
using PersonalCabinetEducationProgram.ViewModels;

namespace PersonalCabinetEducationProgram.Controllers;

// Entry point for every workflow role. Review/publication analysis remains explicitly
// unavailable until its strategies are implemented; this page doesn't open manager APIs.
[Authorize(Roles = "Manager,Approver,Moderator,Admin")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class DocumentAssistantController(ApplicationDbContext db, DocumentAnalysisAccessService access) : Controller
{
    [HttpGet]
    [AppRateLimit(AppRateLimitPolicies.Search)]
    public async Task<IActionResult> Index(int? programId, int? fileId, int page = 1, CancellationToken cancellationToken = default)
    {
        var purpose = User.IsInRole(AppRoles.Admin) ? DocumentAnalysisPurpose.ManagerPreflight :
            User.IsInRole(AppRoles.Moderator) ? DocumentAnalysisPurpose.ModeratorPublication :
            User.IsInRole(AppRoles.Approver) ? DocumentAnalysisPurpose.ApproverReview : DocumentAnalysisPurpose.ManagerPreflight;
        EducationalProgramElementFile? selected = null;
        if (fileId.HasValue)
        {
            selected = await access.GetFileAsync(User, fileId.Value, purpose);
            if (selected == null || (programId.HasValue && selected.Element.EducationalProgramId != programId)) return NotFound();
            programId = selected.Element.EducationalProgramId;
            if (purpose == DocumentAnalysisPurpose.ManagerPreflight)
                return RedirectToAction("Index", "DocumentAnalysis", new { fileId });
        }
        // Only IDs are loaded before authorization. Names/files are fetched after the
        // existing element permission service has approved a representative element.
        var candidates = await db.EducationalProgramElements.AsNoTracking()
            .Where(e => !e.IsArchived && !e.EducationalProgram.IsArchived)
            .GroupBy(e => e.EducationalProgramId)
            .Select(g => new { ProgramId = g.Key, ElementId = g.Min(e => e.Id) }).ToListAsync(cancellationToken);
        var allowed = new List<int>();
        foreach (var candidate in candidates)
            if (await access.CanAccessElementAsync(User, candidate.ElementId, purpose, recordDenial: false)) allowed.Add(candidate.ProgramId);
        if (programId.HasValue && !allowed.Contains(programId.Value)) return NotFound();
        var programs = await db.EducationalPrograms.AsNoTracking().Where(p => allowed.Contains(p.Id)).OrderBy(p => p.Name).ToListAsync(cancellationToken);
        programId ??= programs.FirstOrDefault()?.Id;
        page = Math.Clamp(page, 1, 10000);
        var files = await db.EducationalProgramElementFiles.AsNoTracking().Include(f => f.Element)
            .Where(f => f.Element.EducationalProgramId == programId && !f.Element.IsArchived &&
                !f.Element.EducationalProgram.IsArchived && f.IsCurrent && !f.IsRemoved && f.OriginalFileName.ToLower().EndsWith(".pdf"))
            .OrderBy(f => f.Element.Name).ThenBy(f => f.OriginalFileName).ThenBy(f => f.Id)
            .Skip((page - 1) * 20).Take(21).ToListAsync(cancellationToken);
        ViewBag.SelectedProgramId = programId;
        return View(new DocumentAssistantViewModel { Purpose = purpose, Programs = programs, ProgramId = programId,
            Files = files.Take(20).ToList(), SelectedFile = selected, Page = page, HasNextPage = files.Count > 20 });
    }
}
