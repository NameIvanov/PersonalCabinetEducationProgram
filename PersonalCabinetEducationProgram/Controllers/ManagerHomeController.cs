using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PersonalCabinetEducationProgram.Data;
using PersonalCabinetEducationProgram.Models;
using PersonalCabinetEducationProgram.ViewModels;

namespace PersonalCabinetEducationProgram.Controllers
{
    [Authorize]
    public class ManagerHomeController : Controller
    {
        private readonly IEducationalProgramRepository _educationalProgramRepository;
        private readonly IEducationalProgramElementsRepository _educationalProgramElementsRepository;
        private readonly IDepartmentsRepository _departmentsRepository;
        private readonly IFacultysRepository _facultysRepository;
        private readonly IUsersRepository _usersRepository;
        private readonly UserManager<User> _userManager;
        private readonly ApplicationDbContext _db;

        public ManagerHomeController(
            IEducationalProgramRepository educationalProgramRepository,
            IEducationalProgramElementsRepository educationalProgramElementsRepository,
            IDepartmentsRepository departmentsRepository,
            IFacultysRepository facultysRepository,
            IUsersRepository usersRepository,
            UserManager<User> userManager,
            ApplicationDbContext db)
        {
            _educationalProgramRepository = educationalProgramRepository;
            _educationalProgramElementsRepository = educationalProgramElementsRepository;
            _departmentsRepository = departmentsRepository;
            _facultysRepository = facultysRepository;
            _usersRepository = usersRepository;
            _userManager = userManager;
            _db = db;
        }

        public async Task<IActionResult> Index(int? year)
        {
            var user = await _userManager.GetUserAsync(User);
            var roles = await _userManager.GetRolesAsync(user);
            var role = roles.FirstOrDefault();

            var allElements = _educationalProgramElementsRepository.GetAll();
            var departments = _departmentsRepository.GetAll();
            var faculties = _facultysRepository.GetAll();
            var users = _usersRepository.GetAll();
            var programs = _educationalProgramRepository.GetAll();

            List<EducationalProgram> myPrograms;

            if (role == "РуководительОПОП")
            {
                var myProgramIds = _db.EducationalProgramManagers
                    .Where(m => m.UserId == user.Id)
                    .Select(m => m.EducationalProgramId)
                    .ToList();
                myPrograms = programs.Where(p => myProgramIds.Contains(p.Id)).ToList();
            }
            else
            {
                myPrograms = programs;
            }

            if (year.HasValue)
            {
                var programIdsForYear = programs
                    .Where(p => p.YearApprovals.HasValue && p.YearApprovals.Value.Year == year.Value)
                    .Select(p => p.Id)
                    .ToHashSet();
                allElements = allElements.Where(e => programIdsForYear.Contains(e.EducationalProgramId)).ToList();
            }

            var pinnings = _db.PinningDepartmentFaculties.ToList();

            var viewModel = new ManagerHomeViewModel
            {
                Elements = allElements,
                Departments = departments,
                Faculties = faculties,
                Users = users,
                EducationalPrograms = myPrograms,
                AllPrograms = programs,
                PinningDepartmentFaculties = pinnings,
                CurrentRole = role,
                CurrentUserName = user.FullName,
                SelectedYear = year
            };

            return View(viewModel);
        }

        public async Task<IActionResult> Upload(IFormFile file)
        {
            return View("Index");
        }
    }
}
