using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.ViewModels
{
    public class NotificationsViewModel
    {
        public IReadOnlyList<Notification> Notifications { get; set; } = [];
        public int AllCount { get; set; }
        public int UnreadCount { get; set; }
        public bool UnreadOnly { get; set; }
    }
}
