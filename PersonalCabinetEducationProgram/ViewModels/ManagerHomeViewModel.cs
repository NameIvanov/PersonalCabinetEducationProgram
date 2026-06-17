using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.ViewModels
{
    public class ManagerHomeViewModel
    {
        public List<EducationalProgram> EducationalPrograms { get; set; }
        public List<EducationalProgramElement> Elements { get; set; }
        public List<Department> Departments { get; set; }
        public List<Faculty> Faculties { get; set; }

        public EducationalProgram CurrentProgram => EducationalPrograms?.FirstOrDefault();
        public int[] GetYearsForProgram(EducationalProgram program)
        {
            int startYear = program.YearApprovals?.Year ?? DateTime.Now.Year;
            int currentYear = DateTime.Now.Year + 1;
            List<int> years = new List<int>();
            for (int y = currentYear; y >= startYear; y--)
                years.Add(y);
            return years.ToArray();
        }
    }
}
