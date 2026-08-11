using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PersonalCabinetEducationProgram.Models;
using PersonalCabinetEducationProgram.Services;
using PersonalCabinetEducationProgram.ViewModels;

namespace PersonalCabinetEducationProgram.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly IPasswordHasher<User> _identityPasswordHasher;

        public AccountController(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            IPasswordHasher<User> identityPasswordHasher)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _identityPasswordHasher = identityPasswordHasher;
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
        [ValidateAntiForgeryToken]
        [AppRateLimit(AppRateLimitPolicies.Login)]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.FindByNameAsync(model.Username);
            if (user == null)
            {
                AddInvalidCredentialsError();
                return View(model);
            }

            var check = await _signInManager.CheckPasswordSignInAsync(user, model.Password, lockoutOnFailure: true);
            if (!check.Succeeded && !check.IsLockedOut && IsLegacyPasswordValid(user, model.Password))
            {
                user.PasswordHash = _identityPasswordHasher.HashPassword(user, model.Password);
                user.SecurityStamp = Guid.NewGuid().ToString();
                var updateResult = await _userManager.UpdateAsync(user);
                if (updateResult.Succeeded)
                {
                    await _userManager.ResetAccessFailedCountAsync(user);
                    check = Microsoft.AspNetCore.Identity.SignInResult.Success;
                }
            }

            if (!check.Succeeded)
            {
                AddInvalidCredentialsError();
                return View(model);
            }

            if (user.ApprovalStatus == UserApprovalStatus.Pending)
            {
                ModelState.AddModelError(string.Empty, "Ваш аккаунт ожидает подтверждения администратором.");
                return View(model);
            }

            if (user.ApprovalStatus == UserApprovalStatus.Rejected)
            {
                var reason = string.IsNullOrWhiteSpace(user.RejectionReason)
                    ? string.Empty
                    : $" Причина: {user.RejectionReason}";
                ModelState.AddModelError(string.Empty, $"Ваш аккаунт отклонён администратором.{reason}");
                return View(model);
            }

            await _signInManager.SignInAsync(user, isPersistent: false);
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
        [ValidateAntiForgeryToken]
        [AppRateLimit(AppRateLimitPolicies.Registration)]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (model.RoleId == null || !AppRoles.SelfRegistrationIds.Contains(model.RoleId.Value))
            {
                ModelState.AddModelError(nameof(model.RoleId), "Выберите доступную роль.");
            }

            if (!ModelState.IsValid)
                return View(model);

            var roleId = model.RoleId ?? throw new InvalidOperationException("Роль не выбрана.");
            var roleName = roleId switch
            {
                AppRoles.ManagerId => AppRoles.Manager,
                AppRoles.ApproverId => AppRoles.Approver,
                AppRoles.ModeratorId => AppRoles.Moderator,
                _ => throw new InvalidOperationException("Недоступная роль.")
            };
            var user = new User
            {
                UserName = model.Username,
                FullName = model.FullName,
                Post = model.Post,
                ApprovalStatus = UserApprovalStatus.Pending
            };

            var createResult = await _userManager.CreateAsync(user, model.Password);
            if (!createResult.Succeeded)
            {
                AddIdentityErrors(createResult);
                return View(model);
            }

            var roleResult = await _userManager.AddToRoleAsync(user, roleName);
            if (!roleResult.Succeeded)
            {
                await _userManager.DeleteAsync(user);
                AddIdentityErrors(roleResult);
                return View(model);
            }

            TempData["SuccessMessage"] = "Регистрация завершена. Дождитесь подтверждения администратором.";
            return RedirectToAction(nameof(Login));
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AppRateLimit(AppRateLimitPolicies.Logout)]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
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

        private bool IsLegacyPasswordValid(User user, string password)
        {
            return !string.IsNullOrWhiteSpace(user.PasswordHash)
                && user.PasswordHash.Length == 64
                && LegacyPasswordHasher.Verify(password, user.PasswordHash);
        }

        private void AddInvalidCredentialsError()
        {
            ModelState.AddModelError(string.Empty, "Неверный логин или пароль.");
        }

        private void AddIdentityErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }
    }
}
