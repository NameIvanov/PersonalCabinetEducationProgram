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
                .FirstOrDefaultAsync(e => e.Id == elementId);
            if (element == null)
            {
                return null;
            }

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
                .FirstOrDefaultAsync(e => e.Id == elementId);

            if (element == null)
                return null;

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
                .FirstOrDefaultAsync(e => e.Id == elementId);
            if (element == null)
            {
                return null;
            }

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
                .Select(e => ElementApprovalStatus.Normalize(e.StatusApprovals))
                .ToList();

            if (elementStatuses.Count == 0 || elementStatuses.Any(string.IsNullOrEmpty))
            {
                program.Status = EducationalProgramStatus.Draft;
                return;
            }

            if (elementStatuses.All(s => s == ElementApprovalStatus.Published))
            {
                program.Status = EducationalProgramStatus.Published;
                return;
            }

            if (elementStatuses.All(s => s is ElementApprovalStatus.Approved or ElementApprovalStatus.Published))
            {
                program.Status = EducationalProgramStatus.Approved;
                return;
            }

            program.Status = EducationalProgramStatus.Draft;
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
                ChangeDate = DateTime.Now,
                Comment = comment,
                FilePath = filePath,
                FileName = fileName
            });
        }
    }
}
