namespace PersonalCabinetEducationProgram.Models
{
    public static class UserTheme
    {
        public const string Light = "light";
        public const string Dark = "dark";

        public static bool IsValid(string? theme) => theme is Light or Dark;
    }
}
