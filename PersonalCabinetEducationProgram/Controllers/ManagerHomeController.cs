using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PersonalCabinetEducationProgram.Data;
using PersonalCabinetEducationProgram.Models;
using PersonalCabinetEducationProgram.Services;
using PersonalCabinetEducationProgram.ViewModels;

namespace PersonalCabinetEducationProgram.Controllers
{
    [Authorize(Roles = "Manager,Admin")]
    public class ManagerHomeController : Controller
    {
        private readonly IFileStorageService _fileStorageService;
        private readonly FileStorageSettings _storageSettings;
        private readonly ApplicationDbContext _context;
        private readonly ElementWorkflowService _workflowService;
        private readonly NotificationService _notificationService;
        private readonly ElementAccessService _accessService;
        private readonly AuditService _auditService;
        private readonly ElementListQueryService _elementListQueryService;

        public ManagerHomeController(
            IFileStorageService fileStorageService,
            IOptions<FileStorageSettings> storageSettings,
            ApplicationDbContext context,
            ElementWorkflowService workflowService,
            NotificationService notificationService,
            ElementAccessService accessService,
            AuditService auditService,
            ElementListQueryService elementListQueryService)
        {
            _fileStorageService = fileStorageService;
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
            var currentUserId = GetCurrentUserId();

            var programs = await _context.EducationalPrograms
                .Where(p => !p.IsArchived && (User.IsInRole("Admin") || p.UserId == currentUserId || p.Managers.Any(m => m.UserId == currentUserId)))
                .Include(p => p.Assignments).ThenInclude(a => a.Department)
                .Include(p => p.Assignments).ThenInclude(a => a.Faculty)
                .Include(p => p.Managers)
                .ToListAsync();

            if (programId.HasValue && programs.All(p => p.Id != programId.Value))
                return Forbid();

            int? selectedProgramId = programId ?? programs.FirstOrDefault()?.Id;

            var elementPage = await _elementListQueryService.GetAsync(selectedProgramId, tab, page, sort, direction, filters);

            var comments = await _context.EducationalProgramElementComment
                .Where(c => c.Element.EducationalProgramId == selectedProgramId)
                .Include(c => c.User)
                .ToListAsync();

            ViewBag.Programs = programs;
            ViewBag.SelectedProgramId = selectedProgramId;
            ViewBag.ActiveTab = tab;
            ViewBag.Comments = comments;
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

        [HttpPost]
        public async Task<IActionResult> Upload(int elementId, List<IFormFile> files, bool returnToFiles = false)
        {
            var targetElement = await _context.EducationalProgramElements.FindAsync(elementId);
            int programId = targetElement?.EducationalProgramId ?? 1;

            if (!await _accessService.CanManageElementAsync(User, elementId))
                return Forbid();

            files = files.Where(f => f.Length > 0).ToList();
            if (files.Count == 0)
            {
                TempData["ErrorMessage"] = "Выберите файл для загрузки.";
                return RedirectAfterUpload(elementId, programId, returnToFiles);
            }

            if (files.Count > FileUploadLimits.MaxFilesPerGroup)
            {
                TempData["ErrorMessage"] = $"За один раз можно загрузить не более {FileUploadLimits.MaxFilesPerGroup} файлов.";
                return RedirectAfterUpload(elementId, programId, returnToFiles);
            }

            var currentFileCount = await _context.EducationalProgramElementFiles
                .CountAsync(f => f.EducationalProgramElementId == elementId && f.IsCurrent);
            if (currentFileCount + files.Count > FileUploadLimits.MaxFilesPerGroup)
            {
                TempData["ErrorMessage"] = $"В одной группе может быть не более {FileUploadLimits.MaxFilesPerGroup} файлов.";
                return RedirectAfterUpload(elementId, programId, returnToFiles);
            }

            try
            {
                foreach (var file in files)
                    await _fileStorageService.ValidateFileAsync(file);

                var storedFiles = new List<(string StoredFileName, string OriginalFileName)>();
                try
                {
                    foreach (var file in files)
                        storedFiles.Add((await _fileStorageService.SaveFileAsync(file), Path.GetFileName(file.FileName)));

                    var updatedElement = await _workflowService.MarkFilesUploadedAsync(elementId, GetCurrentUserId(), storedFiles);
                    if (updatedElement == null)
                        throw new InvalidOperationException("Элемент не найден или находится в архиве.");
                }
                catch
                {
                    foreach (var storedFile in storedFiles)
                        await _fileStorageService.DeleteFileAsync(storedFile.StoredFileName);
                    throw;
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or IOException or DbUpdateException)
            {
                TempData["ErrorMessage"] = ex is DbUpdateConcurrencyException
                    ? "Данные были изменены другим пользователем. Обновите страницу и повторите действие."
                    : ex.Message;
            }

            return RedirectAfterUpload(elementId, programId, returnToFiles);
        }

        public async Task<IActionResult> Download(int elementId)
        {
            if (!await _accessService.CanManageElementAsync(User, elementId))
                return Forbid();
            await _notificationService.MarkElementReadAsync(GetCurrentUserId(), elementId);
            var element = await _context.EducationalProgramElements.FindAsync(elementId);
            if (element == null || string.IsNullOrEmpty(element.FilePath))
                return NotFound();

            var safePath = StoredFilePath.Resolve(_storageSettings.StoragePath, element.FilePath);
            if (safePath == null)
                return NotFound();
            return PhysicalFile(safePath, GetContentType(element.FileName), element.FileName ?? "download");
        }

        public async Task<IActionResult> Preview(int elementId)
        {
            if (!await _accessService.CanManageElementAsync(User, elementId))
                return Forbid();
            await _notificationService.MarkElementReadAsync(GetCurrentUserId(), elementId);
            var element = await _context.EducationalProgramElements
                .Include(e => e.EducationalProgram)
                .FirstOrDefaultAsync(e => e.Id == elementId);

            if (element == null || string.IsNullOrEmpty(element.FilePath))
                return NotFound();

            var safePath = StoredFilePath.Resolve(_storageSettings.StoragePath, element.FilePath);
            if (safePath == null)
                return NotFound();
            Response.Headers.Append("Content-Disposition", $"inline; filename=\"{element.FileName ?? "preview.pdf"}\"");
            return PhysicalFile(safePath, GetContentType(element.FileName));
        }

        [HttpPost]
        public async Task<IActionResult> SendForApproval(int elementId)
        {
            if (!await _accessService.CanManageElementAsync(User, elementId))
                return Forbid();

            EducationalProgramElement? element;
            try
            {
                element = await _workflowService.SubmitForApprovalAsync(elementId, GetCurrentUserId());
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
            if (element == null) return NotFound();

            return RedirectToAction(nameof(Index), new { programId = element.EducationalProgramId });
        }

        [HttpPost]
        public async Task<IActionResult> AddComment(int elementId, string commentText)
        {
            if (!await _accessService.CanManageElementAsync(User, elementId))
                return Forbid();
            var validationError = EntityInputValidator.Comment(commentText);
            if (validationError != null)
            {
                TempData["ErrorMessage"] = validationError;
                return RedirectToAction(nameof(Index));
            }

            var comment = new EducationalProgramElementComment
            {
                EducationalProgramElementId = elementId,
                UserId = GetCurrentUserId(),
                DateTimeComment = DateTime.UtcNow,
                CommentContent = commentText.Trim(),
                Status = CommentStatus.New
            };

            _context.EducationalProgramElementComment.Add(comment);
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

            var comment = await _context.EducationalProgramElementComment
                .Include(c => c.Element)
                .FirstOrDefaultAsync(c => c.Id == commentId);

            if (comment == null)
                return NotFound();

            if (!await _accessService.CanManageElementAsync(User, comment.EducationalProgramElementId))
                return Forbid();

            comment.Status = status;
            _auditService.Record(GetCurrentUserId(), "EducationalProgramElement", comment.EducationalProgramElementId,
                "CommentStatusChanged", $"Комментарий {comment.Id}: {status}");
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Comments), new { elementId = comment.EducationalProgramElementId });
        }

        public async Task<IActionResult> History(int elementId)
        {
            if (!await _accessService.CanManageElementAsync(User, elementId))
                return Forbid();
            await _notificationService.MarkElementReadAsync(GetCurrentUserId(), elementId);
            var element = await _context.EducationalProgramElements
                .Include(e => e.EducationalProgram)
                .FirstOrDefaultAsync(e => e.Id == elementId);

            var history = await _context.ElementStatusHistory
                .Where(h => h.EducationalProgramElementId == elementId)
                .Include(h => h.User)
                .OrderByDescending(h => h.ChangeDate)
                .ToListAsync();

            var comments = await _context.EducationalProgramElementComment
                .Where(c => c.EducationalProgramElementId == elementId)
                .Include(c => c.User)
                .OrderByDescending(c => c.DateTimeComment)
                .ToListAsync();

            ViewBag.Element = element;
            ViewBag.History = history;
            ViewBag.Comments = comments;
            ViewBag.FileGroups = await _context.EducationalProgramElementFiles
                .Where(f => f.EducationalProgramElementId == elementId)
                .Include(f => f.UploadedByUser)
                .OrderByDescending(f => f.RevisionNumber)
                .ThenBy(f => f.OriginalFileName)
                .ToListAsync();
            ViewBag.ReturnController = nameof(ManagerHomeController).Replace("Controller", "");

            return View();
        }

        public async Task<IActionResult> Comments(int elementId)
        {
            if (!await _accessService.CanManageElementAsync(User, elementId))
                return Forbid();
            await _notificationService.MarkElementReadAsync(GetCurrentUserId(), elementId);
            var element = await _context.EducationalProgramElements
                .Include(e => e.EducationalProgram)
                .FirstOrDefaultAsync(e => e.Id == elementId);

            var comments = await _context.EducationalProgramElementComment
                .Where(c => c.EducationalProgramElementId == elementId)
                .Include(c => c.User)
                .OrderByDescending(c => c.DateTimeComment)
                .ToListAsync();

            ViewBag.Element = element;
            ViewBag.ReturnController = nameof(ManagerHomeController).Replace("Controller", "");
            return View(comments);
        }

        [HttpGet]
        public async Task<IActionResult> EditElement(int elementId)
        {
            if (!await _accessService.CanManageElementAsync(User, elementId))
                return Forbid();
            var element = await _context.EducationalProgramElements
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == elementId && !e.IsArchived);
            if (element == null)
                return NotFound();
            return View(element);
        }

        [HttpPost]
        public async Task<IActionResult> EditElement(int elementId, int version, string name, string? description)
        {
            if (!await _accessService.CanManageElementAsync(User, elementId))
                return Forbid();
            var element = await _context.EducationalProgramElements.FindAsync(elementId);
            if (element == null || element.IsArchived)
                return NotFound();
            var status = ElementApprovalStatus.Normalize(element.StatusApprovals);
            if (status is ElementApprovalStatus.OnApproval or ElementApprovalStatus.Approved or ElementApprovalStatus.Published)
            {
                TempData["ErrorMessage"] = "Карточку зафиксированного элемента нельзя изменять.";
                return RedirectToAction(nameof(Index), new { programId = element.EducationalProgramId });
            }
            if (element.Version != version)
            {
                TempData["ErrorMessage"] = "Элемент уже изменён. Обновите страницу и повторите действие.";
                return RedirectToAction(nameof(EditElement), new { elementId });
            }
            var validationError = EntityInputValidator.Element(name, description);
            if (validationError != null)
            {
                TempData["ErrorMessage"] = validationError;
                return RedirectToAction(nameof(EditElement), new { elementId });
            }

            var oldValue = $"{element.Name} ({element.Description})";
            element.Name = name.Trim();
            element.Description = description?.Trim() ?? string.Empty;
            element.Version++;
            _context.ElementStatusHistory.Add(new ElementStatusHistory
            {
                EducationalProgramElementId = element.Id,
                UserId = GetCurrentUserId(),
                OldStatus = status,
                NewStatus = status,
                ChangeDate = DateTime.UtcNow,
                Comment = $"Изменена карточка элемента. Было: {oldValue}"
            });
            _auditService.Record(GetCurrentUserId(), "EducationalProgramElement", element.Id,
                "ElementEdited", $"{oldValue} -> {element.Name} ({element.Description})");
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                TempData["ErrorMessage"] = "Элемент уже изменён другим пользователем. Обновите страницу и повторите действие.";
                return RedirectToAction(nameof(EditElement), new { elementId });
            }
            return RedirectToAction(nameof(Index), new { programId = element.EducationalProgramId });
        }

        [HttpGet]
        public async Task<IActionResult> ManageFiles(int elementId)
        {
            if (!await _accessService.CanManageElementAsync(User, elementId))
                return Forbid();
            var element = await _context.EducationalProgramElements
                .Include(e => e.Files)
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == elementId && !e.IsArchived);
            if (element == null)
                return NotFound();
            return View(element);
        }

        [HttpPost]
        public async Task<IActionResult> RemoveCurrentFile(int fileId)
        {
            var elementId = await _context.EducationalProgramElementFiles
                .Where(f => f.Id == fileId)
                .Select(f => f.EducationalProgramElementId)
                .FirstOrDefaultAsync();
            if (elementId == 0)
                return NotFound();
            if (!await _accessService.CanManageElementAsync(User, elementId))
                return Forbid();
            try
            {
                var element = await _workflowService.RemoveCurrentFileAsync(fileId, GetCurrentUserId());
                return RedirectToAction(nameof(ManageFiles), new { elementId = element?.Id ?? elementId });
            }
            catch (Exception ex) when (ex is InvalidOperationException or DbUpdateConcurrencyException)
            {
                TempData["ErrorMessage"] = ex is DbUpdateConcurrencyException
                    ? "Файл или элемент уже изменён другим пользователем. Обновите страницу и повторите действие."
                    : ex.Message;
                return RedirectToAction(nameof(ManageFiles), new { elementId });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ReplaceCurrentFile(int fileId, IFormFile? file)
        {
            var elementId = await _context.EducationalProgramElementFiles
                .Where(f => f.Id == fileId)
                .Select(f => f.EducationalProgramElementId)
                .FirstOrDefaultAsync();
            if (elementId == 0)
                return NotFound();
            if (!await _accessService.CanManageElementAsync(User, elementId))
                return Forbid();
            if (file == null)
            {
                TempData["ErrorMessage"] = "Выберите новый файл.";
                return RedirectToAction(nameof(ManageFiles), new { elementId });
            }

            string? storedFileName = null;
            try
            {
                await _fileStorageService.ValidateFileAsync(file);
                storedFileName = await _fileStorageService.SaveFileAsync(file);
                await _workflowService.ReplaceCurrentFileAsync(
                    fileId, GetCurrentUserId(), storedFileName, Path.GetFileName(file.FileName));
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or IOException or DbUpdateException)
            {
                if (storedFileName != null)
                    await _fileStorageService.DeleteFileAsync(storedFileName);
                TempData["ErrorMessage"] = ex.Message;
            }
            return RedirectToAction(nameof(ManageFiles), new { elementId });
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

        private IActionResult RedirectAfterUpload(int elementId, int programId, bool returnToFiles) =>
            returnToFiles
                ? RedirectToAction(nameof(ManageFiles), new { elementId })
                : RedirectToAction(nameof(Index), new { programId });

    }
}
