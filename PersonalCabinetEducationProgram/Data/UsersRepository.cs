using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Data
{
    public class UsersRepository : IUsersRepository
    {
        private readonly ApplicationDbContext _db;
        public UsersRepository(ApplicationDbContext db)
        {
            _db = db;
        }
        public List<User> GetAll()
        {
            return _db.Users.ToList();
        }
    }
}
