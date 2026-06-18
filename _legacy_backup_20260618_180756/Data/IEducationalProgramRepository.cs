using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Data
{
    public interface IEducationalProgramRepository
    {
        private static List<EducationalProgram> elements = new List<EducationalProgram>();
        public List<EducationalProgram> GetAll();
        public EducationalProgram GetElementById(int educationalProgramId);

    }
}
