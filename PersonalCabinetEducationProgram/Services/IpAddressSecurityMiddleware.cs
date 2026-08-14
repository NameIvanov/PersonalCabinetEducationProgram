using Microsoft.EntityFrameworkCore;
using PersonalCabinetEducationProgram.Data;
using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Services
{
    public sealed record ProtectedObjectReference(string ObjectType, long ObjectId);

    public sealed class ProtectedObjectProbeDetector
    {
        private readonly ApplicationDbContext _context;

        public ProtectedObjectProbeDetector(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ProtectedObjectReference?> DetectAsync(
            HttpContext context,
            CancellationToken cancellationToken = default)
        {
            if (context.User.Identity?.IsAuthenticated == true)
                return null;

            var controller = context.Request.RouteValues["controller"]?.ToString();
            var action = context.Request.RouteValues["action"]?.ToString();
            if (string.IsNullOrWhiteSpace(controller) || controller.Equals("Account", StringComparison.OrdinalIgnoreCase))
                return null;

            IFormCollection? form = null;
            if (context.Request.ContentType?.StartsWith(
                    "application/x-www-form-urlencoded",
                    StringComparison.OrdinalIgnoreCase) == true &&
                context.Request.ContentLength is > 0 and <= 65_536)
            {
                try
                {
                    form = await context.Request.ReadFormAsync(cancellationToken);
                }
                catch (InvalidDataException)
                {
                    form = null;
                }
            }

            if (TryGetId(context, form, "elementId", out var elementId) &&
                await _context.EducationalProgramElements.AsNoTracking().AnyAsync(item => item.Id == elementId, cancellationToken))
            {
                return new ProtectedObjectReference("EducationalProgramElement", elementId);
            }

            if (TryGetId(context, form, "programId", out var programId) &&
                await _context.EducationalPrograms.AsNoTracking().AnyAsync(item => item.Id == programId, cancellationToken))
            {
                return new ProtectedObjectReference("EducationalProgram", programId);
            }

            if (TryGetId(context, form, "fileId", out var fileId) &&
                await _context.EducationalProgramElementFiles.AsNoTracking().AnyAsync(item => item.Id == fileId, cancellationToken))
            {
                return new ProtectedObjectReference("EducationalProgramElementFile", fileId);
            }

            if (TryGetId(context, form, "historyId", out var historyId) &&
                await _context.ElementStatusHistory.AsNoTracking().AnyAsync(item => item.Id == historyId, cancellationToken))
            {
                return new ProtectedObjectReference("ElementStatusHistory", historyId);
            }

            if (TryGetId(context, form, "commentId", out var commentId) &&
                await _context.EducationalProgramElementComment.AsNoTracking().AnyAsync(item => item.Id == commentId, cancellationToken))
            {
                return new ProtectedObjectReference("EducationalProgramElementComment", commentId);
            }

            if (TryGetId(context, form, "networkId", out var networkId) &&
                await _context.UserLoginLocations.AsNoTracking().AnyAsync(item => item.Id == networkId, cancellationToken))
            {
                return new ProtectedObjectReference("UserLoginLocation", networkId);
            }

            if (!TryGetId(context, form, "id", out var id))
                return null;

            if (controller.Equals("Notifications", StringComparison.OrdinalIgnoreCase) &&
                await _context.Notifications.AsNoTracking().AnyAsync(item => item.Id == id, cancellationToken))
            {
                return new ProtectedObjectReference("Notification", id);
            }

            if (controller.Equals("ElementFiles", StringComparison.OrdinalIgnoreCase) &&
                await _context.EducationalProgramElementFiles.AsNoTracking().AnyAsync(item => item.Id == id, cancellationToken))
            {
                return new ProtectedObjectReference("EducationalProgramElementFile", id);
            }

            if (controller.Equals("CurriculumImport", StringComparison.OrdinalIgnoreCase) &&
                action?.Equals("Download", StringComparison.OrdinalIgnoreCase) == true &&
                await _context.CurriculumImports.AsNoTracking().AnyAsync(item => item.Id == id, cancellationToken))
            {
                return new ProtectedObjectReference("CurriculumImport", id);
            }

            if (controller.Equals("Administration", StringComparison.OrdinalIgnoreCase) &&
                action?.Equals("RequestDetails", StringComparison.OrdinalIgnoreCase) == true &&
                await _context.SystemRequestLogs.AsNoTracking().AnyAsync(item => item.Id == id, cancellationToken))
            {
                return new ProtectedObjectReference("SystemRequestLog", id);
            }

            return await DetectAdminObjectAsync(controller, action, id, cancellationToken);
        }

        private async Task<ProtectedObjectReference?> DetectAdminObjectAsync(
            string controller,
            string? action,
            long id,
            CancellationToken cancellationToken)
        {
            if (!controller.Equals("Admin", StringComparison.OrdinalIgnoreCase))
                return null;

            if ((action is "ResetUserPassword" or "UnlockUser" or "EditUser" or "DeleteUser" or "ChangeApprovalStatus") &&
                await _context.Users.AsNoTracking().AnyAsync(item => item.Id == id, cancellationToken))
            {
                return new ProtectedObjectReference("User", id);
            }

            if (action == "ProgramDetails" &&
                await _context.EducationalPrograms.AsNoTracking().AnyAsync(item => item.Id == id, cancellationToken))
            {
                return new ProtectedObjectReference("EducationalProgram", id);
            }

            if (action == "DepartmentDetails" &&
                await _context.Departments.AsNoTracking().AnyAsync(item => item.Id == id, cancellationToken))
            {
                return new ProtectedObjectReference("Department", id);
            }

            if (action == "FacultyDetails" &&
                await _context.Facultys.AsNoTracking().AnyAsync(item => item.Id == id, cancellationToken))
            {
                return new ProtectedObjectReference("Faculty", id);
            }

            return null;
        }

        private static bool TryGetId(HttpContext context, IFormCollection? form, string key, out long id)
        {
            id = 0;
            if (context.Request.RouteValues.TryGetValue(key, out var routeValue) &&
                long.TryParse(routeValue?.ToString(), out id))
            {
                return true;
            }

            return context.Request.Query.TryGetValue(key, out var queryValue) &&
                   long.TryParse(queryValue.FirstOrDefault(), out id) ||
                   form != null && form.TryGetValue(key, out var formValue) &&
                   long.TryParse(formValue.FirstOrDefault(), out id);
        }
    }

    public sealed class IpAddressSecurityMiddleware
    {
        private readonly RequestDelegate _next;

        public IpAddressSecurityMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(
            HttpContext context,
            ProtectedObjectProbeDetector probeDetector,
            IpAddressSecurityService securityService,
            IpAddressBlockRegistry blockRegistry)
        {
            var ipAddress = IpAddressNormalizer.NormalizeOrUnknown(context.Connection.RemoteIpAddress?.ToString());
            var reference = await probeDetector.DetectAsync(context, context.RequestAborted);
            if (reference != null && ipAddress != "unknown")
            {
                await securityService.RecordAnonymousObjectProbeAsync(
                    ipAddress,
                    reference.ObjectType,
                    reference.ObjectId,
                    context.RequestAborted);
            }

            if (ipAddress == "unknown" ||
                !blockRegistry.IsBlocked(ipAddress, DateTime.UtcNow, out var block) ||
                IsRecoveryRequest(context))
            {
                await _next(context);
                return;
            }

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "text/plain; charset=utf-8";
            var until = block?.Permanent == true
                ? "до разблокировки администратором"
                : $"до {block?.BlockedUntilUtc?.ToLocalTime():dd.MM.yyyy HH:mm}";
            await context.Response.WriteAsync($"IP-адрес заблокирован службой безопасности {until}.");
        }

        private static bool IsRecoveryRequest(HttpContext context)
        {
            if (context.User.IsInRole(AppRoles.Admin))
                return true;

            var path = context.Request.Path;
            return path.StartsWithSegments("/Account/Login") ||
                   path.StartsWithSegments("/Account/Logout") ||
                   path.StartsWithSegments("/css") ||
                   path.StartsWithSegments("/js") ||
                   path.StartsWithSegments("/lib") ||
                   path.StartsWithSegments("/favicon");
        }
    }
}
