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

        public async Task<IActionResult> Index(
            bool unreadOnly = false, int page = 1, string sort = "date", string direction = "desc",
            [FromQuery] NotificationListFiltersViewModel? filters = null)
        {
            filters ??= new NotificationListFiltersViewModel();
            const int pageSize = 25;
            page = Math.Max(1, page);
            var userId = GetCurrentUserId();
            var baseQuery = _context.Notifications.Where(n => n.UserId == userId);
            var candidates = await baseQuery
                .Where(n => !unreadOnly || !n.IsRead)
                .Include(n => n.Element)
                .ThenInclude(e => e.EducationalProgram)
                .AsNoTracking()
                .ToListAsync();
            IEnumerable<Notification> filteredQuery = candidates.Where(notification =>
                ListFilterMatcher.AnyText([notification.Title, notification.Message], filters.Title) &&
                ListFilterMatcher.AnyText(
                    [notification.Element.EducationalProgram.CodeReferral, notification.Element.EducationalProgram.Name],
                    filters.Program) &&
                ListFilterMatcher.Text(notification.Element.Name, filters.Element) &&
                ListFilterMatcher.Text(notification.ActorName, filters.Actor) &&
                ListFilterMatcher.Date(notification.CreatedAt, filters.DateFrom, filters.DateTo) &&
                (string.IsNullOrWhiteSpace(filters.ReadStatus) ||
                    (filters.ReadStatus == NotificationListFiltersViewModel.Read && notification.IsRead) ||
                    (filters.ReadStatus == NotificationListFiltersViewModel.Unread && !notification.IsRead)));
            var descending = direction.Equals("desc", StringComparison.OrdinalIgnoreCase);
            filteredQuery = sort switch
            {
                "title" => descending ? filteredQuery.OrderByDescending(n => n.Title) : filteredQuery.OrderBy(n => n.Title),
                "program" => descending ? filteredQuery.OrderByDescending(n => n.Element.EducationalProgram.Name) : filteredQuery.OrderBy(n => n.Element.EducationalProgram.Name),
                "element" => descending ? filteredQuery.OrderByDescending(n => n.Element.Name) : filteredQuery.OrderBy(n => n.Element.Name),
                "actor" => descending ? filteredQuery.OrderByDescending(n => n.ActorName) : filteredQuery.OrderBy(n => n.ActorName),
                _ => descending ? filteredQuery.OrderByDescending(n => n.CreatedAt) : filteredQuery.OrderBy(n => n.CreatedAt)
            };
            var filteredCount = filteredQuery.Count();

            return View(new NotificationsViewModel
            {
                AllCount = await baseQuery.CountAsync(),
                UnreadCount = await baseQuery.CountAsync(n => !n.IsRead),
                UnreadOnly = unreadOnly,
                Page = page,
                TotalPages = Math.Max(1, (int)Math.Ceiling(filteredCount / (double)pageSize)),
                Sort = sort,
                Direction = descending ? "desc" : "asc",
                Filters = filters,
                Notifications = filteredQuery
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList()
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
