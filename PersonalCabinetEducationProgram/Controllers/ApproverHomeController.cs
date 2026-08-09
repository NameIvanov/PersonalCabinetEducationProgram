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
    [Authorize(Roles = "Approver,Admin")]
    public class ApproverHomeController : Controller
    {
        private readonly FileStorageSettings _storageSettings;
        private readonly ApplicationDbContext _context;
        private readonly ElementWorkflowService _workflowService;
        private readonly NotificationService _notificationService;
        private readonly ElementAccessService _accessService;
        private readonly AuditService _auditService;
        private readonly ElementListQueryService _elementListQueryService;

        public ApproverHomeController(
            IOptions<FileStorageSettings> storageSettings,
            ApplicationDbContext context,
            ElementWorkflowService workflowService,
            NotificationService notificationService,
            ElementAccessService accessService,
            AuditService auditService,
            ElementListQueryService elementListQueryService)
        {
            _storageSettings = storageSettings.Value;
            _context = context;
            _workflowService = workflowService;
            _notificationService = notificationService;
            _accessService = accessService;
            _auditService = auditService;
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

            if (!User.IsInRole(AppRoles.Admin))
            {
                var currentUserId = GetCurrentUserId();
                var approverAssignments = await _context.ApproverAssignments
                    .Where(a => a.ApproverUserId == currentUserId)
                    .ToListAsync();

                programs = programs.Where(p =>
                    approverAssignments.Any(a => p.Assignments.Any(pa =>
                        (a.FacultyId != null && pa.FacultyId == a.FacultyId) ||
                        (a.DepartmentId != null && pa.DepartmentId == a.DepartmentId))))
                    .ToList();
            }

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

        public async Task<IActionResult> Download(int elementId)
        {
            if (!await _accessService.CanApproveElementAsync(User, elementId))
                return Forbid();
            await _notificationService.MarkElementReadAsync(GetCurrentUserId(), elementId);
            var element = await _context.EducationalProgramElements.FindAsync(elementId);
            if (element == null || string.IsNullOrEmpty(element.FilePath))
                return NotFound();

            var filePath = StoredFilePath.Resolve(_storageSettings.StoragePath, element.FilePath);
            if (filePath == null)
                return NotFound();

            return PhysicalFile(filePath, GetContentType(element.FileName), element.FileName ?? "download");
        }

        public async Task<IActionResult> Preview(int elementId)
        {
            if (!await _accessService.CanApproveElementAsync(User, elementId))
                return Forbid();
            await _notificationService.MarkElementReadAsync(GetCurrentUserId(), elementId);
            var element = await _context.EducationalProgramElements.FindAsync(elementId);
            if (element == null || string.IsNullOrEmpty(element.FilePath))
                return NotFound();

            var filePath = StoredFilePath.Resolve(_storageSettings.StoragePath, element.FilePath);
            if (filePath == null)
                return NotFound();

            Response.Headers.Append("Content-Disposition", $"inline; filename=\"{element.FileName ?? "preview"}\"");
            return PhysicalFile(filePath, GetContentType(element.FileName));
        }

        [HttpPost]
        public async Task<IActionResult> Approve(int elementId, string? comment)
        {
            if (!await _accessService.CanApproveElementAsync(User, elementId))
                return Forbid();
            EducationalProgramElement? element;
            try
            {
                element = await _workflowService.ChangeStatusAsync(
                    elementId, GetCurrentUserId(), ElementApprovalStatus.Approved,
                    comment ?? ElementApprovalStatus.Approved, User.IsInRole(AppRoles.Admin),
                    ElementApprovalStatus.ApproverCanApprove);
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
        public async Task<IActionResult> Reject(int elementId, string? comment)
        {
            if (!await _accessService.CanApproveElementAsync(User, elementId))
                return Forbid();
            EducationalProgramElement? element;
            try
            {
                element = await _workflowService.ChangeStatusAsync(
                    elementId, GetCurrentUserId(), ElementApprovalStatus.RevisionRequired,
                    comment ?? "Отправлено на доработку", User.IsInRole(AppRoles.Admin),
                    ElementApprovalStatus.ApproverCanReject);
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
        public async Task<IActionResult> AddComment(int elementId, string commentText)
        {
            if (!await _accessService.CanApproveElementAsync(User, elementId))
                return Forbid();
            var validationError = EntityInputValidator.Comment(commentText);
            if (validationError != null)
            {
                TempData["ErrorMessage"] = validationError;
                return RedirectToAction(nameof(Index));
            }

            _context.EducationalProgramElementComment.Add(new EducationalProgramElementComment
            {
                EducationalProgramElementId = elementId,
                UserId = GetCurrentUserId(),
                DateTimeComment = DateTime.UtcNow,
                CommentContent = commentText.Trim(),
                Status = CommentStatus.New
            });
            _auditService.Record(GetCurrentUserId(), "EducationalProgramElement", elementId, "CommentAdded", commentText.Trim());

            await _notificationService.CreateForElementAsync(
                elementId,
                GetCurrentUserId(),
                NotificationType.CommentAdded,
                "Добавлен комментарий",
                commentText.Trim());
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

            if (!await _accessService.CanApproveElementAsync(User, comment.EducationalProgramElementId))
                return Forbid();

            comment.Status = status;
            _auditService.Record(GetCurrentUserId(), "EducationalProgramElement", comment.EducationalProgramElementId,
                "CommentStatusChanged", $"Комментарий {comment.Id}: {status}");
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Comments), new { elementId = comment.EducationalProgramElementId });
        }

        public async Task<IActionResult> History(int elementId)
        {
            if (!await _accessService.CanApproveElementAsync(User, elementId))
                return Forbid();
            await _notificationService.MarkElementReadAsync(GetCurrentUserId(), elementId);
            await FillElementDetailsViewBag(elementId, nameof(ApproverHomeController).Replace("Controller", ""));
            return View("~/Views/ManagerHome/History.cshtml");
        }

        public async Task<IActionResult> Comments(int elementId)
        {
            if (!await _accessService.CanApproveElementAsync(User, elementId))
                return Forbid();
            await _notificationService.MarkElementReadAsync(GetCurrentUserId(), elementId);
            await FillElementDetailsViewBag(elementId, nameof(ApproverHomeController).Replace("Controller", ""));
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
