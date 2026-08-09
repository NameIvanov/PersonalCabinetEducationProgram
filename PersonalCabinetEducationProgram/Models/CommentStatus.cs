namespace PersonalCabinetEducationProgram.Models
{
    public static class CommentStatus
    {
        public const string New = "Новый";
        public const string Read = "Прочитан";
        public const string Done = "Выполнен";

        public static readonly string[] All = [New, Read, Done];
    }
}
