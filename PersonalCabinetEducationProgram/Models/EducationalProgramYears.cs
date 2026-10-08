namespace PersonalCabinetEducationProgram.Models;

public static class EducationalProgramYears
{
    public const int FirstYear = 2020;
    public const int ExistingProgramsYear = 2026;
    public static int CurrentYear => Math.Max(ExistingProgramsYear, DateTime.UtcNow.Year);
    public static int LastSelectableYear => CurrentYear + 10;
}
