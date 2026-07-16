using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PersonalCabinetEducationProgram.Data;
using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Services
{
    public class ElementAccessService
    {
        private readonly ApplicationDbContext _context;

        public ElementAccessService(ApplicationDbContext context)
        {
            _context = context;
        }

        public Task<bool> CanManageProgramAsync(ClaimsPrincipal user, int programId)
        {
            if (user.IsInRole(AppRoles.Admin))
                return Task.FromResult(true);

            var userId = GetUserId(user);
            return _context.EducationalPrograms.AnyAsync(p =>
                p.Id == programId && !p.IsArchived &&
                (p.UserId == userId || p.Managers.Any(m => m.UserId == userId)));
        }

        public async Task<bool> CanManageElementAsync(ClaimsPrincipal user, int elementId)
        {
            var programId = await GetProgramIdAsync(elementId);
            return programId.HasValue && await CanManageProgramAsync(user, programId.Value);
        }

        public Task<bool> CanApproveProgramAsync(ClaimsPrincipal user, int programId)
        {
            if (user.IsInRole(AppRoles.Admin))
                return Task.FromResult(true);

            var userId = GetUserId(user);
            return _context.EducationalPrograms.AnyAsync(p =>
                p.Id == programId && !p.IsArchived &&
                p.Assignments.Any(pa => _context.ApproverAssignments.Any(a =>
                    a.ApproverUserId == userId &&
                    ((a.FacultyId != null && a.FacultyId == pa.FacultyId) ||
                     (a.DepartmentId != null && a.DepartmentId == pa.DepartmentId)))));
        }

        public async Task<bool> CanApproveElementAsync(ClaimsPrincipal user, int elementId)
        {
            var programId = await GetProgramIdAsync(elementId);
            return programId.HasValue && await CanApproveProgramAsync(user, programId.Value);
        }

        public async Task<bool> CanViewElementAsync(ClaimsPrincipal user, int elementId)
        {
            if (user.IsInRole(AppRoles.Admin))
                return true;

            if (user.IsInRole(AppRoles.Moderator))
                return await _context.EducationalProgramElements.AnyAsync(e =>
                    e.Id == elementId && !e.IsArchived && !e.EducationalProgram.IsArchived);

            if (user.IsInRole(AppRoles.Manager) && await CanManageElementAsync(user, elementId))
                return true;

            return user.IsInRole(AppRoles.Approver) && await CanApproveElementAsync(user, elementId);
        }

        private Task<int?> GetProgramIdAsync(int elementId)
        {
            return _context.EducationalProgramElements
                .Where(e => e.Id == elementId && !e.IsArchived)
                .Select(e => (int?)e.EducationalProgramId)
                .FirstOrDefaultAsync();
        }

        private static int GetUserId(ClaimsPrincipal user)
        {
            return int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new InvalidOperationException("Идентификатор пользователя отсутствует."));
        }
    }
}
