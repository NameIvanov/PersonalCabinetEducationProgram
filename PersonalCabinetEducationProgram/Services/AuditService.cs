using PersonalCabinetEducationProgram.Data;
using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Services
{
    public class AuditService
    {
        private readonly ApplicationDbContext _context;

        public AuditService(ApplicationDbContext context)
        {
            _context = context;
        }

        public void Record(int userId, string entityType, int entityId, string action, string details)
        {
            _context.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                EntityType = entityType,
                EntityId = entityId,
                Action = action,
                Details = details,
                CreatedAt = DateTime.UtcNow
            });
        }
    }
}
