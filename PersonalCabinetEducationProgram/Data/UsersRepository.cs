using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Data
{
    public class UsersRepository : IUsersRepository
    {
        private static List<User> elements = new List<User>();
        public List<User> GetAll()
        {
            return elements;
        }
    }
}
