namespace PersonalCabinetEducationProgram.Models
{
    public static class AppRoles
    {
        public const string Manager = "Manager";
        public const string Approver = "Approver";
        public const string Moderator = "Moderator";
        public const string Admin = "Admin";

        public static readonly string[] All = [Manager, Approver, Moderator, Admin];
        public static readonly string[] SelfRegistration = [Manager, Approver];
    }
}
