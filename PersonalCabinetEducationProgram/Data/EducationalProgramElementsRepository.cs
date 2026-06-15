using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Data
{
    public class EducationalProgramElementsRepository : IEducationalProgramElementsRepository
    {
        private static List<EducationalProgramElement> elements = new List<EducationalProgramElement>();
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
