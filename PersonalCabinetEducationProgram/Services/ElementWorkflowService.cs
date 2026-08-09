using Microsoft.EntityFrameworkCore;
using PersonalCabinetEducationProgram.Data;
using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Services
{
    public class ElementWorkflowService
    {
        private readonly ApplicationDbContext _context;
        private readonly NotificationService _notificationService;
        private readonly AuditService _auditService;

        public ElementWorkflowService(
            ApplicationDbContext context,
            NotificationService notificationService,
            AuditService auditService)
        {
            _context = context;
            _notificationService = notificationService;
            _auditService = auditService;
        }

        public async Task<EducationalProgramElement?> MarkUploadedAsync(
            int elementId,
            int userId,
            string storedFileName,
            string originalFileName,
            bool adminOverride = false)
        {
            return await MarkFilesUploadedAsync(
                elementId,
                userId,
                [(storedFileName, originalFileName)],
                adminOverride);
        }

        public async Task<EducationalProgramElement?> MarkFilesUploadedAsync(
            int elementId,
            int userId,
            IReadOnlyCollection<(string StoredFileName, string OriginalFileName)> files,
            bool adminOverride = false)
        {
            if (files.Count == 0)
                throw new InvalidOperationException("Не выбран ни один файл.");

            var element = await _context.EducationalProgramElements
                .Include(e => e.Files)
                .FirstOrDefaultAsync(e => e.Id == elementId && !e.IsArchived && !e.EducationalProgram.IsArchived);
            if (element == null)
            {
                return null;
            }

            if (element.IsArchived)
                throw new InvalidOperationException("Архивный элемент нельзя изменять.");

            var oldStatus = ElementApprovalStatus.Normalize(element.StatusApprovals);
            if (ElementApprovalStatus.IsLockedForNonAdmin(oldStatus))
            {
                throw new InvalidOperationException("Нельзя изменить согласованный или опубликованный элемент.");
            }

            if (!adminOverride && oldStatus == ElementApprovalStatus.OnApproval)
                throw new InvalidOperationException("Отправленная на согласование группа файлов зафиксирована.");

            var currentFiles = element.Files.Where(f => f.IsCurrent).ToList();
            if (currentFiles.Count + files.Count > FileUploadLimits.MaxFilesPerGroup)
                throw new InvalidOperationException($"В одной группе может быть не более {FileUploadLimits.MaxFilesPerGroup} файлов.");
            if (currentFiles.Any(f => f.IsSubmitted))
                throw new InvalidOperationException("Отправленная группа файлов зафиксирована и не может быть изменена.");

            var revisionNumber = currentFiles.Select(f => f.RevisionNumber).DefaultIfEmpty(
                element.Files.Select(f => f.RevisionNumber).DefaultIfEmpty(0).Max() + 1).Max();

            var uploadedAt = DateTime.UtcNow;
            foreach (var file in files)
            {
                element.Files.Add(new EducationalProgramElementFile
                {
                    StoredFileName = file.StoredFileName,
                    OriginalFileName = file.OriginalFileName,
                    RevisionNumber = revisionNumber,
                    IsCurrent = true,
                    UploadedAt = uploadedAt,
                    UploadedByUserId = userId
                });
            }

            var firstFile = element.Files.First(f => f.IsCurrent);
            element.FilePath = firstFile.StoredFileName;
            element.FileName = firstFile.OriginalFileName;
            element.UploadDate = DateOnly.FromDateTime(uploadedAt);
            element.StatusApprovals = ElementApprovalStatus.Uploaded;
            element.Version++;

            AddHistory(
                element.Id,
                userId,
                oldStatus,
                ElementApprovalStatus.Uploaded,
                files.Count == 1
                    ? $"Загружен файл: {files.First().OriginalFileName}"
                    : $"Загружена группа из {files.Count} файлов");
            _auditService.Record(userId, "EducationalProgramElement", element.Id, "FilesUploaded",
                $"Итерация {revisionNumber}: добавлено файлов {files.Count}.");
            await _notificationService.CreateForElementAsync(
                element.Id,
                userId,
                NotificationType.FileUploaded,
                "Загружен файл",
                files.Count == 1
                    ? $"{files.First().OriginalFileName} загружен в элемент «{element.Name}»."
                    : $"В элемент «{element.Name}» загружена группа из {files.Count} файлов.");
            await RecalculateProgramStatusAsync(element.EducationalProgramId);
            await _context.SaveChangesAsync();

            return element;
        }

        public async Task<EducationalProgramElement?> SubmitForApprovalAsync(int elementId, int userId)
        {
            var element = await _context.EducationalProgramElements
                .Include(e => e.Files)
                .FirstOrDefaultAsync(e => e.Id == elementId && !e.IsArchived && !e.EducationalProgram.IsArchived);

            if (element == null)
                return null;

            if (element.IsArchived)
                throw new InvalidOperationException("Архивный элемент нельзя отправить на согласование.");

            if (ElementApprovalStatus.Normalize(element.StatusApprovals) != ElementApprovalStatus.Uploaded)
                throw new InvalidOperationException("На согласование можно отправить только загруженный элемент.");

            var currentFiles = element.Files.Where(f => f.IsCurrent).ToList();
            if (currentFiles.Count == 0 && !string.IsNullOrWhiteSpace(element.FilePath))
            {
                currentFiles.Add(new EducationalProgramElementFile
                {
                    StoredFileName = element.FilePath,
                    OriginalFileName = element.FileName ?? Path.GetFileName(element.FilePath),
                    RevisionNumber = 1,
                    IsCurrent = true,
                    UploadedAt = DateTime.UtcNow,
                    UploadedByUserId = userId
                });
                element.Files.Add(currentFiles[0]);
            }

            if (currentFiles.Count == 0)
                throw new InvalidOperationException("Перед отправкой прикрепите хотя бы один файл.");

            foreach (var file in currentFiles)
                file.IsSubmitted = true;

            return await ChangeStatusAsync(
                elementId,
                userId,
                ElementApprovalStatus.OnApproval,
                $"Отправлена на согласование группа из {currentFiles.Count} файлов",
                allowedFrom: [ElementApprovalStatus.Uploaded]);
        }

        public async Task<EducationalProgramElement?> RemoveCurrentFileAsync(
            int fileId,
            int userId,
            string reason = "Удалён из текущего комплекта")
        {
            var file = await _context.EducationalProgramElementFiles
                .Include(f => f.Element).ThenInclude(e => e.EducationalProgram)
                .Include(f => f.Element).ThenInclude(e => e.Files)
                .FirstOrDefaultAsync(f => f.Id == fileId);
            if (file == null)
                return null;

            EnsureCurrentGroupIsEditable(file);
            var element = file.Element;
            var oldStatus = ElementApprovalStatus.Normalize(element.StatusApprovals);
            MarkRemoved(file, userId, reason);
            UpdateCurrentFileSummary(element);
            element.StatusApprovals = element.Files.Any(f => f.IsCurrent)
                ? ElementApprovalStatus.Uploaded
                : ElementApprovalStatus.NotUploaded;
            element.Version++;

            AddHistory(element.Id, userId, oldStatus, element.StatusApprovals,
                $"Файл «{file.OriginalFileName}» удалён из текущего комплекта.");
            _auditService.Record(userId, "EducationalProgramElement", element.Id, "CurrentFileRemoved",
                $"Файл {file.OriginalFileName}; итерация {file.RevisionNumber}.");
            await RecalculateProgramStatusAsync(element.EducationalProgramId);
            await _context.SaveChangesAsync();
            return element;
        }

        public async Task<EducationalProgramElement?> ReplaceCurrentFileAsync(
            int fileId,
            int userId,
            string storedFileName,
            string originalFileName)
        {
            var file = await _context.EducationalProgramElementFiles
                .Include(f => f.Element).ThenInclude(e => e.EducationalProgram)
                .Include(f => f.Element).ThenInclude(e => e.Files)
                .FirstOrDefaultAsync(f => f.Id == fileId);
            if (file == null)
                return null;

            EnsureCurrentGroupIsEditable(file);
            var element = file.Element;
            var oldName = file.OriginalFileName;
            MarkRemoved(file, userId, "Заменён новым файлом");
            element.Files.Add(new EducationalProgramElementFile
            {
                StoredFileName = storedFileName,
                OriginalFileName = originalFileName,
                RevisionNumber = file.RevisionNumber,
                IsCurrent = true,
                UploadedAt = DateTime.UtcNow,
                UploadedByUserId = userId
            });
            UpdateCurrentFileSummary(element);
            element.StatusApprovals = ElementApprovalStatus.Uploaded;
            element.Version++;

            AddHistory(element.Id, userId, ElementApprovalStatus.Uploaded, ElementApprovalStatus.Uploaded,
                $"Файл «{oldName}» заменён файлом «{originalFileName}».");
            _auditService.Record(userId, "EducationalProgramElement", element.Id, "CurrentFileReplaced",
                $"{oldName} -> {originalFileName}; итерация {file.RevisionNumber}.");
            await _notificationService.CreateForElementAsync(
                element.Id, userId, NotificationType.FileUploaded, "Файл заменён",
                $"В элементе «{element.Name}» файл «{oldName}» заменён файлом «{originalFileName}».");
            await _context.SaveChangesAsync();
            return element;
        }

        public async Task<EducationalProgramElement?> ChangeStatusAsync(
            int elementId,
            int userId,
            string newStatus,
            string? comment,
            bool adminOverride = false,
            IReadOnlyCollection<string>? allowedFrom = null)
        {
            var element = await _context.EducationalProgramElements
                .Include(e => e.Files)
                .FirstOrDefaultAsync(e => e.Id == elementId && !e.IsArchived && !e.EducationalProgram.IsArchived);
            if (element == null)
            {
                return null;
            }

            if (element.IsArchived)
                throw new InvalidOperationException("Архивный элемент нельзя изменить.");

            var oldStatus = ElementApprovalStatus.Normalize(element.StatusApprovals);
            var normalizedNewStatus = ElementApprovalStatus.Normalize(newStatus);

            if (!adminOverride)
            {
                if (allowedFrom == null && ElementApprovalStatus.IsLockedForNonAdmin(oldStatus))
                {
                    throw new InvalidOperationException("Нельзя изменить согласованный или опубликованный элемент.");
                }

                if (allowedFrom != null && !allowedFrom.Contains(oldStatus))
                {
                    throw new InvalidOperationException("Элемент не может перейти в выбранный статус из текущего состояния.");
                }
            }

            element.StatusApprovals = normalizedNewStatus;
            element.Version++;

            if (normalizedNewStatus == ElementApprovalStatus.RevisionRequired)
            {
                foreach (var file in element.Files.Where(f => f.IsCurrent))
                    file.IsCurrent = false;

                element.FilePath = null;
                element.FileName = null;
                element.UploadDate = null;
            }
            AddHistory(element.Id, userId, oldStatus, normalizedNewStatus, comment ?? normalizedNewStatus);
            _auditService.Record(userId, "EducationalProgramElement", element.Id, "StatusChanged",
                $"{oldStatus} -> {normalizedNewStatus}. {comment}".Trim());
            await _notificationService.CreateForElementAsync(
                element.Id,
                userId,
                NotificationType.StatusChanged,
                "Изменён статус",
                $"Элемент «{element.Name}»: «{oldStatus}» → «{normalizedNewStatus}». {comment}".Trim());
            await RecalculateProgramStatusAsync(element.EducationalProgramId);
            await _context.SaveChangesAsync();

            return element;
        }

        public async Task RecalculateProgramStatusAsync(int programId)
        {
            var program = await _context.EducationalPrograms
                .Include(p => p.Elements)
                .FirstOrDefaultAsync(p => p.Id == programId);

            if (program == null)
            {
                return;
            }

            var elementStatuses = program.Elements
                .Where(e => !e.IsArchived)
                .Select(e => e.StatusApprovals)
                .ToList();

            var newStatus = CalculateProgramStatus(elementStatuses);

            if (program.Status != newStatus)
            {
                program.Status = newStatus;
                program.Version++;
            }
        }

        public static string CalculateProgramStatus(IEnumerable<string?> statuses)
        {
            var elementStatuses = statuses.Select(ElementApprovalStatus.Normalize).ToList();
            return elementStatuses.Any(s => s == ElementApprovalStatus.RevisionRequired)
                ? EducationalProgramStatus.RevisionRequired
                : elementStatuses.Count == 0 || elementStatuses.Any(string.IsNullOrEmpty)
                    ? EducationalProgramStatus.Draft
                    : elementStatuses.All(s => s == ElementApprovalStatus.Published)
                        ? EducationalProgramStatus.Published
                        : elementStatuses.All(s => s is ElementApprovalStatus.Approved or ElementApprovalStatus.Published)
                            ? EducationalProgramStatus.Approved
                            : EducationalProgramStatus.Draft;
        }

        private void AddHistory(
            int elementId,
            int userId,
            string oldStatus,
            string newStatus,
            string comment,
            string? filePath = null,
            string? fileName = null)
        {
            _context.ElementStatusHistory.Add(new ElementStatusHistory
            {
                EducationalProgramElementId = elementId,
                UserId = userId,
                OldStatus = oldStatus,
                NewStatus = newStatus,
                ChangeDate = DateTime.UtcNow,
                Comment = comment,
                FilePath = filePath,
                FileName = fileName
            });
        }

        private static void EnsureCurrentGroupIsEditable(EducationalProgramElementFile file)
        {
            var status = ElementApprovalStatus.Normalize(file.Element.StatusApprovals);
            if (!file.IsCurrent || file.IsRemoved)
                throw new InvalidOperationException("Файл уже не входит в текущий комплект.");
            if (file.IsSubmitted || status == ElementApprovalStatus.OnApproval)
                throw new InvalidOperationException("Зафиксированный комплект нельзя изменять.");
            if (ElementApprovalStatus.IsLockedForNonAdmin(status))
                throw new InvalidOperationException("Согласованный или опубликованный элемент нельзя изменять.");
        }

        private static void MarkRemoved(EducationalProgramElementFile file, int userId, string reason)
        {
            file.IsCurrent = false;
            file.IsRemoved = true;
            file.RemovedAt = DateTime.UtcNow;
            file.RemovedByUserId = userId;
            file.RemovalReason = reason;
        }

        private static void UpdateCurrentFileSummary(EducationalProgramElement element)
        {
            var current = element.Files
                .Where(f => f.IsCurrent && !f.IsRemoved)
                .OrderByDescending(f => f.UploadedAt)
                .FirstOrDefault();
            element.FilePath = current?.StoredFileName;
            element.FileName = current?.OriginalFileName;
            element.UploadDate = current == null ? null : DateOnly.FromDateTime(current.UploadedAt);
        }
    }
}
