using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
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
        private readonly SecurityEventService _securityEventService;
        private readonly IIpGeolocationService _ipGeolocationService;
        private readonly string _allowedCountryCode;

        public AccountController(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            IPasswordHasher<User> identityPasswordHasher,
            SecurityEventService securityEventService,
            IIpGeolocationService ipGeolocationService,
            IOptions<SecurityMonitoringOptions> securityMonitoringOptions)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _identityPasswordHasher = identityPasswordHasher;
            _securityEventService = securityEventService;
            _ipGeolocationService = ipGeolocationService;
            _allowedCountryCode = securityMonitoringOptions.Value.IpGeolocation.AllowedCountryCode;
        }

        [AllowAnonymous]
        public IActionResult Login(bool securityBlocked = false)
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction(nameof(RedirectByRole));

            if (securityBlocked)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Учётная запись заблокирована службой безопасности. Обратитесь к администратору.");
            }

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
                _securityEventService.Record(
                    SecurityEventTypes.LoginFailed,
                    SecurityEventSeverities.Warning,
                    "Неудачная попытка входа",
                    "Пользователь с указанным логином не найден.",
                    userLogin: model.Username);
                await RecordForeignLoginAsync(null, model.Username, succeeded: false, locked: false);
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
                _securityEventService.Record(
                    check.IsLockedOut ? SecurityEventTypes.AccountLocked : SecurityEventTypes.LoginFailed,
                    check.IsLockedOut ? SecurityEventSeverities.High : SecurityEventSeverities.Warning,
                    check.IsLockedOut ? "Учётная запись заблокирована" : "Неудачная попытка входа",
                    check.IsLockedOut
                        ? "Превышено допустимое количество неудачных попыток входа."
                        : "Указан неверный пароль.",
                    user.Id,
                    user.UserName,
                    user.FullName);
                await RecordForeignLoginAsync(user, model.Username, succeeded: false, locked: check.IsLockedOut);
                if (check.IsLockedOut)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        "Учётная запись заблокирована. Обратитесь к администратору или повторите попытку позже.");
                }
                else
                {
                    AddInvalidCredentialsError();
                }
                return View(model);
            }

            if (user.ApprovalStatus == UserApprovalStatus.Pending)
            {
                RecordRejectedLogin(user, "Учётная запись ещё не подтверждена администратором.");
                await RecordForeignLoginAsync(user, model.Username, succeeded: false, locked: false);
                ModelState.AddModelError(string.Empty, "Ваш аккаунт ожидает подтверждения администратором.");
                return View(model);
            }

            if (user.ApprovalStatus == UserApprovalStatus.Rejected)
            {
                RecordRejectedLogin(user, "Учётная запись отклонена администратором.");
                await RecordForeignLoginAsync(user, model.Username, succeeded: false, locked: false);
                var reason = string.IsNullOrWhiteSpace(user.RejectionReason)
                    ? string.Empty
                    : $" Причина: {user.RejectionReason}";
                ModelState.AddModelError(string.Empty, $"Ваш аккаунт отклонён администратором.{reason}");
                return View(model);
            }

            await _signInManager.SignInAsync(user, isPersistent: false);
            _securityEventService.Record(
                SecurityEventTypes.LoginSucceeded,
                SecurityEventSeverities.Information,
                "Успешный вход",
                userId: user.Id,
                userLogin: user.UserName,
                userFullName: user.FullName);
            await RecordForeignLoginAsync(user, model.Username, succeeded: true, locked: false);
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

            _securityEventService.Record(
                SecurityEventTypes.Registration,
                SecurityEventSeverities.Information,
                "Создана новая учётная запись",
                $"Запрошена роль: {roleName}.",
                user.Id,
                user.UserName,
                user.FullName);

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

        private void RecordRejectedLogin(User user, string description)
        {
            _securityEventService.Record(
                SecurityEventTypes.AccessDenied,
                SecurityEventSeverities.Warning,
                "Вход отклонён по статусу учётной записи",
                description,
                user.Id,
                user.UserName,
                user.FullName);
        }

        private async Task RecordForeignLoginAsync(
            User? user,
            string attemptedLogin,
            bool succeeded,
            bool locked)
        {
            var lookup = await _ipGeolocationService.LookupAsync(
                HttpContext.Connection.RemoteIpAddress,
                HttpContext.RequestAborted);
            if (!lookup.IsPublicAddress || !lookup.WasResolved ||
                string.Equals(lookup.CountryCode, _allowedCountryCode, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var severity = locked
                ? SecurityEventSeverities.Critical
                : succeeded
                    ? SecurityEventSeverities.Warning
                    : SecurityEventSeverities.High;
            var title = locked
                ? "Заблокирован вход с иностранного IP"
                : succeeded
                    ? "Вход с иностранного IP"
                    : "Неудачный вход с иностранного IP";
            var country = string.IsNullOrWhiteSpace(lookup.CountryName)
                ? lookup.CountryCode
                : $"{lookup.CountryName} ({lookup.CountryCode})";

            _securityEventService.Record(
                SecurityEventTypes.ForeignLogin,
                severity,
                title,
                $"Страна IP-адреса: {country}. Результат входа: " +
                (locked ? "учётная запись заблокирована" : succeeded ? "успешно" : "отказ"),
                user?.Id,
                user?.UserName ?? attemptedLogin,
                user?.FullName);
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
