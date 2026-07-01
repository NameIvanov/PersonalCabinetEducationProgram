using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PersonalCabinetEducationProgram.Data;
using PersonalCabinetEducationProgram.Models;
using PersonalCabinetEducationProgram.Services;
using PersonalCabinetEducationProgram.ViewModels;

namespace PersonalCabinetEducationProgram.Controllers
{
    [Authorize]
    public class NotificationsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly NotificationService _notificationService;

        public NotificationsController(
            ApplicationDbContext context,
            NotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        public async Task<IActionResult> Index(bool unreadOnly = false)
        {
            var userId = GetCurrentUserId();
            var baseQuery = _context.Notifications.Where(n => n.UserId == userId);

            return View(new NotificationsViewModel
            {
                AllCount = await baseQuery.CountAsync(),
                UnreadCount = await baseQuery.CountAsync(n => !n.IsRead),
                UnreadOnly = unreadOnly,
                Notifications = await baseQuery
                    .Where(n => !unreadOnly || !n.IsRead)
                    .Include(n => n.Element)
                    .ThenInclude(e => e.EducationalProgram)
                    .OrderByDescending(n => n.CreatedAt)
                    .ToListAsync()
            });
        }

        public async Task<IActionResult> Open(int id)
        {
            var userId = GetCurrentUserId();
            var notification = await _context.Notifications
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);

            if (notification == null)
                return NotFound();

            await _notificationService.MarkElementReadAsync(
                userId,
                notification.EducationalProgramElementId);

            var controller = User.IsInRole(AppRoles.Moderator)
                ? "ModeratorHome"
                : User.IsInRole(AppRoles.Approver)
                    ? "ApproverHome"
                    : "ManagerHome";

            return RedirectToAction(
                "History",
                controller,
                new { elementId = notification.EducationalProgramElementId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllRead()
        {
            await _notificationService.MarkAllReadAsync(GetCurrentUserId());
            return RedirectToAction(nameof(Index));
        }

        private int GetCurrentUserId()
        {
            return int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        }
    }
}
