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

        public CurriculumImportController(
            ApplicationDbContext context,
            ElementAccessService accessService,
            PlxParserService parser,
            PlxImportStorageService storage,
            CurriculumImportService importService)
        {
            _context = context;
            _accessService = accessService;
            _parser = parser;
            _storage = storage;
            _importService = importService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int programId, CancellationToken cancellationToken)
        {
            if (!await _accessService.CanManageProgramAsync(User, programId))
                return Forbid();

            var program = await _context.EducationalPrograms
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == programId && !item.IsArchived, cancellationToken);
            if (program == null)
                return NotFound();

            var imports = await _context.CurriculumImports
                .Where(item => item.EducationalProgramId == programId)
                .Include(item => item.ImportedByUser)
                .OrderByDescending(item => item.ImportedAt)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            return View(new CurriculumImportIndexViewModel
            {
                Program = program,
                Imports = imports
            });
        }

        [HttpPost]
        [RequestSizeLimit(PlxParserService.MaxPlxFileSizeBytes)]
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

            try
            {
                if (file == null)
                    throw new InvalidOperationException("Выберите файл учебного плана PLX.");
                if (!Path.GetExtension(file.FileName).Equals(".plx", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Для импорта требуется файл с расширением .plx.");
                if (file.Length == 0 || file.Length > PlxParserService.MaxPlxFileSizeBytes)
                    throw new InvalidOperationException("Размер файла PLX должен быть больше 0 и не превышать 20 МБ.");

                PlxImportPreview preview;
                await using (var stream = file.OpenReadStream())
                    preview = await _parser.ParseAsync(stream, cancellationToken);

                AddCompatibilityWarnings(program, preview);
                var staged = await _storage.StageAsync(file, GetCurrentUserId(), programId, cancellationToken);
                return View(new CurriculumImportPreviewViewModel
                {
                    Program = program,
                    Preview = preview,
                    Token = staged.Token,
                    OriginalFileName = staged.OriginalFileName
                });
            }
            catch (Exception ex) when (ex is InvalidOperationException or InvalidDataException or IOException or XmlException)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction(nameof(Index), new { programId });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Apply(
            int programId,
            string token,
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
                AddCompatibilityWarnings(program, preview);

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

        private void AddCompatibilityWarnings(EducationalProgram program, PlxImportPreview preview)
        {
            if (!string.IsNullOrWhiteSpace(preview.PlanCode) &&
                !program.CodeReferral.Equals(preview.PlanCode, StringComparison.CurrentCultureIgnoreCase))
            {
                preview.Warnings.Add($"Шифр в PLX ({preview.PlanCode}) отличается от шифра выбранной ОПОП ({program.CodeReferral}).");
            }

            if (!string.IsNullOrWhiteSpace(preview.EducationalLevel) &&
                !program.EducationalLevel.Contains(preview.EducationalLevel, StringComparison.CurrentCultureIgnoreCase) &&
                !preview.EducationalLevel.Contains(program.EducationalLevel, StringComparison.CurrentCultureIgnoreCase))
            {
                preview.Warnings.Add($"Уровень образования в PLX ({preview.EducationalLevel}) отличается от выбранной ОПОП ({program.EducationalLevel}).");
            }
        }

        private int GetCurrentUserId() =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new InvalidOperationException("Идентификатор пользователя отсутствует."));
    }
}
