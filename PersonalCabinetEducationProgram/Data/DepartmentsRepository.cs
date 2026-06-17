using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Data
{
    public class DepartmentsRepository : IDepartmentsRepository
    {
        private readonly ApplicationDbContext _db;
        public DepartmentsRepository(ApplicationDbContext db)
        {
            _db = db;
        }
        public List<Department> GetAll()
        {
            return _db.Departments.ToList();
        }
    }
}
