using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Data
{
    public interface IEducationalProgramElementsRepository
    {
        private static List<EducationalProgramElement> elements;
        public List<EducationalProgramElement> GetAll();
        public EducationalProgramElement GetElementById(int educationalProgramId);
    }
}
