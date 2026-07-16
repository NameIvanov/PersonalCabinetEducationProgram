using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.ViewModels
{
    public class NotificationsViewModel
    {
        public IReadOnlyList<Notification> Notifications { get; set; } = [];
        public int AllCount { get; set; }
        public int UnreadCount { get; set; }
        public bool UnreadOnly { get; set; }
        public int Page { get; set; }
        public int TotalPages { get; set; }
        public string Sort { get; set; } = "date";
        public string Direction { get; set; } = "desc";
        public NotificationListFiltersViewModel Filters { get; set; } = new();
    }
}
