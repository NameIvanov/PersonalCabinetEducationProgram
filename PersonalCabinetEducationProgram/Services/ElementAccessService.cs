using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PersonalCabinetEducationProgram.Data;
using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Services
{
    public class ElementAccessService
    {
        private readonly ApplicationDbContext _context;
        private readonly ObjectAuthorizationIncidentService _incidents;

        public ElementAccessService(
            ApplicationDbContext context,
            ObjectAuthorizationIncidentService incidents)
        {
            _context = context;
            _incidents = incidents;
        }

        public async Task<bool> CanManageProgramAsync(ClaimsPrincipal user, int programId)
        {
            var isAdmin = user.IsInRole(AppRoles.Admin);
            if (!isAdmin && !user.IsInRole(AppRoles.Manager))
                return false;

            var userId = GetUserId(user);
            var access = await _context.EducationalPrograms
                .Where(program => program.Id == programId && !program.IsArchived)
                .Select(program => new
                {
                    Allowed = isAdmin || program.UserId == userId ||
                              program.Managers.Any(manager => manager.UserId == userId)
                })
                .SingleOrDefaultAsync();

            if (access == null)
                return false;
            if (!access.Allowed)
                _incidents.Record("EducationalProgram", programId, "управление программой");
            return access.Allowed;
        }

        public async Task<bool> CanManageElementAsync(ClaimsPrincipal user, int elementId)
        {
            var isAdmin = user.IsInRole(AppRoles.Admin);
            if (!isAdmin && !user.IsInRole(AppRoles.Manager))
                return false;

            var userId = GetUserId(user);
            var access = await _context.EducationalProgramElements
                .Where(element => element.Id == elementId &&
                                  !element.IsArchived &&
                                  !element.EducationalProgram.IsArchived)
                .Select(element => new
                {
                    Allowed = isAdmin || element.EducationalProgram.UserId == userId ||
                              element.EducationalProgram.Managers.Any(manager => manager.UserId == userId)
                })
                .SingleOrDefaultAsync();

            if (access == null)
                return false;
            if (!access.Allowed)
                _incidents.Record("EducationalProgramElement", elementId, "управление элементом");
            return access.Allowed;
        }

        public async Task<bool> CanApproveProgramAsync(ClaimsPrincipal user, int programId)
        {
            var isAdmin = user.IsInRole(AppRoles.Admin);
            if (!isAdmin && !user.IsInRole(AppRoles.Approver))
                return false;

            var userId = GetUserId(user);
            var access = await _context.EducationalPrograms
                .Where(program => program.Id == programId && !program.IsArchived)
                .Select(program => new
                {
                    Allowed = isAdmin || program.Assignments.Any(assignment =>
                        _context.ApproverAssignments.Any(approver =>
                            approver.ApproverUserId == userId &&
                            ((approver.FacultyId != null && approver.FacultyId == assignment.FacultyId) ||
                             (approver.DepartmentId != null && approver.DepartmentId == assignment.DepartmentId))))
                })
                .SingleOrDefaultAsync();

            if (access == null)
                return false;
            if (!access.Allowed)
                _incidents.Record("EducationalProgram", programId, "согласование программы");
            return access.Allowed;
        }

        public async Task<bool> CanApproveElementAsync(ClaimsPrincipal user, int elementId)
        {
            var isAdmin = user.IsInRole(AppRoles.Admin);
            if (!isAdmin && !user.IsInRole(AppRoles.Approver))
                return false;

            var userId = GetUserId(user);
            var access = await _context.EducationalProgramElements
                .Where(element => element.Id == elementId &&
                                  !element.IsArchived &&
                                  !element.EducationalProgram.IsArchived)
                .Select(element => new
                {
                    Allowed = isAdmin || element.EducationalProgram.Assignments.Any(assignment =>
                        _context.ApproverAssignments.Any(approver =>
                            approver.ApproverUserId == userId &&
                            ((approver.FacultyId != null && approver.FacultyId == assignment.FacultyId) ||
                             (approver.DepartmentId != null && approver.DepartmentId == assignment.DepartmentId))))
                })
                .SingleOrDefaultAsync();

            if (access == null)
                return false;
            if (!access.Allowed)
                _incidents.Record("EducationalProgramElement", elementId, "согласование элемента");
            return access.Allowed;
        }

        public async Task<bool> CanViewElementAsync(ClaimsPrincipal user, int elementId)
        {
            if (user.IsInRole(AppRoles.Admin) || user.IsInRole(AppRoles.Moderator))
            {
                return await _context.EducationalProgramElements.AnyAsync(element =>
                    element.Id == elementId && !element.IsArchived && !element.EducationalProgram.IsArchived);
            }

            if (user.IsInRole(AppRoles.Manager))
                return await CanManageElementAsync(user, elementId);

            return user.IsInRole(AppRoles.Approver) && await CanApproveElementAsync(user, elementId);
        }

        public Task<bool> CanModerateElementAsync(ClaimsPrincipal user, int elementId)
        {
            if (!user.IsInRole(AppRoles.Moderator) && !user.IsInRole(AppRoles.Admin))
                return Task.FromResult(false);

            return _context.EducationalProgramElements.AnyAsync(element =>
                element.Id == elementId && !element.IsArchived && !element.EducationalProgram.IsArchived);
        }

        private static int GetUserId(ClaimsPrincipal user)
        {
            return int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new InvalidOperationException("Идентификатор пользователя отсутствует."));
        }
    }
}
