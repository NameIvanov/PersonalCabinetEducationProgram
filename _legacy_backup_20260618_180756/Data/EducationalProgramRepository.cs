using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Data
{
    public class EducationalProgramRepository : IEducationalProgramRepository
    {
        private static List<EducationalProgram> elements = new List<EducationalProgram>();
        public List<EducationalProgram> GetAll()
        {
            return elements;
        }
        public EducationalProgram GetElementById(int educationalProgramId)
        {
            return elements.FirstOrDefault(i => i.Id == educationalProgramId);
        }
    }
}
