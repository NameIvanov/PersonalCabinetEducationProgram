using System.Security.Claims;
using System.Xml;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PersonalCabinetEducationProgram.Data;
using PersonalCabinetEducationProgram.Models;
using PersonalCabinetEducationProgram.Services;
using PersonalCabinetEducationProgram.ViewModels;

namespace PersonalCabinetEducationProgram.Controllers
{
    [Authorize(Roles = "Manager,Admin")]
    public sealed class CurriculumImportController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ElementAccessService _accessService;
        private readonly PlxParserService _parser;
        private readonly PlxImportStorageService _storage;
        private readonly CurriculumImportService _importService;
        private readonly SecurityEventService _securityEventService;
        private readonly AccountSecurityService _accountSecurityService;

        public CurriculumImportController(
            ApplicationDbContext context,
            ElementAccessService accessService,
            PlxParserService parser,
            PlxImportStorageService storage,
            CurriculumImportService importService,
            SecurityEventService securityEventService,
            AccountSecurityService accountSecurityService)
        {
            _context = context;
            _accessService = accessService;
            _parser = parser;
            _storage = storage;
            _importService = importService;
            _securityEventService = securityEventService;
            _accountSecurityService = accountSecurityService;
        }

        [HttpGet]
        [AppRateLimit(AppRateLimitPolicies.Search)]
        public async Task<IActionResult> Index(
            int programId,
            int page = 1,
            string sort = "date",
            string direction = "desc",
            [FromQuery] CurriculumImportListFiltersViewModel? filters = null,
            CancellationToken cancellationToken = default)
        {
            if (!await _accessService.CanManageProgramAsync(User, programId))
                return Forbid();

            var program = await _context.EducationalPrograms
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == programId && !item.IsArchived, cancellationToken);
            if (program == null)
                return NotFound();

            filters ??= new CurriculumImportListFiltersViewModel();
            const int pageSize = 25;
            page = Math.Max(1, page);
            var allImports = await _context.CurriculumImports
                .Where(item => item.EducationalProgramId == programId)
                .Include(item => item.ImportedByUser)
                .AsNoTracking()
                .ToListAsync(cancellationToken);
            IEnumerable<CurriculumImport> query = allImports.Where(item =>
                ListFilterMatcher.Text(item.OriginalFileName, filters.FileName) &&
                ListFilterMatcher.Text(item.PlanCode, filters.PlanCode) &&
                ListFilterMatcher.Text(item.ImportedByUser?.FullName, filters.Author) &&
                ListFilterMatcher.Date(item.ImportedAt, filters.DateFrom, filters.DateTo));
            var descending = direction.Equals("desc", StringComparison.OrdinalIgnoreCase);
            query = sort switch
            {
                "file" => descending ? query.OrderByDescending(i => i.OriginalFileName) : query.OrderBy(i => i.OriginalFileName),
                "author" => descending ? query.OrderByDescending(i => i.ImportedByUser.FullName) : query.OrderBy(i => i.ImportedByUser.FullName),
                "code" => descending ? query.OrderByDescending(i => i.PlanCode) : query.OrderBy(i => i.PlanCode),
                "created" => descending ? query.OrderByDescending(i => i.CreatedCount) : query.OrderBy(i => i.CreatedCount),
                "updated" => descending ? query.OrderByDescending(i => i.UpdatedCount) : query.OrderBy(i => i.UpdatedCount),
                "archived" => descending ? query.OrderByDescending(i => i.ArchivedCount) : query.OrderBy(i => i.ArchivedCount),
                "skipped" => descending ? query.OrderByDescending(i => i.SkippedCount) : query.OrderBy(i => i.SkippedCount),
                "warnings" => descending ? query.OrderByDescending(i => i.Warnings.Count) : query.OrderBy(i => i.Warnings.Count),
                _ => descending ? query.OrderByDescending(i => i.ImportedAt) : query.OrderBy(i => i.ImportedAt)
            };
            var totalCount = query.Count();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            page = Math.Min(page, totalPages);
            var imports = query.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            return View(new CurriculumImportIndexViewModel
            {
                Program = program,
                Imports = imports,
                Filters = filters,
                Page = page,
                TotalPages = totalPages,
                Sort = sort,
                Direction = descending ? "desc" : "asc"
            });
        }

        [HttpPost]
        [RequestSizeLimit(PlxParserService.MaxPlxRequestSizeBytes)]
        [AppRateLimit(AppRateLimitPolicies.PlxPreview)]
        public async Task<IActionResult> Preview(
            int programId,
            IFormFile? file,
            CancellationToken cancellationToken)
        {
            if (!await _accessService.CanManageProgramAsync(User, programId))
                return Forbid();

            var program = await _context.EducationalPrograms
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == programId && !item.IsArchived, cancellationToken);
            if (program == null)
                return NotFound();

            var invalidFormatAttempt = false;
            try
            {
                if (file == null)
                    throw new InvalidOperationException("Выберите файл учебного плана PLX.");
                if (!Path.GetExtension(file.FileName).Equals(".plx", StringComparison.OrdinalIgnoreCase))
                {
                    invalidFormatAttempt = true;
                    throw new InvalidOperationException("Для импорта требуется файл с расширением .plx.");
                }
                if (file.Length == 0 || file.Length > PlxParserService.MaxPlxFileSizeBytes)
                    throw new InvalidOperationException("Размер файла PLX должен быть больше 0 и не превышать 20 МБ.");

                _accountSecurityService.RecordPlxUpload(file);

                PlxImportPreview preview;
                await using (var stream = file.OpenReadStream())
                    preview = await _parser.ParseAsync(stream, cancellationToken);

                await _accountSecurityService.ResetInvalidUploadSequenceAsync(cancellationToken);

                var requiresConfirmation = AddCompatibilityWarnings(program, preview);
                var staged = await _storage.StageAsync(file, GetCurrentUserId(), programId, cancellationToken);
                return View(new CurriculumImportPreviewViewModel
                {
                    Program = program,
                    Preview = preview,
                    Token = staged.Token,
                    OriginalFileName = staged.OriginalFileName,
                    RequiresMismatchConfirmation = requiresConfirmation
                });
            }
            catch (Exception ex) when (ex is InvalidOperationException or InvalidDataException or IOException or XmlException)
            {
                await _accountSecurityService.RecordInvalidUploadAsync(
                    file?.FileName,
                    file?.Length ?? 0,
                    ex.Message,
                    invalidFormatAttempt || ex is InvalidDataException or XmlException,
                    cancellationToken);
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction(nameof(Index), new { programId });
            }
        }

        [HttpPost]
        [AppRateLimit(AppRateLimitPolicies.PlxApply)]
        public async Task<IActionResult> Apply(
            int programId,
            string token,
            bool confirmMismatch,
            CancellationToken cancellationToken)
        {
            if (!await _accessService.CanManageProgramAsync(User, programId))
                return Forbid();

            StagedPlxFile? staged = null;
            string? storedPath = null;
            try
            {
                staged = await _storage.GetStagedAsync(token, GetCurrentUserId(), programId, cancellationToken);
                PlxImportPreview preview;
                await using (var stream = new FileStream(staged.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    preview = await _parser.ParseAsync(stream, cancellationToken);

                var program = await _context.EducationalPrograms
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.Id == programId && !item.IsArchived, cancellationToken)
                    ?? throw new InvalidOperationException("ОПОП не найдена или находится в архиве.");
                var requiresConfirmation = AddCompatibilityWarnings(program, preview);
                if (requiresConfirmation && !confirmMismatch)
                    throw new InvalidOperationException("Импорт отменён: подтвердите отдельно несовпадение данных PLX и выбранной ОПОП.");

                storedPath = await _storage.CopyToArchiveAsync(staged, cancellationToken);
                var result = await _importService.ApplyAsync(
                    programId,
                    GetCurrentUserId(),
                    preview,
                    staged.OriginalFileName,
                    storedPath,
                    cancellationToken);

                _storage.DeleteStaged(staged);
                TempData["SuccessMessage"] =
                    $"PLX импортирован. Создано: {result.CreatedCount}, обновлено: {result.UpdatedCount}, перенесено в архив: {result.ArchivedCount}, пропущено: {result.SkippedCount}.";
                if (result.Warnings.Count > 0)
                    TempData["WarningMessage"] = string.Join(" ", result.Warnings);
            }
            catch (Exception ex) when (ex is InvalidOperationException or InvalidDataException or IOException or DbUpdateException)
            {
                if (!ex.Message.StartsWith("Импорт отменён:", StringComparison.Ordinal))
                {
                    _securityEventService.Record(
                        SecurityEventTypes.InvalidRequest,
                        SecurityEventSeverities.Warning,
                        "Не удалось применить импорт PLX",
                        ex.Message);
                }
                if (!string.IsNullOrWhiteSpace(storedPath))
                    _storage.DeleteStored(storedPath);
                TempData["ErrorMessage"] = ex.Message;
            }
            catch
            {
                if (!string.IsNullOrWhiteSpace(storedPath))
                    _storage.DeleteStored(storedPath);
                throw;
            }

            return RedirectToAction(nameof(Index), new { programId });
        }

        [HttpGet]
        [AppRateLimit(AppRateLimitPolicies.FileDownload)]
        public async Task<IActionResult> Download(int id, CancellationToken cancellationToken)
        {
            var import = await _context.CurriculumImports
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (import == null)
                return NotFound();
            if (!await _accessService.CanManageProgramAsync(User, import.EducationalProgramId))
                return Forbid();

            var filePath = _storage.ResolveStoredPath(import.StoredFilePath);
            if (!System.IO.File.Exists(filePath))
                return NotFound();
            return PhysicalFile(filePath, "application/xml", import.OriginalFileName);
        }

        private bool AddCompatibilityWarnings(EducationalProgram program, PlxImportPreview preview)
        {
            var mismatch = false;
            if (!string.IsNullOrWhiteSpace(preview.PlanCode) &&
                !program.CodeReferral.Equals(preview.PlanCode, StringComparison.CurrentCultureIgnoreCase))
            {
                mismatch = true;
                preview.Warnings.Add($"Шифр в PLX ({preview.PlanCode}) отличается от шифра выбранной ОПОП ({program.CodeReferral}).");
            }

            if (!string.IsNullOrWhiteSpace(preview.EducationalLevel) &&
                !program.EducationalLevel.Contains(preview.EducationalLevel, StringComparison.CurrentCultureIgnoreCase) &&
                !preview.EducationalLevel.Contains(program.EducationalLevel, StringComparison.CurrentCultureIgnoreCase))
            {
                mismatch = true;
                preview.Warnings.Add($"Уровень образования в PLX ({preview.EducationalLevel}) отличается от выбранной ОПОП ({program.EducationalLevel}).");
            }
            return mismatch;
        }

        private int GetCurrentUserId() =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new InvalidOperationException("Идентификатор пользователя отсутствует."));
    }
}
