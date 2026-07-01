using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Data
{
    public class DepartmentsRepository : IDepartmentsRepository
    {
        private static List<Department> elements = new List<Department>();
        public List<Department> GetAll()
        {
            return elements;
        }
    }
}
