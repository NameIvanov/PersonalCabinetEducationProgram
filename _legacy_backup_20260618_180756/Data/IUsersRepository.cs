using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Data
{
    public interface IUsersRepository
    {
        private static List<User> elements = new List<User>();
        public List<User> GetAll();

    }
}
