using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PersonalCabinetEducationProgram.Data;
using PersonalCabinetEducationProgram.Models;
using PersonalCabinetEducationProgram.Services;
using PersonalCabinetEducationProgram.ViewModels;

namespace PersonalCabinetEducationProgram.Controllers
{
    [Authorize(Roles = "Moderator,Admin")]
    public class ModeratorHomeController : Controller
    {
        private readonly FileStorageSettings _storageSettings;
        private readonly ApplicationDbContext _context;
        private readonly ElementWorkflowService _workflowService;
        private readonly NotificationService _notificationService;
        private readonly ElementListQueryService _elementListQueryService;

        public ModeratorHomeController(
            IOptions<FileStorageSettings> storageSettings,
            ApplicationDbContext context,
            ElementWorkflowService workflowService,
            NotificationService notificationService,
            ElementListQueryService elementListQueryService)
        {
            _storageSettings = storageSettings.Value;
            _context = context;
            _workflowService = workflowService;
            _notificationService = notificationService;
            _elementListQueryService = elementListQueryService;
        }

        private int GetCurrentUserId()
        {
            return int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        }

        public async Task<IActionResult> Index(
            int? programId, string tab = "disciplines", int page = 1,
            string sort = "name", string direction = "asc",
            [FromQuery] ElementListFiltersViewModel? filters = null)
        {
            filters ??= new ElementListFiltersViewModel();
            var programs = await _context.EducationalPrograms
                .Where(p => !p.IsArchived)
                .Include(p => p.Assignments).ThenInclude(a => a.Department)
                .Include(p => p.Assignments).ThenInclude(a => a.Faculty)
                .ToListAsync();

            int? selectedProgramId = programId.HasValue && programs.Any(p => p.Id == programId.Value)
                ? programId
                : programs.FirstOrDefault()?.Id;

            var elementPage = await _elementListQueryService.GetAsync(selectedProgramId, tab, page, sort, direction, filters);

            ViewBag.Programs = programs;
            ViewBag.SelectedProgramId = selectedProgramId;
            ViewBag.ActiveTab = tab;
            ViewBag.Comments = await _context.EducationalProgramElementComment.Include(c => c.User).ToListAsync();
            FillElementPagination(elementPage);

            return View(elementPage.Elements);
        }

        private void FillElementPagination(PersonalCabinetEducationProgram.ViewModels.ElementListPageViewModel result)
        {
            ViewBag.ElementStatuses = result.Statuses;
            ViewBag.ElementPage = result.Page;
            ViewBag.ElementTotalPages = result.TotalPages;
            ViewBag.ElementSort = result.Sort;
            ViewBag.ElementDirection = result.Direction;
            ViewBag.ElementFilters = result.Filters;
        }

        public async Task<IActionResult> Download(int elementId)
        {
            await _notificationService.MarkElementReadAsync(GetCurrentUserId(), elementId);
            var element = await _context.EducationalProgramElements
                .FirstOrDefaultAsync(e => e.Id == elementId && !e.IsArchived && !e.EducationalProgram.IsArchived);
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
            await _notificationService.MarkElementReadAsync(GetCurrentUserId(), elementId);
            var element = await _context.EducationalProgramElements
                .FirstOrDefaultAsync(e => e.Id == elementId && !e.IsArchived && !e.EducationalProgram.IsArchived);
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
            EducationalProgramElement? element;
            try
            {
                element = await _workflowService.ChangeStatusAsync(
                    elementId, GetCurrentUserId(), ElementApprovalStatus.Published,
                    comment ?? ElementApprovalStatus.Published, User.IsInRole(AppRoles.Admin),
                    [ElementApprovalStatus.Approved]);
            }
            catch (DbUpdateConcurrencyException)
            {
                TempData["ErrorMessage"] = "Элемент уже изменён другим пользователем. Обновите страницу и повторите действие.";
                return RedirectToAction(nameof(Index));
            }

            if (element == null)
                return NotFound();

            return RedirectToAction(nameof(Index), new { programId = element.EducationalProgramId });
        }

        [HttpPost]
        public async Task<IActionResult> Unpublish(int elementId, string? comment)
        {
            EducationalProgramElement? element;
            try
            {
                element = await _workflowService.ChangeStatusAsync(
                    elementId, GetCurrentUserId(), ElementApprovalStatus.Approved,
                    comment ?? "Снято с публикации", User.IsInRole(AppRoles.Admin),
                    [ElementApprovalStatus.Published]);
            }
            catch (DbUpdateConcurrencyException)
            {
                TempData["ErrorMessage"] = "Элемент уже изменён другим пользователем. Обновите страницу и повторите действие.";
                return RedirectToAction(nameof(Index));
            }

            if (element == null)
                return NotFound();

            return RedirectToAction(nameof(Index), new { programId = element.EducationalProgramId });
        }


        public async Task<IActionResult> History(int elementId)
        {
            await _notificationService.MarkElementReadAsync(GetCurrentUserId(), elementId);
            await FillElementDetailsViewBag(elementId, nameof(ModeratorHomeController).Replace("Controller", ""));
            return View("~/Views/ManagerHome/History.cshtml");
        }

        public async Task<IActionResult> Comments(int elementId)
        {
            await _notificationService.MarkElementReadAsync(GetCurrentUserId(), elementId);
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
            ViewBag.FileGroups = await _context.EducationalProgramElementFiles
                .Where(f => f.EducationalProgramElementId == elementId)
                .Include(f => f.UploadedByUser)
                .OrderByDescending(f => f.RevisionNumber)
                .ThenBy(f => f.OriginalFileName)
                .ToListAsync();
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
            return Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant() switch
            {
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                _ => "application/pdf"
            };
        }
    }
}
