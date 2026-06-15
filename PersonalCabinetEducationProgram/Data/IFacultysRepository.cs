using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Data
{
    public interface IFacultysRepository
    {
        private static List<Faculty> elements = new List<Faculty>();
        public List<Faculty> GetAll();

    }
}
