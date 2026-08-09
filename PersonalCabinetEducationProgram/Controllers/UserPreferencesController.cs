using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Controllers
{
    [Authorize]
    [Route("user-preferences")]
    public class UserPreferencesController : Controller
    {
        private readonly UserManager<User> _userManager;

        public UserPreferencesController(UserManager<User> userManager)
        {
            _userManager = userManager;
        }

        [HttpPost("theme")]
        public async Task<IActionResult> SetTheme([FromForm] string theme)
        {
            if (!UserTheme.IsValid(theme))
                return BadRequest("Неизвестная цветовая тема.");

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized();

            if (user.PreferredTheme == theme)
                return NoContent();

            user.PreferredTheme = theme;
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
                return Problem("Не удалось сохранить цветовую тему.");

            return NoContent();
        }
    }
}
