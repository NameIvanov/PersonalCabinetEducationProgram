using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PersonalCabinetEducationProgram.Data;
using PersonalCabinetEducationProgram.Models;
using PersonalCabinetEducationProgram.Services;
using PersonalCabinetEducationProgram.ViewModels;

namespace PersonalCabinetEducationProgram.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AccountController(ApplicationDbContext context)
        {
            _context = context;
        }

        [AllowAnonymous]
        public IActionResult Login()
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction(nameof(RedirectByRole));

            return View(new LoginViewModel());
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Username == model.Username);

            if (user == null || !PasswordHasher.Verify(model.Password, user.PasswordHash))
            {
                ModelState.AddModelError(string.Empty, "Неверный логин или пароль.");
                return View(model);
            }

            if (user.ApprovalStatus == UserApprovalStatus.Pending)
            {
                ModelState.AddModelError(string.Empty, "Ваш аккаунт ожидает подтверждения модератором.");
                return View(model);
            }

            if (user.ApprovalStatus == UserApprovalStatus.Rejected)
            {
                var reason = string.IsNullOrWhiteSpace(user.RejectionReason) ? string.Empty : $" Причина: {user.RejectionReason}";
                ModelState.AddModelError(string.Empty, $"Ваш аккаунт отклонен модератором.{reason}");
                return View(model);
            }

            var roleName = user.Role?.Name ?? user.LinkRole;
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.FullName),
                new(ClaimTypes.Role, roleName),
                new("Username", user.Username),
                new("Post", user.Post)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity));

            return RedirectToAction(nameof(RedirectByRole));
        }

        [AllowAnonymous]
        public IActionResult Register()
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction(nameof(RedirectByRole));

            return View(new RegisterViewModel());
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!AppRoles.SelfRegistration.Contains(model.Role))
            {
                ModelState.AddModelError(nameof(model.Role), "Можно зарегистрироваться только как руководитель или согласующий.");
            }

            if (!ModelState.IsValid)
                return View(model);

            var usernameExists = await _context.Users.AnyAsync(u => u.Username == model.Username);
            if (usernameExists)
            {
                ModelState.AddModelError(nameof(model.Username), "Этот логин уже занят.");
                return View(model);
            }

            _context.Users.Add(new User
            {
                Username = model.Username,
                PasswordHash = PasswordHasher.Hash(model.Password),
                FullName = model.FullName,
                Post = model.Post,
                LinkRole = model.Role,
                RoleId = model.Role == AppRoles.Manager ? 1 : 2,
                ApprovalStatus = UserApprovalStatus.Pending
            });

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Регистрация завершена. Дождитесь подтверждения модератором.";
            return RedirectToAction(nameof(Login));
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        [Authorize]
        public IActionResult RedirectByRole()
        {
            if (User.IsInRole(AppRoles.Admin))
                return RedirectToAction("Users", "Admin");

            if (User.IsInRole(AppRoles.Moderator))
                return RedirectToAction("Index", "ModeratorHome");

            if (User.IsInRole(AppRoles.Approver))
                return RedirectToAction("Index", "ApproverHome");

            return RedirectToAction("Index", "ManagerHome");
        }

        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
