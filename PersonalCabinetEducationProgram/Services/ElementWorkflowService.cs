using Microsoft.EntityFrameworkCore;
using PersonalCabinetEducationProgram.Data;
using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Services
{
    public class ElementWorkflowService
    {
        private readonly ApplicationDbContext _context;

        public ElementWorkflowService(ApplicationDbContext context)
        {
            _context = context;
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

            AddHistory(element.Id, userId, oldStatus, ElementApprovalStatus.Uploaded, $"Загружен файл: {originalFileName}");
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
                if (ElementApprovalStatus.IsLockedForNonAdmin(oldStatus))
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

        private void AddHistory(int elementId, int userId, string oldStatus, string newStatus, string comment)
        {
            _context.ElementStatusHistory.Add(new ElementStatusHistory
            {
                EducationalProgramElementId = elementId,
                UserId = userId,
                OldStatus = oldStatus,
                NewStatus = newStatus,
                ChangeDate = DateTime.Now,
                Comment = comment
            });
        }
    }
}
