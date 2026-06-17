using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Data
{
    public interface IUsersRepository
    {
        public List<User> GetAll();
    }
}
