using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Data
{
    public class FacultysRepository : IFacultysRepository
    {
        private static List<Faculty> elements = new List<Faculty>();
        public List<Faculty> GetAll()
        {
            return elements;
        }
    }
}
