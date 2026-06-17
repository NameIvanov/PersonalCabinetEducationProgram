using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Data
{
    public class EducationalProgramRepository : IEducationalProgramRepository
    {
        private readonly ApplicationDbContext _db;
        public EducationalProgramRepository(ApplicationDbContext db)
        {
            _db = db;
        }
        public List<EducationalProgram> GetAll()
        {
            return _db.EducationalPrograms.ToList();
        }
        public EducationalProgram GetElementById(int educationalProgramId)
        {
            return _db.EducationalPrograms.FirstOrDefault(i => i.Id == educationalProgramId);
        }
    }
}
