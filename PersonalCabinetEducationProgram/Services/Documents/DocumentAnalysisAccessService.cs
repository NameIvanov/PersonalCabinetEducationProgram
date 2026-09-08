using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PersonalCabinetEducationProgram.Data;
using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Services.Documents;

public sealed class DocumentAnalysisAccessService(ApplicationDbContext db, ElementAccessService access)
{
    public Task<bool> CanAccessElementAsync(ClaimsPrincipal user, int elementId, DocumentAnalysisPurpose purpose, bool recordDenial = true) => purpose switch
    {
        DocumentAnalysisPurpose.ManagerPreflight => access.CanManageElementAsync(user, elementId, recordDenial),
        DocumentAnalysisPurpose.ApproverReview => access.CanApproveElementAsync(user, elementId, recordDenial),
        DocumentAnalysisPurpose.ModeratorPublication => access.CanModerateElementAsync(user, elementId),
        _ => Task.FromResult(false)
    };

    public async Task<EducationalProgramElementFile?> GetFileAsync(ClaimsPrincipal user, int fileId,
        DocumentAnalysisPurpose purpose = DocumentAnalysisPurpose.ManagerPreflight)
    {
        var file = await db.EducationalProgramElementFiles.AsNoTracking()
            .Include(f => f.Element).ThenInclude(e => e.EducationalProgram)
            .SingleOrDefaultAsync(f => f.Id == fileId);
        return file != null && await CanAccessElementAsync(user, file.EducationalProgramElementId, purpose)
            ? file : null;
    }

    // Use live account/role assignments, never the claims captured at enqueue time.
    public async Task<ClaimsPrincipal?> CurrentPrincipalAsync(int? userId, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId, ct);
        if (user == null || user.ApprovalStatus != UserApprovalStatus.Approved || user.SecurityBlockedAtUtc != null ||
            (user.LockoutEnabled && user.LockoutEnd > DateTimeOffset.UtcNow)) return null;
        var roles = await (from membership in db.UserRoles join role in db.Roles on membership.RoleId equals role.Id
                           where membership.UserId == user.Id select role.Name).ToListAsync(ct);
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, user.Id.ToString()), new(ClaimTypes.Name, user.FullName) };
        claims.AddRange(roles.Where(x => x != null).Select(x => new Claim(ClaimTypes.Role, x!)));
        return new(new ClaimsIdentity(claims, "DocumentAnalysisWorker"));
    }
}
