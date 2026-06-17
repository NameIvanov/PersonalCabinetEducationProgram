using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Data
{
    public interface IEducationalProgramRepository
    {
        public List<EducationalProgram> GetAll();
        public EducationalProgram GetElementById(int educationalProgramId);
    }
}
