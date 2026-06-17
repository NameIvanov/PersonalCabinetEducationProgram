using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Data
{
    public class EducationalProgramElementsRepository : IEducationalProgramElementsRepository
    {
        private readonly ApplicationDbContext _db;
        public EducationalProgramElementsRepository(ApplicationDbContext db)
        {
            _db = db;
        }
        public List<EducationalProgramElement> GetAll()
        {
            return _db.EducationalProgramElements.ToList();
        }
        public EducationalProgramElement GetElementById(int educationalProgramId)
        {
            return _db.EducationalProgramElements.FirstOrDefault(i => i.EducationalProgramId == educationalProgramId);
        }
    }
}
