using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Data
{
    public interface IDepartmentsRepository
    {
        private static List<Department> elements = new List<Department>();
        public List<Department> GetAll();

    }
}
