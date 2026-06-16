using Microsoft.AspNetCore.Mvc;
using PersonalCabinetEducationProgram.Data;
using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Controllers
{
    public class ManagerHomeController : Controller
    {
        IEducationalProgramRepository _educationalProgramRepository;
        IEducationalProgramElementsRepository _educationalProgramElementsRepository;
        public ManagerHomeController(IEducationalProgramRepository educationalProgramRepository, IEducationalProgramElementsRepository educationalProgramElementsRepository)
        {
            _educationalProgramRepository = educationalProgramRepository;
            _educationalProgramElementsRepository = educationalProgramElementsRepository;
        }
        public IActionResult Index()
        {
            var educationalProgramElements = _educationalProgramElementsRepository.GetAll();
            return View(educationalProgramElements);
        }
        //[HttpPost]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            //string uploadsFolder = Path.Combine(_env.WebRootPath, "uploads");
            //Directory.CreateDirectory(uploadsFolder);

            //string uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
            //string filePath = Path.Combine(uploadsFolder, uniqueFileName);

            //using (var fileStream = new FileStream(filePath, FileMode.Create))
            //{
            //    await file.CopyToAsync(fileStream);
            //}
            //ViewBag.FilePath = $"/uploads/{uniqueFileName}";
            return View("Index");
        }
    }
}
