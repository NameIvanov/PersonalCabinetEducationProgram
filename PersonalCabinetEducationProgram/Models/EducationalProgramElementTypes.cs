namespace PersonalCabinetEducationProgram.Models
{
    public static class EducationalProgramElementTypes
    {
        public const string Main = "Main";
        public const string Discipline = "Discipline";
        public const string Module = "Module";
        public const string Practice = "Practice";
        public const string Coursework = "Coursework";
        public const string Gia = "GIA";

        public static readonly string[] All = [Main, Discipline, Module, Practice, Coursework, Gia];

        public static string GetDisplayName(string type) => type switch
        {
            Main => "Основные документы",
            Discipline => "Дисциплины",
            Module => "Модули",
            Practice => "Практики",
            Coursework => "Курсовые работы",
            Gia => "ГИА",
            _ => type
        };
    }
}
