using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PersonalCabinetEducationProgram.Data;
using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Services
{
    public sealed class CurriculumImportService
    {
        public const string ExternalSource = "PLX";
        private readonly ApplicationDbContext _context;
        private readonly AuditService _auditService;

        public CurriculumImportService(ApplicationDbContext context, AuditService auditService)
        {
            _context = context;
            _auditService = auditService;
        }

        public async Task<CurriculumImportResult> ApplyAsync(
            int programId,
            int userId,
            PlxImportPreview preview,
            string originalFileName,
            string storedFilePath,
            CancellationToken cancellationToken = default)
        {
            var program = await _context.EducationalPrograms
                .Include(item => item.Elements)
                .FirstOrDefaultAsync(item => item.Id == programId && !item.IsArchived, cancellationToken)
                ?? throw new InvalidOperationException("ОПОП не найдена или находится в архиве.");

            if (preview.Elements.Count == 0)
                throw new InvalidOperationException("В PLX не найдено элементов для импорта.");

            var duplicateKey = preview.Elements
                .GroupBy(item => item.ExternalKey, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(group => group.Count() > 1);
            if (duplicateKey != null)
                throw new InvalidDataException($"В PLX обнаружен повторяющийся ключ элемента: {duplicateKey.Key}.");

            var importedAt = DateTime.UtcNow;
            var warnings = new List<string>(preview.Warnings);
            var created = new List<EducationalProgramElement>();
            var createdCount = 0;
            var updatedCount = 0;
            var archivedCount = 0;
            var skippedCount = 0;
            var activeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var importedByKey = program.Elements
                .Where(element => element.ExternalSource == ExternalSource && !string.IsNullOrWhiteSpace(element.ExternalKey))
                .GroupBy(element => element.ExternalKey!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            await using var transaction = _context.Database.IsRelational()
                ? await _context.Database.BeginTransactionAsync(cancellationToken)
                : null;
            try
            {
                foreach (var candidate in preview.Elements)
                {
                    activeKeys.Add(candidate.ExternalKey);
                    if (!importedByKey.TryGetValue(candidate.ExternalKey, out var element))
                        element = FindManualMatch(program.Elements, candidate);

                    if (element == null)
                    {
                        element = new EducationalProgramElement
                        {
                            EducationalProgramId = programId,
                            TypeElement = candidate.TypeElement,
                            Name = candidate.Name,
                            Description = candidate.Code,
                            StatusApprovals = ElementApprovalStatus.NotUploaded,
                            ExternalSource = ExternalSource,
                            ExternalKey = candidate.ExternalKey,
                            ParentExternalKey = candidate.ParentExternalKey,
                            LastImportedAt = importedAt
                        };
                        program.Elements.Add(element);
                        created.Add(element);
                        createdCount++;
                        continue;
                    }

                    var isManualMatch = string.IsNullOrWhiteSpace(element.ExternalKey);
                    var hasVisibleChanges =
                        element.TypeElement != candidate.TypeElement ||
                        element.Name != candidate.Name ||
                        element.Description != candidate.Code ||
                        element.ParentExternalKey != candidate.ParentExternalKey ||
                        element.IsArchived;

                    if (IsLocked(element) && (hasVisibleChanges || isManualMatch))
                    {
                        warnings.Add($"Элемент «{element.Name}» не изменён, так как имеет статус «{ElementApprovalStatus.Normalize(element.StatusApprovals)}».");
                        skippedCount++;
                        continue;
                    }

                    if (hasVisibleChanges || isManualMatch)
                    {
                        var action = isManualMatch ? "связан с импортом PLX" : "синхронизирован с PLX";
                        element.TypeElement = candidate.TypeElement;
                        element.Name = candidate.Name;
                        element.Description = candidate.Code;
                        element.IsArchived = false;
                        element.ArchivedAt = null;
                        element.ArchivedByUserId = null;
                        element.ExternalSource = ExternalSource;
                        element.ExternalKey = candidate.ExternalKey;
                        element.ParentExternalKey = candidate.ParentExternalKey;
                        element.Version++;
                        AddHistory(element, userId, $"Элемент {action} при импорте «{originalFileName}».");
                        updatedCount++;
                    }

                    if (!IsLocked(element))
                        element.LastImportedAt = importedAt;
                }

                foreach (var element in program.Elements.Where(element =>
                             element.ExternalSource == ExternalSource &&
                             !string.IsNullOrWhiteSpace(element.ExternalKey) &&
                             !activeKeys.Contains(element.ExternalKey!) &&
                             !element.IsArchived))
                {
                    if (IsLocked(element))
                    {
                        warnings.Add($"Элемент «{element.Name}» отсутствует в новом PLX, но оставлен активным из-за статуса «{ElementApprovalStatus.Normalize(element.StatusApprovals)}».");
                        skippedCount++;
                        continue;
                    }

                    element.IsArchived = true;
                    element.ArchivedAt = importedAt;
                    element.ArchivedByUserId = userId;
                    element.LastImportedAt = importedAt;
                    element.Version++;
                    AddHistory(element, userId, $"Элемент перенесён в архив: он отсутствует в импортированном плане «{originalFileName}».");
                    archivedCount++;
                }

                if (createdCount + updatedCount + archivedCount > 0)
                    program.Version++;

                program.Status = ElementWorkflowService.CalculateProgramStatus(
                    program.Elements.Where(element => !element.IsArchived).Select(element => element.StatusApprovals));

                await _context.SaveChangesAsync(cancellationToken);

                foreach (var element in created)
                    AddHistory(element, userId, $"Элемент создан при импорте учебного плана «{originalFileName}».");

                var import = new CurriculumImport
                {
                    EducationalProgramId = programId,
                    ImportedByUserId = userId,
                    OriginalFileName = Path.GetFileName(originalFileName),
                    StoredFilePath = storedFilePath,
                    ImportedAt = importedAt,
                    PlanCode = preview.PlanCode,
                    PlanName = preview.PlanName,
                    SourceAppVersion = preview.SourceAppVersion,
                    CreatedCount = createdCount,
                    UpdatedCount = updatedCount,
                    ArchivedCount = archivedCount,
                    SkippedCount = skippedCount,
                    WarningsJson = JsonSerializer.Serialize(warnings.Distinct().ToList())
                };
                _context.CurriculumImports.Add(import);
                _auditService.Record(
                    userId,
                    "EducationalProgram",
                    programId,
                    "PlxImported",
                    $"Файл: {import.OriginalFileName}; создано: {createdCount}; обновлено: {updatedCount}; архивировано: {archivedCount}; пропущено: {skippedCount}.");

                await _context.SaveChangesAsync(cancellationToken);
                if (transaction != null)
                    await transaction.CommitAsync(cancellationToken);

                return new CurriculumImportResult
                {
                    ImportId = import.Id,
                    CreatedCount = createdCount,
                    UpdatedCount = updatedCount,
                    ArchivedCount = archivedCount,
                    SkippedCount = skippedCount,
                    Warnings = warnings.Distinct().ToList()
                };
            }
            catch
            {
                if (transaction != null)
                    await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        private EducationalProgramElement? FindManualMatch(
            IEnumerable<EducationalProgramElement> elements,
            PlxElementCandidate candidate)
        {
            var candidates = elements.Where(element =>
                string.IsNullOrWhiteSpace(element.ExternalSource) &&
                !element.IsArchived &&
                element.TypeElement == candidate.TypeElement);

            if (!string.IsNullOrWhiteSpace(candidate.Code))
            {
                var codeMatch = candidates.FirstOrDefault(element =>
                    element.Description.Equals(candidate.Code, StringComparison.CurrentCultureIgnoreCase));
                if (codeMatch != null)
                    return codeMatch;
            }

            if (candidate.TypeElement == EducationalProgramElementTypes.Main)
                return candidates.FirstOrDefault(element => MainTemplateMatches(element.Name, candidate.ExternalKey));

            return candidates.FirstOrDefault(element =>
                element.Name.Equals(candidate.Name, StringComparison.CurrentCultureIgnoreCase));
        }

        private static bool MainTemplateMatches(string existingName, string externalKey)
        {
            return externalKey switch
            {
                "main:general" => ContainsAny(existingName, "общая характеристика", "пояснительная записка"),
                "main:curriculum" => existingName.StartsWith("Учебный план", StringComparison.CurrentCultureIgnoreCase),
                "main:schedule" => existingName.Contains("Календарный учебный график", StringComparison.CurrentCultureIgnoreCase),
                "main:guidelines" => existingName.Contains("Методические рекомендации", StringComparison.CurrentCultureIgnoreCase),
                "main:education-work" => existingName.Contains("Программа воспитательной работы", StringComparison.CurrentCultureIgnoreCase),
                "main:education-calendar" => existingName.Contains("Календарный план воспитательной работы", StringComparison.CurrentCultureIgnoreCase),
                _ => false
            };
        }

        private static bool ContainsAny(string value, params string[] candidates) =>
            candidates.Any(candidate => value.Contains(candidate, StringComparison.CurrentCultureIgnoreCase));

        private static bool IsLocked(EducationalProgramElement element)
        {
            var status = ElementApprovalStatus.Normalize(element.StatusApprovals);
            return status is ElementApprovalStatus.Approved or ElementApprovalStatus.Published;
        }

        private void AddHistory(EducationalProgramElement element, int userId, string comment)
        {
            _context.ElementStatusHistory.Add(new ElementStatusHistory
            {
                Element = element,
                UserId = userId,
                OldStatus = ElementApprovalStatus.Normalize(element.StatusApprovals),
                NewStatus = ElementApprovalStatus.Normalize(element.StatusApprovals),
                ChangeDate = DateTime.UtcNow,
                Comment = comment
            });
        }
    }
}
