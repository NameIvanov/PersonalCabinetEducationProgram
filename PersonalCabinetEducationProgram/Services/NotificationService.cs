using Microsoft.EntityFrameworkCore;
using PersonalCabinetEducationProgram.Data;
using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Services
{
    public class NotificationService
    {
        private readonly ApplicationDbContext _context;

        public NotificationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task CreateForElementAsync(
            int elementId,
            int actorUserId,
            string type,
            string title,
            string message)
        {
            var element = await _context.EducationalProgramElements
                .Include(e => e.EducationalProgram)
                .ThenInclude(p => p.Assignments)
                .FirstOrDefaultAsync(e => e.Id == elementId);

            if (element == null)
                return;

            var actorName = await _context.Users
                .Where(u => u.Id == actorUserId)
                .Select(u => u.FullName)
                .FirstOrDefaultAsync() ?? "Пользователь";

            var recipientIds = new HashSet<int>();

            var globalRecipientIds = await _context.UserRoles
                .Where(ur => ur.RoleId == AppRoles.AdminId || ur.RoleId == AppRoles.ModeratorId)
                .Select(ur => ur.UserId)
                .ToListAsync();
            recipientIds.UnionWith(globalRecipientIds);

            if (element.EducationalProgram.UserId.HasValue)
                recipientIds.Add(element.EducationalProgram.UserId.Value);

            var managerIds = await _context.EducationalProgramManagers
                .Where(m => m.EducationalProgramId == element.EducationalProgramId)
                .Select(m => m.UserId)
                .ToListAsync();
            recipientIds.UnionWith(managerIds);

            var facultyIds = element.EducationalProgram.Assignments
                .Select(a => a.FacultyId)
                .Distinct()
                .ToList();
            var departmentIds = element.EducationalProgram.Assignments
                .Select(a => a.DepartmentId)
                .Distinct()
                .ToList();

            var approverIds = await _context.ApproverAssignments
                .Where(a =>
                    (a.FacultyId.HasValue && facultyIds.Contains(a.FacultyId.Value)) ||
                    (a.DepartmentId.HasValue && departmentIds.Contains(a.DepartmentId.Value)))
                .Select(a => a.ApproverUserId)
                .ToListAsync();
            recipientIds.UnionWith(approverIds);

            var approvedRecipientIds = await _context.Users
                .Where(u => recipientIds.Contains(u.Id) &&
                            u.Id != actorUserId &&
                            u.ApprovalStatus == UserApprovalStatus.Approved)
                .Select(u => u.Id)
                .ToListAsync();

            var createdAt = DateTime.UtcNow;
            foreach (var recipientId in approvedRecipientIds)
            {
                _context.Notifications.Add(new Notification
                {
                    UserId = recipientId,
                    EducationalProgramElementId = elementId,
                    ActorName = actorName,
                    Type = type,
                    Title = title,
                    Message = message,
                    CreatedAt = createdAt
                });
            }
        }

        public async Task MarkElementReadAsync(int userId, int elementId)
        {
            var unread = await _context.Notifications
                .Where(n => n.UserId == userId &&
                            n.EducationalProgramElementId == elementId &&
                            !n.IsRead)
                .ToListAsync();

            if (unread.Count == 0)
                return;

            var readAt = DateTime.UtcNow;
            foreach (var notification in unread)
            {
                notification.IsRead = true;
                notification.ReadAt = readAt;
            }

            await _context.SaveChangesAsync();
        }

        public async Task MarkAllReadAsync(int userId)
        {
            var unread = await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync();

            var readAt = DateTime.UtcNow;
            foreach (var notification in unread)
            {
                notification.IsRead = true;
                notification.ReadAt = readAt;
            }

            await _context.SaveChangesAsync();
        }

        public void CreateSecurity(int userId, string title, string message)
        {
            _context.Notifications.Add(new Notification
            {
                UserId = userId,
                EducationalProgramElementId = null,
                ActorName = "Система безопасности",
                Type = NotificationType.Security,
                Title = title,
                Message = message,
                CreatedAt = DateTime.UtcNow
            });
        }

        public async Task CreateSecurityForAdministratorsAsync(
            string title,
            string message,
            int? exceptUserId = null,
            CancellationToken cancellationToken = default)
        {
            var administratorIds = await _context.UserRoles
                .Where(role => role.RoleId == AppRoles.AdminId &&
                               (!exceptUserId.HasValue || role.UserId != exceptUserId.Value))
                .Select(role => role.UserId)
                .ToListAsync(cancellationToken);

            foreach (var administratorId in administratorIds)
                CreateSecurity(administratorId, title, message);
        }

        public async Task MarkReadAsync(int userId, int notificationId)
        {
            var notification = await _context.Notifications
                .SingleOrDefaultAsync(item => item.Id == notificationId && item.UserId == userId);
            if (notification == null || notification.IsRead)
                return;

            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }
}
