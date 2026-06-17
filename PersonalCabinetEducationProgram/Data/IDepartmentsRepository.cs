using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Data
{
    public interface IDepartmentsRepository
    {
        public List<Department> GetAll();
    }
}
