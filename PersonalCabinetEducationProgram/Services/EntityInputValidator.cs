namespace PersonalCabinetEducationProgram.Services
{
    public static class EntityInputValidator
    {
        public const int ProgramCodeMaxLength = 50;
        public const int ProgramNameMaxLength = 500;
        public const int EducationalLevelMaxLength = 100;
        public const int ElementNameMaxLength = 500;
        public const int ElementDescriptionMaxLength = 2000;
        public const int DepartmentCodeMaxLength = 50;
        public const int OrganizationNameMaxLength = 300;
        public const int UserFullNameMaxLength = 200;
        public const int UserPostMaxLength = 200;
        public const int UserNameMaxLength = 100;
        public const int CommentMaxLength = 4000;

        public static string? Program(string? code, string? name, string? level, int year)
        {
            return Required(code, "Шифр ОПОП", ProgramCodeMaxLength)
                ?? Required(name, "Наименование ОПОП", ProgramNameMaxLength)
                ?? Required(level, "Уровень образования", EducationalLevelMaxLength)
                ?? (year < 1900 || year > DateTime.UtcNow.Year + 10
                    ? $"Год утверждения должен быть от 1900 до {DateTime.UtcNow.Year + 10}."
                    : null);
        }

        public static string? Element(string? name, string? description) =>
            Required(name, "Наименование элемента", ElementNameMaxLength)
            ?? Optional(description, "Описание элемента", ElementDescriptionMaxLength);

        public static string? Department(string? code, string? name) =>
            Required(code, "Код кафедры", DepartmentCodeMaxLength)
            ?? Required(name, "Наименование кафедры", OrganizationNameMaxLength);

        public static string? Faculty(string? name) =>
            Required(name, "Наименование факультета", OrganizationNameMaxLength);

        public static string? User(string? fullName, string? post, string? username = null) =>
            Required(fullName, "ФИО", UserFullNameMaxLength)
            ?? Required(post, "Должность", UserPostMaxLength)
            ?? (username == null ? null : Required(username, "Имя пользователя", UserNameMaxLength));

        public static string? Comment(string? value) =>
            Required(value, "Комментарий", CommentMaxLength);

        private static string? Required(string? value, string label, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
                return $"Поле «{label}» обязательно.";
            return value.Trim().Length > maxLength
                ? $"Поле «{label}» не должно превышать {maxLength} символов."
                : null;
        }

        private static string? Optional(string? value, string label, int maxLength) =>
            value?.Trim().Length > maxLength
                ? $"Поле «{label}» не должно превышать {maxLength} символов."
                : null;
    }
}
