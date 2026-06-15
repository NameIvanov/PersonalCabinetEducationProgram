using Microsoft.AspNetCore.Mvc;

namespace PersonalCabinetEducationProgram.Controllers
{
    public class ManagerHomeController : Controller
    {
        private readonly IWebHostEnvironment _env;

        public ManagerHomeController(IWebHostEnvironment env)
        {
            _env = env;
        }
        public IActionResult Index()
        {
            return View();
        }
        [HttpPost]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            string uploadsFolder = Path.Combine(_env.WebRootPath, "uploads");
            Directory.CreateDirectory(uploadsFolder);

            string uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }
            ViewBag.FilePath = $"/uploads/{uniqueFileName}";
            return View("Index");
        }
    }
}
