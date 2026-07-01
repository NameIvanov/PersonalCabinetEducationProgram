using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Data
{
    public class EducationalProgramElementsRepository : IEducationalProgramElementsRepository
    {
        private static List<EducationalProgramElement> elements = new List<EducationalProgramElement>()
        {
            new EducationalProgramElement()
            {
                Id=0,
                EducationalProgramId=0,
                TypeElement = "учебный план",
                Name = "учебный план(очный)",
                UploadDate = new DateOnly(2024, 2, 1),
                StatusApprovals = "на согласовании"
            },
            new EducationalProgramElement()
            {
                Id=0,
                EducationalProgramId=0,
                TypeElement = "Дисциплина",
                Name = "Дисциплина",
                UploadDate = new DateOnly(2024, 2, 1),
                StatusApprovals = "на согласовании"

            },
            new EducationalProgramElement()
            {
                Id=0,
                EducationalProgramId=0,
                TypeElement = "Практика",
                Name = "Практика",
                UploadDate = new DateOnly(2024, 2, 1),
                StatusApprovals = "на согласовании"

            },
            new EducationalProgramElement()
            {
                Id=0,
                EducationalProgramId=0,
                TypeElement = "Программа ГИА",
                Name = "Программа ГИА",
                UploadDate = new DateOnly(2024, 2, 1),
                StatusApprovals = "на согласовании"

            },
            new EducationalProgramElement()
            {
                Id=0,
                EducationalProgramId=0,
                TypeElement = "Методический материал",
                Name = "Методический материал",
                UploadDate = new DateOnly(2024, 2, 1),
                StatusApprovals = "на согласовании"

            }


        };
        public List<EducationalProgramElement> GetAll()
        {
            return elements;
        }
        public EducationalProgramElement GetElementById(int educationalProgramId)
        {
            return elements.FirstOrDefault(i => i.EducationalProgramId == educationalProgramId);
        }
    }
}
