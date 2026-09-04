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
        private readonly ElementAccessService _accessService;

        public ModeratorHomeController(
            IOptions<FileStorageSettings> storageSettings,
            ApplicationDbContext context,
            ElementWorkflowService workflowService,
            NotificationService notificationService,
            ElementListQueryService elementListQueryService,
            ElementAccessService accessService)
        {
            _storageSettings = storageSettings.Value;
            _context = context;
            _workflowService = workflowService;
            _notificationService = notificationService;
            _elementListQueryService = elementListQueryService;
            _accessService = accessService;
        }

        private int GetCurrentUserId()
        {
            return int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        }

        [AppRateLimit(AppRateLimitPolicies.Search)]
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

            if (programId.HasValue && programs.All(p => p.Id != programId.Value))
                return Forbid();

            int? selectedProgramId = programId ?? programs.FirstOrDefault()?.Id;

            var elementPage = await _elementListQueryService.GetAsync(selectedProgramId, tab, page, sort, direction, filters);

            ViewBag.Programs = programs;
            ViewBag.SelectedProgramId = selectedProgramId;
            ViewBag.ActiveTab = tab;
            ViewBag.Comments = await _context.EducationalProgramElementComment
                .Where(c => c.Element.EducationalProgramId == selectedProgramId)
                .Include(c => c.User)
                .ToListAsync();
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

        [HttpGet]
        public async Task<IActionResult> ManageFiles(int elementId)
        {
            if (!await _accessService.CanModerateElementAsync(User, elementId))
                return Forbid();

            await _notificationService.MarkElementReadAsync(GetCurrentUserId(), elementId);
            var element = await _context.EducationalProgramElements
                .Include(item => item.Files)
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == elementId &&
                                             !item.IsArchived &&
                                             !item.EducationalProgram.IsArchived);
            if (element == null)
                return NotFound();

            ViewBag.AllowFileEditing = false;
            ViewBag.ReturnController = nameof(ModeratorHomeController).Replace("Controller", "");
            return View("~/Views/ManagerHome/ManageFiles.cshtml", element);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AppRateLimit(AppRateLimitPolicies.FileDownload)]
        public async Task<IActionResult> Download(int elementId)
        {
            if (!await _accessService.CanModerateElementAsync(User, elementId))
                return Forbid();
            await _notificationService.MarkElementReadAsync(GetCurrentUserId(), elementId);
            var element = await _context.EducationalProgramElements
                .FirstOrDefaultAsync(e => e.Id == elementId && !e.IsArchived && !e.EducationalProgram.IsArchived);
            if (element == null || string.IsNullOrEmpty(element.FilePath))
                return NotFound();

            if (!SupportedDocumentFormats.IsSupported(element.FileName))
                return NotFound();

            var filePath = StoredFilePath.Resolve(_storageSettings.StoragePath, element.FilePath);
            if (filePath == null)
                return NotFound();

            return PhysicalFile(filePath, GetContentType(element.FileName), element.FileName ?? "download");
        }

        [AppRateLimit(AppRateLimitPolicies.FileDownload)]
        public async Task<IActionResult> Preview(int elementId)
        {
            if (!await _accessService.CanModerateElementAsync(User, elementId))
                return Forbid();
            await _notificationService.MarkElementReadAsync(GetCurrentUserId(), elementId);
            var element = await _context.EducationalProgramElements
                .FirstOrDefaultAsync(e => e.Id == elementId && !e.IsArchived && !e.EducationalProgram.IsArchived);
            if (element == null || string.IsNullOrEmpty(element.FilePath))
                return NotFound();

            if (!SupportedDocumentFormats.IsSupported(element.FileName))
                return NotFound();

            var filePath = StoredFilePath.Resolve(_storageSettings.StoragePath, element.FilePath);
            if (filePath == null)
                return NotFound();

            FileContentDisposition.SetInline(Response, element.FileName ?? "preview");
            return PhysicalFile(filePath, GetContentType(element.FileName));
        }

        [HttpPost]
        [AppRateLimit(AppRateLimitPolicies.WorkflowMutation)]
        public async Task<IActionResult> Publish(int elementId, string? comment)
        {
            if (!await _accessService.CanModerateElementAsync(User, elementId))
                return Forbid();
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
            catch (InvalidOperationException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction(nameof(Index));
            }

            if (element == null)
                return NotFound();

            return RedirectToAction(nameof(Index), new { programId = element.EducationalProgramId });
        }

        [HttpPost]
        [AppRateLimit(AppRateLimitPolicies.WorkflowMutation)]
        public async Task<IActionResult> Unpublish(int elementId, string? comment)
        {
            if (!await _accessService.CanModerateElementAsync(User, elementId))
                return Forbid();
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
            catch (InvalidOperationException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction(nameof(Index));
            }

            if (element == null)
                return NotFound();

            return RedirectToAction(nameof(Index), new { programId = element.EducationalProgramId });
        }


        public async Task<IActionResult> History(int elementId)
        {
            if (!await _accessService.CanModerateElementAsync(User, elementId))
                return Forbid();
            await _notificationService.MarkElementReadAsync(GetCurrentUserId(), elementId);
            await FillElementDetailsViewBag(elementId, nameof(ModeratorHomeController).Replace("Controller", ""));
            return View("~/Views/ManagerHome/History.cshtml");
        }

        public async Task<IActionResult> Comments(int elementId)
        {
            if (!await _accessService.CanModerateElementAsync(User, elementId))
                return Forbid();
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
            return SupportedDocumentFormats.PdfContentType;
        }
    }
}
