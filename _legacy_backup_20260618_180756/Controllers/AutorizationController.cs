using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Controllers
{
    public class AutorizationController : Controller
    {

        private readonly ILogger<AutorizationController> _logger;
        public AutorizationController(ILogger<AutorizationController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }
        [HttpPost]
        public IActionResult Authorization(string login, string password, bool remember)
        {

            return RedirectToAction("Index");
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}