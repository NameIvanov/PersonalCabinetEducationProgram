using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Data
{
    public interface IFacultysRepository
    {
        public List<Faculty> GetAll();
    }
}
