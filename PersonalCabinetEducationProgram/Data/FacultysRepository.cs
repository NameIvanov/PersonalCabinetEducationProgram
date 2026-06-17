using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Data
{
    public class FacultysRepository : IFacultysRepository
    {
        private readonly ApplicationDbContext _db;
        public FacultysRepository(ApplicationDbContext db)
        {
            _db = db;
        }
        public List<Faculty> GetAll()
        {
            return _db.Faculties.ToList();
        }
    }
}
