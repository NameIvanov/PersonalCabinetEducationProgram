using Microsoft.EntityFrameworkCore;
using PersonalCabinetEducationProgram.Data;
using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Services
{
    public class ElementWorkflowService
    {
        private readonly ApplicationDbContext _context;
        private readonly NotificationService _notificationService;

        public ElementWorkflowService(
            ApplicationDbContext context,
            NotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        public async Task<EducationalProgramElement?> MarkUploadedAsync(
            int elementId,
            int userId,
            string storedFileName,
            string originalFileName,
            bool adminOverride = false)
        {
            var element = await _context.EducationalProgramElements.FindAsync(elementId);
            if (element == null)
            {
                return null;
            }

            var oldStatus = ElementApprovalStatus.Normalize(element.StatusApprovals);
            if (!adminOverride && ElementApprovalStatus.IsLockedForNonAdmin(oldStatus))
            {
                throw new InvalidOperationException("Нельзя изменить согласованный или опубликованный элемент.");
            }

            element.FilePath = storedFileName;
            element.FileName = originalFileName;
            element.UploadDate = DateOnly.FromDateTime(DateTime.Now);
            element.StatusApprovals = ElementApprovalStatus.Uploaded;

            AddHistory(
                element.Id,
                userId,
                oldStatus,
                ElementApprovalStatus.Uploaded,
                $"Загружен файл: {originalFileName}",
                storedFileName,
                originalFileName);
            await _notificationService.CreateForElementAsync(
                element.Id,
                userId,
                NotificationType.FileUploaded,
                "Загружен файл",
                $"{originalFileName} загружен в элемент «{element.Name}».");
            await RecalculateProgramStatusAsync(element.EducationalProgramId);
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
            var element = await _context.EducationalProgramElements.FindAsync(elementId);
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
            AddHistory(element.Id, userId, oldStatus, normalizedNewStatus, comment ?? normalizedNewStatus);
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
