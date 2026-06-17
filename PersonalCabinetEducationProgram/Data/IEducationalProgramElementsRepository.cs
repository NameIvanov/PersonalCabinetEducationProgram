using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Data
{
    public interface IEducationalProgramElementsRepository
    {
        public List<EducationalProgramElement> GetAll();
        public EducationalProgramElement GetElementById(int educationalProgramId);
    }
}
