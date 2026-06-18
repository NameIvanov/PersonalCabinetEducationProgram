using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PersonalCabinetEducationProgram.Data;
using PersonalCabinetEducationProgram.Models;
using PersonalCabinetEducationProgram.Services;

namespace PersonalCabinetEducationProgram.Controllers
{
    [Authorize(Roles = "Moderator,Admin")]
    public class ModeratorHomeController : Controller
    {
        private readonly FileStorageSettings _storageSettings;
        private readonly ApplicationDbContext _context;
        private readonly ElementWorkflowService _workflowService;

        public ModeratorHomeController(
            IOptions<FileStorageSettings> storageSettings,
            ApplicationDbContext context,
            ElementWorkflowService workflowService)
        {
            _storageSettings = storageSettings.Value;
            _context = context;
            _workflowService = workflowService;
        }

        private int GetCurrentUserId()
        {
            return int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        }

        public async Task<IActionResult> Index(int? programId, string tab = "disciplines")
        {
            var programs = await _context.EducationalPrograms
                .Include(p => p.Assignments).ThenInclude(a => a.Department)
                .Include(p => p.Assignments).ThenInclude(a => a.Faculty)
                .ToListAsync();

            int? selectedProgramId = programId ?? programs.FirstOrDefault()?.Id;

            var elements = selectedProgramId == null
                ? new List<EducationalProgramElement>()
                : await _context.EducationalProgramElements
                    .Where(e => e.EducationalProgramId == selectedProgramId)
                    .Include(e => e.EducationalProgram)
                    .ToListAsync();

            ViewBag.Programs = programs;
            ViewBag.SelectedProgramId = selectedProgramId;
            ViewBag.ActiveTab = tab;
            ViewBag.Comments = await _context.EducationalProgramElementComment.Include(c => c.User).ToListAsync();

            return View(elements);
        }

        public async Task<IActionResult> Download(int elementId)
        {
            var element = await _context.EducationalProgramElements.FindAsync(elementId);
            if (element == null || string.IsNullOrEmpty(element.FilePath))
                return NotFound();

            var filePath = Path.Combine(_storageSettings.StoragePath, element.FilePath);
            if (!System.IO.File.Exists(filePath))
                return NotFound();

            var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
            return File(fileBytes, GetContentType(element.FileName), element.FileName ?? "download");
        }

        public async Task<IActionResult> Preview(int elementId)
        {
            var element = await _context.EducationalProgramElements.FindAsync(elementId);
            if (element == null || string.IsNullOrEmpty(element.FilePath))
                return NotFound();

            var filePath = Path.Combine(_storageSettings.StoragePath, element.FilePath);
            if (!System.IO.File.Exists(filePath))
                return NotFound();

            var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
            Response.Headers.Append("Content-Disposition", $"inline; filename=\"{element.FileName ?? "preview"}\"");
            return File(fileBytes, GetContentType(element.FileName));
        }

        [HttpPost]
        public async Task<IActionResult> Publish(int elementId, string? comment)
        {
            var element = await _workflowService.ChangeStatusAsync(
                elementId,
                GetCurrentUserId(),
                ElementApprovalStatus.Published,
                comment ?? ElementApprovalStatus.Published,
                User.IsInRole(AppRoles.Admin),
                [ElementApprovalStatus.Approved]);

            if (element == null)
                return NotFound();

            return RedirectToAction(nameof(Index), new { programId = element.EducationalProgramId });
        }

        [HttpPost]
        public async Task<IActionResult> Unpublish(int elementId, string? comment)
        {
            var element = await _workflowService.ChangeStatusAsync(
                elementId,
                GetCurrentUserId(),
                ElementApprovalStatus.Approved,
                comment ?? "Снято с публикации",
                User.IsInRole(AppRoles.Admin),
                [ElementApprovalStatus.Published]);

            if (element == null)
                return NotFound();

            return RedirectToAction(nameof(Index), new { programId = element.EducationalProgramId });
        }

        [HttpPost]
        public async Task<IActionResult> AddComment(int elementId, string commentText)
        {
            if (string.IsNullOrWhiteSpace(commentText))
                return RedirectToAction(nameof(Index));

            _context.EducationalProgramElementComment.Add(new EducationalProgramElementComment
            {
                EducationalProgramElementId = elementId,
                UserId = GetCurrentUserId(),
                DateTimeComment = DateTime.Now,
                CommentContent = commentText,
                Status = CommentStatus.New
            });

            await _context.SaveChangesAsync();

            var element = await _context.EducationalProgramElements.FindAsync(elementId);
            return RedirectToAction(nameof(Index), new { programId = element?.EducationalProgramId ?? 1 });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateCommentStatus(int commentId, string status)
        {
            if (!CommentStatus.All.Contains(status))
                return BadRequest();

            var comment = await _context.EducationalProgramElementComment.FindAsync(commentId);
            if (comment == null)
                return NotFound();

            comment.Status = status;
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Comments), new { elementId = comment.EducationalProgramElementId });
        }

        public async Task<IActionResult> History(int elementId)
        {
            await FillElementDetailsViewBag(elementId, nameof(ModeratorHomeController).Replace("Controller", ""));
            return View("~/Views/ManagerHome/History.cshtml");
        }

        public async Task<IActionResult> Comments(int elementId)
        {
            await FillElementDetailsViewBag(elementId, nameof(ModeratorHomeController).Replace("Controller", ""));
            var comments = await GetElementComments(elementId);
            return View("~/Views/ManagerHome/Comments.cshtml", comments);
        }

        private async Task FillElementDetailsViewBag(int elementId, string returnController)
        {
            ViewBag.Element = await _context.EducationalProgramElements
                .Include(e => e.EducationalProgram)
                .FirstOrDefaultAsync(e => e.Id == elementId);
            ViewBag.History = await _context.ElementStatusHistory
                .Where(h => h.EducationalProgramElementId == elementId)
                .Include(h => h.User)
                .OrderByDescending(h => h.ChangeDate)
                .ToListAsync();
            ViewBag.Comments = await GetElementComments(elementId);
            ViewBag.ReturnController = returnController;
        }

        private async Task<List<EducationalProgramElementComment>> GetElementComments(int elementId)
        {
            return await _context.EducationalProgramElementComment
                .Where(c => c.EducationalProgramElementId == elementId)
                .Include(c => c.User)
                .OrderByDescending(c => c.DateTimeComment)
                .ToListAsync();
        }

        private static string GetContentType(string? fileName)
        {
            return Path.GetExtension(fileName).ToLowerInvariant() switch
            {
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                _ => "application/pdf"
            };
        }
    }
}
