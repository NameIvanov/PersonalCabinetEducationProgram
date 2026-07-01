namespace PersonalCabinetEducationProgram.Models
{
    public static class AppRoles
    {
        public const int ManagerId = 1;
        public const int ApproverId = 2;
        public const int ModeratorId = 3;
        public const int AdminId = 4;

        public const string Manager = "Manager";
        public const string Approver = "Approver";
        public const string Moderator = "Moderator";
        public const string Admin = "Admin";

        public static readonly string[] All = [Manager, Approver, Moderator, Admin];
        public static readonly int[] AllIds = [ManagerId, ApproverId, ModeratorId, AdminId];
        public static readonly int[] AssignableIds = [ManagerId, ApproverId, ModeratorId];
        public static readonly int[] SelfRegistrationIds = [ManagerId, ApproverId, ModeratorId];
    }
}
