using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.ViewModels
{
    public class ManagerHomeViewModel
    {
        public List<EducationalProgram> EducationalPrograms { get; set; }
        public List<EducationalProgram> AllPrograms { get; set; }
        public List<EducationalProgramElement> Elements { get; set; }
        public List<Department> Departments { get; set; }
        public List<Faculty> Faculties { get; set; }
        public List<User> Users { get; set; }
        public List<PinningDepartmentFaculty> PinningDepartmentFaculties { get; set; }
        public string CurrentRole { get; set; }
        public string CurrentUserName { get; set; }
        public int? SelectedYear { get; set; }

        public EducationalProgram CurrentProgram =>
            SelectedYear.HasValue
                ? EducationalPrograms?.FirstOrDefault(p => p.YearApprovals?.Year == SelectedYear.Value)
                : EducationalPrograms?.FirstOrDefault();
        public bool IsAdmin => CurrentRole == "Администратор";
        public bool IsApprover => CurrentRole == "Согласующий";
        public bool IsModerator => CurrentRole == "Модератор";
        public bool IsManager => CurrentRole == "РуководительОПОП";
        public bool IsAdminOrStaff => IsAdmin || IsApprover || IsModerator;

        public int[] GetYearsForProgram(EducationalProgram program)
        {
            int startYear = program.YearApprovals?.Year ?? DateTime.Now.Year;
            int currentYear = DateTime.Now.Year + 1;
            List<int> years = new List<int>();
            for (int y = currentYear; y >= startYear; y--)
                years.Add(y);
            return years.ToArray();
        }

        public Dictionary<string, int[]> GetStats()
        {
            string[] statuses = { "Новые", "Согласовано", "Не согласовано", "На сайте" };
            string[] typeCols = { "Дисциплина", "Практика", "Программа ГИА" };
            var result = new Dictionary<string, int[]>();
            foreach (var status in statuses)
            {
                int discipline = Elements?.Count(e => e.StatusApprovals == status && e.TypeElement == "Дисциплина") ?? 0;
                int practice = Elements?.Count(e => e.StatusApprovals == status && e.TypeElement == "Практика") ?? 0;
                int gia = Elements?.Count(e => e.StatusApprovals == status && e.TypeElement == "Программа ГИА") ?? 0;
                result[status] = new[] { discipline, practice, gia, discipline + practice + gia };
            }
            return result;
        }
    }
}
