using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PersonalCabinetEducationProgram.Data;
using PersonalCabinetEducationProgram.Models;
using PersonalCabinetEducationProgram.Services;
using PersonalCabinetEducationProgram.ViewModels;

namespace PersonalCabinetEducationProgram.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IFileStorageService _fileStorageService;
        private readonly FileStorageSettings _storageSettings;
        private readonly ElementWorkflowService _workflowService;
        private readonly UserManager<User> _userManager;
        private readonly NotificationService _notificationService;
        private readonly AuditService _auditService;
        private readonly ElementFilterService _elementFilterService;
        private readonly AccountSecurityService _accountSecurityService;

        public AdminController(
            ApplicationDbContext context,
            IFileStorageService fileStorageService,
            IOptions<FileStorageSettings> storageSettings,
            ElementWorkflowService workflowService,
            UserManager<User> userManager,
            NotificationService notificationService,
            AuditService auditService,
            ElementFilterService elementFilterService,
            AccountSecurityService accountSecurityService)
        {
            _context = context;
            _fileStorageService = fileStorageService;
            _storageSettings = storageSettings.Value;
            _workflowService = workflowService;
            _userManager = userManager;
            _notificationService = notificationService;
            _auditService = auditService;
            _elementFilterService = elementFilterService;
            _accountSecurityService = accountSecurityService;
        }

        private int GetCurrentUserId()
        {
            return int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        }

        [Authorize(Roles = "Admin")]
        [AppRateLimit(AppRateLimitPolicies.Search)]
        public async Task<IActionResult> Users(
            int page = 1, string sort = "id", string direction = "asc",
            [FromQuery] UserListFiltersViewModel? filters = null)
        {
            filters ??= new UserListFiltersViewModel();
            const int pageSize = 25;
            page = Math.Max(page, 1);
            var roleRows = await (
                from userRole in _context.UserRoles
                join role in _context.Roles on userRole.RoleId equals role.Id
                select new { userRole.UserId, RoleName = role.Name ?? string.Empty })
                .ToListAsync();
            var rolesByUser = roleRows
                .GroupBy(row => row.UserId)
                .ToDictionary(group => group.Key, group => group.First().RoleName);
            var allUsers = await _userManager.Users.AsNoTracking().ToListAsync();
            foreach (var user in allUsers)
                user.RoleName = rolesByUser.GetValueOrDefault(user.Id, string.Empty);

            IEnumerable<User> query = allUsers.Where(user =>
                (!filters.Id.HasValue || user.Id == filters.Id.Value) &&
                ListFilterMatcher.Text(user.UserName, filters.Login) &&
                ListFilterMatcher.Text(user.FullName, filters.FullName) &&
                ListFilterMatcher.Text(user.Post, filters.Post) &&
                ListFilterMatcher.Exact(user.RoleName, filters.Role) &&
                (filters.ApprovalStatus == "SecurityBlocked"
                    ? user.SecurityBlockedAtUtc.HasValue || user.LockoutEnd > DateTimeOffset.UtcNow
                    : ListFilterMatcher.Exact(user.ApprovalStatus, filters.ApprovalStatus)));
            var descending = direction.Equals("desc", StringComparison.OrdinalIgnoreCase);
            query = sort switch
            {
                "login" => descending ? query.OrderByDescending(u => u.UserName) : query.OrderBy(u => u.UserName),
                "name" => descending ? query.OrderByDescending(u => u.FullName) : query.OrderBy(u => u.FullName),
                "post" => descending ? query.OrderByDescending(u => u.Post) : query.OrderBy(u => u.Post),
                "role" => descending
                    ? query.OrderByDescending(u => _context.Roles.Where(role => _context.UserRoles.Any(link => link.UserId == u.Id && link.RoleId == role.Id)).Select(role => role.Name).FirstOrDefault())
                    : query.OrderBy(u => _context.Roles.Where(role => _context.UserRoles.Any(link => link.UserId == u.Id && link.RoleId == role.Id)).Select(role => role.Name).FirstOrDefault()),
                "status" => descending ? query.OrderByDescending(u => u.ApprovalStatus) : query.OrderBy(u => u.ApprovalStatus),
                _ => descending ? query.OrderByDescending(u => u.Id) : query.OrderBy(u => u.Id)
            };
            var totalCount = query.Count();
            var users = query.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            SetPagination(totalCount, page, pageSize, sort, direction);
            ViewBag.Filters = filters;

            return View(users);
        }

        [Authorize(Roles = "Admin")]
        [AppRateLimit(AppRateLimitPolicies.Search)]
        public async Task<IActionResult> Audit(
            int page = 1, string sort = "date", string direction = "desc",
            [FromQuery] AuditListFiltersViewModel? filters = null)
        {
            filters ??= new AuditListFiltersViewModel();
            const int pageSize = 50;
            page = Math.Max(page, 1);
            var entriesQuery = await _context.AuditLogs
                .AsNoTracking()
                .ToListAsync();
            IEnumerable<AuditLog> query = entriesQuery.Where(entry =>
                ListFilterMatcher.Date(entry.CreatedAt, filters.DateFrom, filters.DateTo) &&
                ListFilterMatcher.AnyText([entry.UserFullName, entry.UserLogin, entry.UserId.ToString()], filters.User) &&
                ListFilterMatcher.Text($"{entry.EntityType} #{entry.EntityId}", filters.Entity) &&
                ListFilterMatcher.Text(entry.Action, filters.Action) &&
                ListFilterMatcher.Text(entry.Details, filters.Details));
            var descending = direction.Equals("desc", StringComparison.OrdinalIgnoreCase);
            query = sort switch
            {
                "user" => descending ? query.OrderByDescending(a => a.UserFullName) : query.OrderBy(a => a.UserFullName),
                "entity" => descending ? query.OrderByDescending(a => a.EntityType).ThenByDescending(a => a.EntityId) : query.OrderBy(a => a.EntityType).ThenBy(a => a.EntityId),
                "action" => descending ? query.OrderByDescending(a => a.Action) : query.OrderBy(a => a.Action),
                "details" => descending ? query.OrderByDescending(a => a.Details) : query.OrderBy(a => a.Details),
                _ => descending ? query.OrderByDescending(a => a.CreatedAt) : query.OrderBy(a => a.CreatedAt)
            };
            var totalCount = query.Count();
            var entries = query.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            SetPagination(totalCount, page, pageSize, sort, direction);
            ViewBag.Filters = filters;

            return View(entries);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [AppRateLimit(AppRateLimitPolicies.AdminUserMutation)]
        public async Task<IActionResult> ChangeApprovalStatus(int id, string approvalStatus, string? rejectionReason)
        {
            if (approvalStatus is not (UserApprovalStatus.Pending or UserApprovalStatus.Approved or UserApprovalStatus.Rejected))
                return BadRequest("Неизвестный статус учётной записи.");
            if (rejectionReason?.Length > 1000)
                return BadRequest("Причина отклонения не должна превышать 1000 символов.");

            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null)
                return NotFound();

            if (GetCurrentUserId() == id && approvalStatus == UserApprovalStatus.Rejected)
            {
                TempData["UsersError"] = "Нельзя отклонить собственный аккаунт.";
                return RedirectToAction(nameof(Users));
            }

            var previousApproval = new { user.ApprovalStatus, user.RejectionReason };
            user.ApprovalStatus = approvalStatus;
            user.RejectionReason = approvalStatus == UserApprovalStatus.Rejected ? rejectionReason : null;

            await _userManager.UpdateAsync(user);
            await _userManager.UpdateSecurityStampAsync(user);
            _auditService.Record(GetCurrentUserId(), "User", user.Id, "ApprovalStatusChanged",
                $"Статус учётной записи изменён на «{approvalStatus}».",
                previousApproval,
                new { user.ApprovalStatus, user.RejectionReason });
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Users));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        [AppRateLimit(AppRateLimitPolicies.AdminUserCreate)]
        public async Task<IActionResult> CreateUser(
            string fullName,
            int roleId,
            string post,
            string username,
            string password,
            string confirmPassword)
        {
            if (!AppRoles.AssignableIds.Contains(roleId))
                return BadRequest();

            var validationError = EntityInputValidator.User(fullName, post, username);
            if (validationError != null)
            {
                TempData["UsersError"] = validationError;
                return RedirectToAction(nameof(Users));
            }

            if (string.IsNullOrWhiteSpace(fullName) ||
                string.IsNullOrWhiteSpace(post) ||
                string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(password))
            {
                TempData["UsersError"] = "Заполните все поля для создания аккаунта.";
                return RedirectToAction(nameof(Users));
            }

            if (password != confirmPassword)
            {
                TempData["UsersError"] = "Пароли не совпадают.";
                return RedirectToAction(nameof(Users));
            }

            var user = new User
            {
                UserName = username.Trim(),
                FullName = fullName.Trim(),
                Post = post.Trim(),
                ApprovalStatus = UserApprovalStatus.Approved
            };

            var createResult = await _userManager.CreateAsync(user, password);
            if (!createResult.Succeeded)
            {
                TempData["UsersError"] = string.Join(" ", createResult.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(Users));
            }

            var roleResult = await _userManager.AddToRoleAsync(user, GetRoleName(roleId));
            if (!roleResult.Succeeded)
            {
                await _userManager.DeleteAsync(user);
                TempData["UsersError"] = string.Join(" ", roleResult.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(Users));
            }

            TempData["UsersSuccess"] = $"Аккаунт «{user.UserName}» создан.";
            _auditService.Record(GetCurrentUserId(), "User", user.Id, "Created", $"Создан пользователь {user.UserName}.",
                newValues: new
                {
                    user.UserName,
                    user.FullName,
                    user.Post,
                    user.ApprovalStatus,
                    Role = GetRoleName(roleId)
                });
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Users));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        [AppRateLimit(AppRateLimitPolicies.AdminPasswordReset)]
        public async Task<IActionResult> ResetUserPassword(int id, string newPassword, string confirmPassword)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null)
                return NotFound();

            if (string.IsNullOrWhiteSpace(newPassword))
            {
                TempData["UsersError"] = "Введите новый пароль.";
                return RedirectToAction(nameof(Users));
            }

            if (newPassword != confirmPassword)
            {
                TempData["UsersError"] = "Пароли не совпадают.";
                return RedirectToAction(nameof(Users));
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
            if (!result.Succeeded)
            {
                TempData["UsersError"] = string.Join(" ", result.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(Users));
            }

            await _userManager.UpdateSecurityStampAsync(user);
            _auditService.Record(GetCurrentUserId(), "User", user.Id, "PasswordReset", "Пароль сброшен администратором.",
                newValues: new { PasswordChanged = true, SecurityStampUpdated = true });
            await _context.SaveChangesAsync();
            TempData["UsersSuccess"] = $"Пароль пользователя «{user.UserName}» сброшен.";
            return RedirectToAction(nameof(Users));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        [AppRateLimit(AppRateLimitPolicies.AdminUserMutation)]
        public async Task<IActionResult> UnlockUser(
            int id,
            string reviewNote,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(reviewNote) || reviewNote.Trim().Length < 5)
            {
                TempData["UsersError"] = "Для разблокировки укажите комментарий длиной не менее 5 символов.";
                return RedirectToAction(nameof(Users));
            }

            if (reviewNote.Length > 500)
            {
                TempData["UsersError"] = "Комментарий к разблокировке не должен превышать 500 символов.";
                return RedirectToAction(nameof(Users));
            }

            var result = await _accountSecurityService.UnlockAsync(
                id,
                GetCurrentUserId(),
                reviewNote,
                cancellationToken);
            if (!result.Succeeded)
            {
                TempData["UsersError"] = result.Error ?? "Не удалось разблокировать учётную запись.";
                return RedirectToAction(nameof(Users));
            }

            TempData["UsersSuccess"] = $"Учётная запись «{result.UserName}» разблокирована.";
            return RedirectToAction(nameof(Users));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        [AppRateLimit(AppRateLimitPolicies.AdminUserMutation)]
        public async Task<IActionResult> EditUser(int id, string fullName, int? roleId, string post)
        {
            var validationError = EntityInputValidator.User(fullName, post);
            if (validationError != null)
            {
                TempData["UsersError"] = validationError;
                return RedirectToAction(nameof(Users));
            }

            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null)
                return NotFound();

            var currentRoles = await _userManager.GetRolesAsync(user);
            var isAdministrator = currentRoles.Contains(AppRoles.Admin, StringComparer.OrdinalIgnoreCase);
            var previousUser = new { user.FullName, user.Post, Roles = currentRoles.ToArray() };

            if (!isAdministrator && (roleId == null || !AppRoles.AssignableIds.Contains(roleId.Value)))
                return BadRequest();

            user.FullName = fullName.Trim();
            user.Post = post.Trim();

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                TempData["UsersError"] = string.Join(" ", updateResult.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(Users));
            }

            if (!isAdministrator)
            {
                var targetRole = GetRoleName(roleId!.Value);
                if (!currentRoles.Contains(targetRole, StringComparer.OrdinalIgnoreCase))
                {
                    var roleResult = await _userManager.AddToRoleAsync(user, targetRole);
                    if (!roleResult.Succeeded)
                    {
                        TempData["UsersError"] = string.Join(" ", roleResult.Errors.Select(e => e.Description));
                        return RedirectToAction(nameof(Users));
                    }
                }

                var rolesToRemove = currentRoles
                    .Where(role => !role.Equals(targetRole, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (rolesToRemove.Count > 0)
                {
                    var removeResult = await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
                    if (!removeResult.Succeeded)
                    {
                        TempData["UsersError"] = string.Join(" ", removeResult.Errors.Select(e => e.Description));
                        return RedirectToAction(nameof(Users));
                    }
                }
            }

            await _userManager.UpdateSecurityStampAsync(user);
            _auditService.Record(GetCurrentUserId(), "User", user.Id, "Edited", "Данные пользователя или его роль изменены.",
                previousUser,
                new
                {
                    user.FullName,
                    user.Post,
                    Roles = isAdministrator ? currentRoles.ToArray() : new[] { GetRoleName(roleId!.Value) }
                });
            await _context.SaveChangesAsync();
            TempData["UsersSuccess"] = $"Данные пользователя «{user.UserName}» обновлены.";
            return RedirectToAction(nameof(Users));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        [AppRateLimit(AppRateLimitPolicies.AdminUserDelete)]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null)
                return NotFound();

            if (user.ApprovalStatus != UserApprovalStatus.Rejected)
            {
                TempData["UsersError"] = "Удалять можно только отклонённые аккаунты.";
                return RedirectToAction(nameof(Users));
            }

            if (GetCurrentUserId() == id)
            {
                TempData["UsersError"] = "Нельзя удалить собственный аккаунт.";
                return RedirectToAction(nameof(Users));
            }

            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                TempData["UsersError"] = string.Join(" ", result.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(Users));
            }

            _auditService.Record(GetCurrentUserId(), "User", id, "Deleted", $"Удалён пользователь {user.UserName}.",
                previousValues: new { user.UserName, user.FullName, user.Post, user.ApprovalStatus });
            await _context.SaveChangesAsync();
            TempData["UsersSuccess"] = $"Аккаунт «{user.UserName}» удалён.";
            return RedirectToAction(nameof(Users));
        }

        [AppRateLimit(AppRateLimitPolicies.Search)]
        public async Task<IActionResult> Programs(
            bool showArchived = false, int page = 1, string sort = "code", string direction = "asc",
            [FromQuery] ProgramListFiltersViewModel? filters = null)
        {
            filters ??= new ProgramListFiltersViewModel();
            const int pageSize = 25;
            page = Math.Max(1, page);
            var query = _context.EducationalPrograms
                .Where(p => p.IsArchived == showArchived)
                .Include(p => p.User)
                .Include(p => p.Managers).ThenInclude(m => m.User)
                .Include(p => p.Assignments).ThenInclude(a => a.Department)
                .Include(p => p.Assignments).ThenInclude(a => a.Faculty)
                .AsSplitQuery()
                .AsNoTracking();

            var allPrograms = await query.ToListAsync();
            IEnumerable<EducationalProgram> programsQuery = allPrograms.Where(program =>
                ListFilterMatcher.Text(program.CodeReferral, filters.Code) &&
                ListFilterMatcher.Text(program.Name, filters.Name) &&
                ListFilterMatcher.Exact(program.EducationalLevel, filters.Level) &&
                (!filters.Year.HasValue || program.YearApprovals == filters.Year.Value) &&
                ListFilterMatcher.AnyText(program.Assignments.Select(a => a.Department?.Name), filters.Department) &&
                ListFilterMatcher.AnyText(program.Assignments.Select(a => a.Faculty?.Name), filters.Faculty) &&
                ListFilterMatcher.Exact(program.Status, filters.Status) &&
                ListFilterMatcher.Text(program.User?.FullName, filters.Manager));

            var descending = direction.Equals("desc", StringComparison.OrdinalIgnoreCase);
            programsQuery = sort switch
            {
                "name" => descending ? programsQuery.OrderByDescending(p => p.Name) : programsQuery.OrderBy(p => p.Name),
                "level" => descending ? programsQuery.OrderByDescending(p => p.EducationalLevel) : programsQuery.OrderBy(p => p.EducationalLevel),
                "year" => descending ? programsQuery.OrderByDescending(p => p.YearApprovals) : programsQuery.OrderBy(p => p.YearApprovals),
                "department" => descending
                    ? programsQuery.OrderByDescending(p => p.Assignments.Select(a => a.Department?.Name).OrderBy(name => name).FirstOrDefault())
                    : programsQuery.OrderBy(p => p.Assignments.Select(a => a.Department?.Name).OrderBy(name => name).FirstOrDefault()),
                "faculty" => descending
                    ? programsQuery.OrderByDescending(p => p.Assignments.Select(a => a.Faculty?.Name).OrderBy(name => name).FirstOrDefault())
                    : programsQuery.OrderBy(p => p.Assignments.Select(a => a.Faculty?.Name).OrderBy(name => name).FirstOrDefault()),
                "status" => descending ? programsQuery.OrderByDescending(p => p.Status) : programsQuery.OrderBy(p => p.Status),
                "manager" => descending ? programsQuery.OrderByDescending(p => p.User?.FullName) : programsQuery.OrderBy(p => p.User?.FullName),
                _ => descending ? programsQuery.OrderByDescending(p => p.CodeReferral) : programsQuery.OrderBy(p => p.CodeReferral)
            };

            var totalCount = programsQuery.Count();
            var programs = programsQuery.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            SetPagination(totalCount, page, pageSize, sort, direction);
            ViewBag.ShowArchived = showArchived;
            ViewBag.Filters = filters;

            ViewBag.Departments = await _context.Departments.ToListAsync();
            ViewBag.Facultys = await _context.Facultys.ToListAsync();
            ViewBag.Managers = await GetApprovedUsersInRole(AppRoles.Manager);
            ViewBag.ProgramLevels = await _context.EducationalPrograms
                .Select(program => program.EducationalLevel)
                .Distinct()
                .OrderBy(level => level)
                .ToListAsync();

            return View(programs);
        }

        [Authorize(Roles = "Admin")]
        [AppRateLimit(AppRateLimitPolicies.Search)]
        public async Task<IActionResult> ProgramDetails(
            int id, bool showArchivedElements = false, int page = 1,
            string sort = "name", string direction = "asc",
            [FromQuery] ElementListFiltersViewModel? filters = null)
        {
            filters ??= new ElementListFiltersViewModel();
            var program = await _context.EducationalPrograms
                .Include(p => p.User)
                .Include(p => p.Assignments).ThenInclude(a => a.Department)
                .Include(p => p.Assignments).ThenInclude(a => a.Faculty)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (program == null)
                return NotFound();

            const int pageSize = 25;
            page = Math.Max(page, 1);
            var elementsQuery = _context.EducationalProgramElements
                .Where(e => e.EducationalProgramId == id && e.IsArchived == showArchivedElements)
                .Include(e => e.Files.Where(f => f.IsCurrent))
                .AsQueryable();
            var descending = direction.Equals("desc", StringComparison.OrdinalIgnoreCase);
            elementsQuery = sort switch
            {
                "type" => descending ? elementsQuery.OrderByDescending(e => e.TypeElement).ThenByDescending(e => e.Name) : elementsQuery.OrderBy(e => e.TypeElement).ThenBy(e => e.Name),
                "description" => descending ? elementsQuery.OrderByDescending(e => e.Description) : elementsQuery.OrderBy(e => e.Description),
                "status" => descending ? elementsQuery.OrderByDescending(e => e.StatusApprovals) : elementsQuery.OrderBy(e => e.StatusApprovals),
                "date" => descending ? elementsQuery.OrderByDescending(e => e.UploadDate) : elementsQuery.OrderBy(e => e.UploadDate),
                _ => descending ? elementsQuery.OrderByDescending(e => e.Name) : elementsQuery.OrderBy(e => e.Name)
            };
            var filteredElements = await _elementFilterService.FilterAndPageAsync(
                elementsQuery, filters.Tab, page, pageSize);
            var totalCount = filteredElements.TotalCount;
            program.Elements = filteredElements.Items;
            SetPagination(totalCount, page, pageSize, sort, direction);

            ViewBag.ShowArchivedElements = showArchivedElements;
            ViewBag.ElementFilters = filters;
            ViewBag.Departments = await _context.Departments.OrderBy(d => d.Name).ToListAsync();
            ViewBag.Faculties = await _context.Facultys.OrderBy(f => f.Name).ToListAsync();

            return View(program);
        }

        [Authorize(Roles = "Admin")]
        [AppRateLimit(AppRateLimitPolicies.Search)]
        public async Task<IActionResult> Assignments(
            int page = 1, string sort = "date", string direction = "desc",
            [FromQuery] AssignmentListFiltersViewModel? filters = null)
        {
            filters ??= new AssignmentListFiltersViewModel();
            var approverAssignments = await _context.ApproverAssignments
                .Include(a => a.ApproverUser)
                .Include(a => a.AssignedByUser)
                .Include(a => a.Faculty)
                .Include(a => a.Department)
                .ToListAsync();

            var managerAssignments = await _context.EducationalProgramManagers
                .Include(m => m.User)
                .Include(m => m.AssignedByUser)
                .Include(m => m.EducationalProgram)
                .ToListAsync();

            var assignmentsQuery = approverAssignments
                .Select(a => new AssignmentListItemViewModel
                {
                    AssignedAt = a.AssignedAt,
                    UserFullName = a.ApproverUser?.FullName ?? "—",
                    AssignmentType = "Согласующий",
                    TargetName = a.Faculty?.Name ?? a.Department?.Name ?? "—",
                    AssignedByFullName = a.AssignedByUser?.FullName ?? "—"
                })
                .Concat(managerAssignments.Select(m => new AssignmentListItemViewModel
                {
                    AssignedAt = m.AssignedAt,
                    UserFullName = m.User?.FullName ?? "—",
                    AssignmentType = "Руководитель ОПОП",
                    TargetName = m.EducationalProgram == null
                        ? "—"
                        : $"{m.EducationalProgram.CodeReferral} {m.EducationalProgram.Name}",
                    AssignedByFullName = m.AssignedByUser?.FullName ?? "—"
                }))
                .AsEnumerable();

            assignmentsQuery = assignmentsQuery.Where(assignment =>
                ListFilterMatcher.Date(assignment.AssignedAt, filters.DateFrom, filters.DateTo) &&
                ListFilterMatcher.Text(assignment.UserFullName, filters.User) &&
                ListFilterMatcher.Exact(assignment.AssignmentType, filters.AssignmentType) &&
                ListFilterMatcher.Text(assignment.TargetName, filters.Target) &&
                ListFilterMatcher.Text(assignment.AssignedByFullName, filters.Author));

            var descending = direction.Equals("desc", StringComparison.OrdinalIgnoreCase);
            assignmentsQuery = sort switch
            {
                "user" => descending ? assignmentsQuery.OrderByDescending(a => a.UserFullName) : assignmentsQuery.OrderBy(a => a.UserFullName),
                "type" => descending ? assignmentsQuery.OrderByDescending(a => a.AssignmentType) : assignmentsQuery.OrderBy(a => a.AssignmentType),
                "target" => descending ? assignmentsQuery.OrderByDescending(a => a.TargetName) : assignmentsQuery.OrderBy(a => a.TargetName),
                "author" => descending ? assignmentsQuery.OrderByDescending(a => a.AssignedByFullName) : assignmentsQuery.OrderBy(a => a.AssignedByFullName),
                _ => descending ? assignmentsQuery.OrderByDescending(a => a.AssignedAt) : assignmentsQuery.OrderBy(a => a.AssignedAt)
            };
            const int pageSize = 25;
            page = Math.Max(page, 1);
            var totalCount = assignmentsQuery.Count();
            var assignments = assignmentsQuery.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            SetPagination(totalCount, page, pageSize, sort, direction);
            ViewBag.Filters = filters;
            ViewBag.AssignmentTypes = approverAssignments.Count > 0 || managerAssignments.Count > 0
                ? approverAssignments.Select(_ => "Согласующий")
                    .Concat(managerAssignments.Select(_ => "Руководитель ОПОП"))
                    .Distinct()
                    .ToList()
                : new List<string> { "Согласующий", "Руководитель ОПОП" };

            return View(assignments);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [AppRateLimit(AppRateLimitPolicies.AdminStructureMutation)]
        public async Task<IActionResult> CreateProgram(string codeReferral, string name, string educationalLevel,
            int yearApprovals, int departmentId, int facultyId, int? managerUserId)
        {
            var validationError = EntityInputValidator.Program(codeReferral, name, educationalLevel, yearApprovals);
            if (validationError != null)
            {
                TempData["ErrorMessage"] = validationError;
                return RedirectToAction(nameof(Programs));
            }
            codeReferral = codeReferral.Trim();
            name = name.Trim();
            educationalLevel = educationalLevel.Trim();
            if (!await _context.Departments.AnyAsync(d => d.Id == departmentId) ||
                !await _context.Facultys.AnyAsync(f => f.Id == facultyId))
                return BadRequest("Выбранная кафедра или факультет не найдены.");
            if (await _context.EducationalPrograms.AnyAsync(p =>
                    p.CodeReferral == codeReferral && p.Name == name &&
                    p.EducationalLevel == educationalLevel && p.YearApprovals == yearApprovals))
            {
                TempData["ErrorMessage"] = "Такая ОПОП уже существует.";
                return RedirectToAction(nameof(Programs));
            }

            if (managerUserId.HasValue)
            {
                var manager = await _userManager.FindByIdAsync(managerUserId.Value.ToString());
                if (manager == null ||
                    manager.ApprovalStatus != UserApprovalStatus.Approved ||
                    !await _userManager.IsInRoleAsync(manager, AppRoles.Manager))
                    return NotFound();
            }

            var program = new EducationalProgram
            {
                CodeReferral = codeReferral,
                Name = name,
                EducationalLevel = educationalLevel,
                YearApprovals = yearApprovals,
                Status = EducationalProgramStatus.Draft,
                UserId = managerUserId
            };

            await using var transaction = _context.Database.IsRelational()
                ? await _context.Database.BeginTransactionAsync()
                : null;
            try
            {
                _context.EducationalPrograms.Add(program);
                await _context.SaveChangesAsync();
                _context.EducationalProgramAssignments.Add(new EducationalProgramAssignment
                {
                    EducationalProgramId = program.Id,
                    DepartmentId = departmentId,
                    FacultyId = facultyId
                });

                if (managerUserId.HasValue)
                {
                    _context.EducationalProgramManagers.Add(new EducationalProgramManager
                    {
                        EducationalProgramId = program.Id,
                        UserId = managerUserId.Value,
                        AssignedByUserId = GetCurrentUserId(),
                        AssignedAt = DateTime.UtcNow
                    });
                }

                _auditService.Record(GetCurrentUserId(), "EducationalProgram", program.Id, "Created",
                    $"Создана ОПОП {program.CodeReferral} «{program.Name}».",
                    newValues: new
                    {
                        program.CodeReferral,
                        program.Name,
                        program.EducationalLevel,
                        program.YearApprovals,
                        program.Status,
                        ManagerUserId = managerUserId,
                        DepartmentId = departmentId,
                        FacultyId = facultyId
                    });
                await _context.SaveChangesAsync();
                if (transaction != null)
                    await transaction.CommitAsync();
            }
            catch
            {
                if (transaction != null)
                    await transaction.RollbackAsync();
                throw;
            }
            return RedirectToAction(nameof(Programs));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [AppRateLimit(AppRateLimitPolicies.AdminStructureMutation)]
        public async Task<IActionResult> EditProgram(
            int programId, int version, string codeReferral, string name,
            string educationalLevel, int yearApprovals)
        {
            var program = await _context.EducationalPrograms.FindAsync(programId);
            if (program == null)
                return NotFound();
            if (program.IsArchived)
                return ArchivedProgram(programId);
            if (program.Version != version)
                return ProgramConflict(programId);
            var validationError = EntityInputValidator.Program(codeReferral, name, educationalLevel, yearApprovals);
            if (validationError != null)
                return BadRequest(validationError);
            var normalizedCode = codeReferral.Trim();
            var normalizedName = name.Trim();
            var normalizedLevel = educationalLevel.Trim();
            if (await _context.EducationalPrograms.AnyAsync(p => p.Id != programId &&
                    p.CodeReferral == normalizedCode && p.Name == normalizedName &&
                    p.EducationalLevel == normalizedLevel && p.YearApprovals == yearApprovals))
                return BadRequest("Такая ОПОП уже существует.");

            var previousProgram = new
            {
                program.CodeReferral,
                program.Name,
                program.EducationalLevel,
                program.YearApprovals,
                program.Version
            };
            program.CodeReferral = normalizedCode;
            program.Name = normalizedName;
            program.EducationalLevel = normalizedLevel;
            program.YearApprovals = yearApprovals;
            program.Version++;
            _auditService.Record(GetCurrentUserId(), "EducationalProgram", program.Id, "Edited", "Изменена карточка ОПОП.",
                previousProgram,
                new { program.CodeReferral, program.Name, program.EducationalLevel, program.YearApprovals, program.Version });

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return ProgramConflict(programId);
            }

            TempData["SuccessMessage"] = "Карточка ОПОП сохранена.";
            return RedirectToAction(nameof(ProgramDetails), new { id = programId });
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [AppRateLimit(AppRateLimitPolicies.AdminStructureMutation)]
        public async Task<IActionResult> UpdateProgramAssignments(
            int programId, int version, List<int> departmentIds, List<int> facultyIds)
        {
            var program = await _context.EducationalPrograms
                .Include(p => p.Assignments)
                .FirstOrDefaultAsync(p => p.Id == programId);
            if (program == null)
                return NotFound();
            if (program.IsArchived)
                return ArchivedProgram(programId);
            if (program.Version != version)
                return ProgramConflict(programId);
            if (departmentIds.Count == 0 || departmentIds.Count != facultyIds.Count)
            {
                TempData["ErrorMessage"] = "Добавьте хотя бы одну полную пару «кафедра — факультет».";
                return RedirectToAction(nameof(ProgramDetails), new { id = programId });
            }

            var pairs = departmentIds.Zip(facultyIds).Distinct().ToList();
            var validDepartments = await _context.Departments.CountAsync(d => departmentIds.Contains(d.Id));
            var validFaculties = await _context.Facultys.CountAsync(f => facultyIds.Contains(f.Id));
            if (validDepartments != departmentIds.Distinct().Count() || validFaculties != facultyIds.Distinct().Count())
                return BadRequest("Одна из кафедр или факультетов не найдена.");

            var previousAssignments = program.Assignments
                .Select(assignment => new { assignment.DepartmentId, assignment.FacultyId })
                .ToArray();
            _context.EducationalProgramAssignments.RemoveRange(program.Assignments);
            foreach (var pair in pairs)
            {
                _context.EducationalProgramAssignments.Add(new EducationalProgramAssignment
                {
                    EducationalProgramId = programId,
                    DepartmentId = pair.First,
                    FacultyId = pair.Second
                });
            }

            program.Version++;
            _auditService.Record(GetCurrentUserId(), "EducationalProgram", program.Id, "AssignmentsChanged",
                $"Установлено привязок: {pairs.Count}.",
                previousAssignments,
                pairs.Select(pair => new { DepartmentId = pair.First, FacultyId = pair.Second }).ToArray());
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return ProgramConflict(programId);
            }

            TempData["SuccessMessage"] = "Привязки ОПОП сохранены.";
            return RedirectToAction(nameof(ProgramDetails), new { id = programId });
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [AppRateLimit(AppRateLimitPolicies.AdminStructureMutation)]
        public async Task<IActionResult> SetProgramArchived(int programId, int version, bool archived)
        {
            var program = await _context.EducationalPrograms.FindAsync(programId);
            if (program == null)
                return NotFound();
            if (program.Version != version)
                return ProgramConflict(programId);

            program.IsArchived = archived;
            program.ArchivedAt = archived ? DateTime.UtcNow : null;
            program.ArchivedByUserId = archived ? GetCurrentUserId() : null;
            program.Version++;
            _auditService.Record(GetCurrentUserId(), "EducationalProgram", program.Id,
                archived ? "Archived" : "Restored", archived ? "ОПОП перенесена в архив." : "ОПОП восстановлена из архива.");
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return ProgramConflict(programId);
            }

            return RedirectToAction(nameof(Programs), new { showArchived = archived });
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [AppRateLimit(AppRateLimitPolicies.AdminStructureMutation)]
        public async Task<IActionResult> AssignProgramManager(int programId, int version, int? managerUserId)
        {
            var program = await _context.EducationalPrograms
                .Include(p => p.Managers)
                .FirstOrDefaultAsync(p => p.Id == programId);

            if (program == null)
                return NotFound();
            if (program.IsArchived)
                return ArchivedProgram(programId);
            if (program.Version != version)
                return ProgramConflict(programId);

            User? manager = null;
            if (managerUserId.HasValue)
            {
                manager = await _userManager.FindByIdAsync(managerUserId.Value.ToString());
                if (manager == null ||
                    manager.ApprovalStatus != UserApprovalStatus.Approved ||
                    !await _userManager.IsInRoleAsync(manager, AppRoles.Manager))
                    return NotFound();
            }

            var previousManagerUserId = program.UserId;
            program.UserId = managerUserId;
            program.Version++;

            var currentAssignments = await _context.EducationalProgramManagers
                .Where(m => m.EducationalProgramId == programId)
                .ToListAsync();

            _context.EducationalProgramManagers.RemoveRange(currentAssignments);
            if (managerUserId.HasValue)
            {
                _context.EducationalProgramManagers.Add(new EducationalProgramManager
                {
                    EducationalProgramId = programId,
                    UserId = managerUserId.Value,
                    AssignedByUserId = GetCurrentUserId(),
                    AssignedAt = DateTime.UtcNow
                });
            }

            _auditService.Record(GetCurrentUserId(), "EducationalProgram", program.Id, "ManagerAssigned",
                managerUserId.HasValue ? $"Назначен руководитель, ID {managerUserId.Value}." : "Руководитель снят.",
                new { ManagerUserId = previousManagerUserId },
                new { ManagerUserId = managerUserId });

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return ProgramConflict(programId);
            }
            return RedirectToAction(nameof(Programs));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [AppRateLimit(AppRateLimitPolicies.AdminStructureMutation)]
        public async Task<IActionResult> CreateProgramElement(int programId, string typeElement, string name, string description)
        {
            if (!await _context.EducationalPrograms.AnyAsync(p => p.Id == programId && !p.IsArchived))
                return NotFound();

            if (!EducationalProgramElementTypes.All.Contains(typeElement))
                return BadRequest("Неизвестный тип элемента ОПОП");

            var validationError = EntityInputValidator.Element(name, description);
            if (validationError != null)
                return BadRequest(validationError);
            name = name.Trim();
            description = description?.Trim() ?? string.Empty;
            if (await _context.EducationalProgramElements.AnyAsync(e =>
                    e.EducationalProgramId == programId && !e.IsArchived &&
                    e.TypeElement == typeElement && e.Name == name))
                return BadRequest("Элемент с таким типом и наименованием уже существует в ОПОП.");

            var element = new EducationalProgramElement
            {
                EducationalProgramId = programId,
                TypeElement = typeElement,
                Name = name,
                Description = description,
                StatusApprovals = ElementApprovalStatus.NotUploaded
            };
            _context.EducationalProgramElements.Add(element);

            await _context.SaveChangesAsync();
            _context.ElementStatusHistory.Add(new ElementStatusHistory
            {
                EducationalProgramElementId = element.Id,
                UserId = GetCurrentUserId(),
                OldStatus = string.Empty,
                NewStatus = ElementApprovalStatus.NotUploaded,
                ChangeDate = DateTime.UtcNow,
                Comment = "Элемент ОПОП создан."
            });
            _auditService.Record(GetCurrentUserId(), "EducationalProgramElement", element.Id, "Created",
                $"Создан элемент «{element.Name}».",
                newValues: new { element.EducationalProgramId, element.TypeElement, element.Name, element.Description, element.StatusApprovals });
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(ProgramDetails), new { id = programId });
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [AppRateLimit(AppRateLimitPolicies.AdminStructureMutation)]
        public async Task<IActionResult> EditProgramElement(int elementId, int version, string typeElement, string name, string? description)
        {
            var element = await _context.EducationalProgramElements.FindAsync(elementId);
            if (element == null)
                return NotFound();

            if (element.Version != version)
                return ElementConflict(element.EducationalProgramId);

            var validationError = EntityInputValidator.Element(name, description);
            if (!EducationalProgramElementTypes.All.Contains(typeElement) || validationError != null)
                return BadRequest(validationError ?? "Проверьте тип элемента ОПОП.");
            var normalizedName = name.Trim();
            if (await _context.EducationalProgramElements.AnyAsync(e => e.Id != elementId &&
                    e.EducationalProgramId == element.EducationalProgramId && !e.IsArchived &&
                    e.TypeElement == typeElement && e.Name == normalizedName))
                return BadRequest("Элемент с таким типом и наименованием уже существует в ОПОП.");

            var oldDescription = $"{element.TypeElement}: {element.Name} ({element.Description})";
            var previousElement = new { element.TypeElement, element.Name, element.Description, element.Version };
            element.TypeElement = typeElement;
            element.Name = normalizedName;
            element.Description = description?.Trim() ?? string.Empty;
            element.Version++;
            _context.ElementStatusHistory.Add(new ElementStatusHistory
            {
                EducationalProgramElementId = element.Id,
                UserId = GetCurrentUserId(),
                OldStatus = element.StatusApprovals,
                NewStatus = element.StatusApprovals,
                ChangeDate = DateTime.UtcNow,
                Comment = $"Изменена карточка элемента. Было: {oldDescription}"
            });
            _auditService.Record(GetCurrentUserId(), "EducationalProgramElement", element.Id, "Edited",
                $"Изменена карточка. Было: {oldDescription}",
                previousElement,
                new { element.TypeElement, element.Name, element.Description, element.Version });
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return ElementConflict(element.EducationalProgramId);
            }
            return RedirectToAction(nameof(ProgramDetails), new { id = element.EducationalProgramId });
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [AppRateLimit(AppRateLimitPolicies.AdminStructureMutation)]
        public async Task<IActionResult> SetElementArchived(int elementId, int version, bool archived)
        {
            var element = await _context.EducationalProgramElements.FindAsync(elementId);
            if (element == null)
                return NotFound();
            if (element.Version != version)
                return ElementConflict(element.EducationalProgramId);

            var previousArchiveState = new { element.IsArchived, element.ArchivedAt, element.ArchivedByUserId, element.Version };
            element.IsArchived = archived;
            element.ArchivedAt = archived ? DateTime.UtcNow : null;
            element.ArchivedByUserId = archived ? GetCurrentUserId() : null;
            element.Version++;
            _auditService.Record(GetCurrentUserId(), "EducationalProgramElement", element.Id,
                archived ? "Archived" : "Restored", archived ? "Элемент перенесён в архив." : "Элемент восстановлен из архива.",
                previousArchiveState,
                new { element.IsArchived, element.ArchivedAt, element.ArchivedByUserId, element.Version });
            _context.ElementStatusHistory.Add(new ElementStatusHistory
            {
                EducationalProgramElementId = element.Id,
                UserId = GetCurrentUserId(),
                OldStatus = element.StatusApprovals,
                NewStatus = element.StatusApprovals,
                ChangeDate = DateTime.UtcNow,
                Comment = archived ? "Элемент перенесён в архив." : "Элемент восстановлен из архива."
            });
            await _workflowService.RecalculateProgramStatusAsync(element.EducationalProgramId);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return ElementConflict(element.EducationalProgramId);
            }

            return RedirectToAction(nameof(ProgramDetails), new { id = element.EducationalProgramId, showArchivedElements = archived });
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [AppRateLimit(AppRateLimitPolicies.FileUpload)]
        public async Task<IActionResult> UploadElement(int elementId, List<IFormFile> files)
        {
            var element = await _context.EducationalProgramElements.FindAsync(elementId);
            if (element == null)
                return NotFound();

            files = files.Where(f => f.Length > 0).ToList();
            if (files.Count > 0)
            {
                if (files.Count > FileUploadLimits.MaxFilesPerGroup)
                {
                    TempData["ErrorMessage"] = $"За один раз можно загрузить не более {FileUploadLimits.MaxFilesPerGroup} файлов.";
                    return RedirectToAction(nameof(ProgramDetails), new { id = element.EducationalProgramId });
                }

                var totalUploadSize = files.Sum(file => file.Length);
                if (totalUploadSize > FileUploadLimits.MaxGroupSizeBytes)
                {
                    await _accountSecurityService.RecordInvalidUploadAsync(
                        "группа файлов",
                        totalUploadSize,
                        $"Общий размер превышает {FileUploadLimits.MaxGroupSizeDisplay}.",
                        countsTowardsBlock: false);
                    TempData["ErrorMessage"] = $"Общий размер группы файлов не должен превышать {FileUploadLimits.MaxGroupSizeDisplay}.";
                    return RedirectToAction(nameof(ProgramDetails), new { id = element.EducationalProgramId });
                }

                try
                {
                    foreach (var file in files)
                        await _fileStorageService.ValidateFileAsync(file);

                    _accountSecurityService.RecordDocumentUpload(files);
                    await _accountSecurityService.ResetInvalidUploadSequenceAsync();

                    var storedFiles = new List<(string StoredFileName, string OriginalFileName)>();
                    try
                    {
                        foreach (var file in files)
                            storedFiles.Add((await _fileStorageService.SaveFileAsync(file), Path.GetFileName(file.FileName)));

                        var updatedElement = await _workflowService.MarkFilesUploadedAsync(
                            elementId, GetCurrentUserId(), storedFiles, adminOverride: true);
                        if (updatedElement == null)
                            throw new InvalidOperationException("Элемент не найден или находится в архиве.");
                    }
                    catch
                    {
                        foreach (var storedFile in storedFiles)
                            await _fileStorageService.DeleteFileAsync(storedFile.StoredFileName);
                        throw;
                    }
                }
                catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or IOException or DbUpdateException)
                {
                    TempData["ErrorMessage"] = ex is DbUpdateConcurrencyException
                        ? "Данные были изменены другим пользователем. Обновите страницу и повторите действие."
                        : ex.Message;
                }
            }

            return RedirectToAction(nameof(ProgramDetails), new { id = element.EducationalProgramId });
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [AppRateLimit(AppRateLimitPolicies.WorkflowMutation)]
        public async Task<IActionResult> ApproveElement(int elementId, string? comment)
        {
            return await ChangeElementStatus(elementId, ElementApprovalStatus.Approved, comment ?? "Согласовано администратором");
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [AppRateLimit(AppRateLimitPolicies.WorkflowMutation)]
        public async Task<IActionResult> SendElementToRevision(int elementId, string? comment)
        {
            return await ChangeElementStatus(elementId, ElementApprovalStatus.RevisionRequired, comment ?? "Отправлено на доработку администратором");
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [AppRateLimit(AppRateLimitPolicies.WorkflowMutation)]
        public async Task<IActionResult> PublishElement(int elementId, string? comment)
        {
            var element = await _context.EducationalProgramElements.FindAsync(elementId);
            if (element == null)
                return NotFound();

            if (ElementApprovalStatus.Normalize(element.StatusApprovals) != ElementApprovalStatus.Approved)
                return BadRequest("Опубликовать можно только согласованный элемент");

            return await ChangeElementStatus(elementId, ElementApprovalStatus.Published, comment ?? "Опубликовано администратором");
        }

        [Authorize(Roles = "Admin")]
        [AppRateLimit(AppRateLimitPolicies.FileDownload)]
        public async Task<IActionResult> DownloadElement(int elementId)
        {
            await _notificationService.MarkElementReadAsync(GetCurrentUserId(), elementId);
            var element = await _context.EducationalProgramElements.FindAsync(elementId);
            if (element == null || string.IsNullOrEmpty(element.FilePath))
                return NotFound();

            var filePath = StoredFilePath.Resolve(_storageSettings.StoragePath, element.FilePath);
            if (filePath == null)
                return NotFound();

            return PhysicalFile(filePath, GetContentType(element.FileName), element.FileName ?? "download");
        }

        [Authorize(Roles = "Admin")]
        [AppRateLimit(AppRateLimitPolicies.FileDownload)]
        public async Task<IActionResult> PreviewElement(int elementId)
        {
            await _notificationService.MarkElementReadAsync(GetCurrentUserId(), elementId);
            var element = await _context.EducationalProgramElements.FindAsync(elementId);
            if (element == null || string.IsNullOrEmpty(element.FilePath))
                return NotFound();

            var filePath = StoredFilePath.Resolve(_storageSettings.StoragePath, element.FilePath);
            if (filePath == null)
                return NotFound();

            Response.Headers.Append("Content-Disposition", $"inline; filename=\"{element.FileName ?? "preview"}\"");
            return PhysicalFile(filePath, GetContentType(element.FileName));
        }

        private async Task<IActionResult> ChangeElementStatus(int elementId, string newStatus, string comment)
        {
            EducationalProgramElement? element;
            try
            {
                element = await _workflowService.ChangeStatusAsync(elementId, GetCurrentUserId(), newStatus, comment, adminOverride: true);
            }
            catch (DbUpdateConcurrencyException)
            {
                var programId = await _context.EducationalProgramElements
                    .Where(e => e.Id == elementId).Select(e => e.EducationalProgramId).FirstOrDefaultAsync();
                return ElementConflict(programId);
            }
            if (element == null)
                return NotFound();

            return RedirectToAction(nameof(ProgramDetails), new { id = element.EducationalProgramId });
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [AppRateLimit(AppRateLimitPolicies.AdminStructureMutation)]
        public async Task<IActionResult> AssignApproverToFaculty(int? approverUserId, int facultyId)
        {
            return await CreateApproverAssignment(approverUserId, facultyId, null, nameof(Faculties));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [AppRateLimit(AppRateLimitPolicies.AdminStructureMutation)]
        public async Task<IActionResult> AssignApproverToDepartment(int? approverUserId, int departmentId)
        {
            return await CreateApproverAssignment(approverUserId, null, departmentId, nameof(Departments));
        }

        private async Task<IActionResult> CreateApproverAssignment(int? approverUserId, int? facultyId, int? departmentId, string redirectAction)
        {
            if (facultyId == null && departmentId == null)
                return BadRequest();

            if (facultyId != null && !await _context.Facultys.AnyAsync(f => f.Id == facultyId))
                return NotFound();

            if (departmentId != null && !await _context.Departments.AnyAsync(d => d.Id == departmentId))
                return NotFound();

            var currentAssignments = await _context.ApproverAssignments
                .Where(a => a.FacultyId == facultyId && a.DepartmentId == departmentId)
                .ToListAsync();
            var previousApproverIds = currentAssignments.Select(assignment => assignment.ApproverUserId).ToArray();
            _context.ApproverAssignments.RemoveRange(currentAssignments);

            if (approverUserId.HasValue)
            {
                var approver = await _userManager.FindByIdAsync(approverUserId.Value.ToString());
                if (approver == null ||
                    approver.ApprovalStatus != UserApprovalStatus.Approved ||
                    !await _userManager.IsInRoleAsync(approver, AppRoles.Approver))
                    return NotFound();

                _context.ApproverAssignments.Add(new ApproverAssignment
                {
                    ApproverUserId = approverUserId.Value,
                    FacultyId = facultyId,
                    DepartmentId = departmentId,
                    AssignedByUserId = GetCurrentUserId(),
                    AssignedAt = DateTime.UtcNow
                });
            }

            _auditService.Record(GetCurrentUserId(), facultyId.HasValue ? "Faculty" : "Department",
                facultyId ?? departmentId!.Value, "ApproverAssigned",
                approverUserId.HasValue ? $"Назначен согласующий, ID {approverUserId.Value}." : "Согласующий снят.",
                new { ApproverUserIds = previousApproverIds },
                new { ApproverUserId = approverUserId });
            await _context.SaveChangesAsync();
            return RedirectToAction(redirectAction);
        }

        [AppRateLimit(AppRateLimitPolicies.Search)]
        public async Task<IActionResult> Departments(
            int page = 1, string sort = "name", string direction = "asc",
            [FromQuery] DepartmentListFiltersViewModel? filters = null)
        {
            filters ??= new DepartmentListFiltersViewModel();
            const int pageSize = 25;
            page = Math.Max(page, 1);
            var allDepartments = await _context.Departments.AsNoTracking().ToListAsync();
            IEnumerable<Departments> query = allDepartments.Where(department =>
                (!filters.Id.HasValue || department.Id == filters.Id.Value) &&
                ListFilterMatcher.Text(department.CodeDepartment, filters.Code) &&
                ListFilterMatcher.Text(department.Name, filters.Name));
            var descending = direction.Equals("desc", StringComparison.OrdinalIgnoreCase);
            query = sort switch
            {
                "id" => descending ? query.OrderByDescending(d => d.Id) : query.OrderBy(d => d.Id),
                "code" => descending ? query.OrderByDescending(d => d.CodeDepartment) : query.OrderBy(d => d.CodeDepartment),
                _ => descending ? query.OrderByDescending(d => d.Name) : query.OrderBy(d => d.Name)
            };
            var totalCount = query.Count();
            var departments = query.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            SetPagination(totalCount, page, pageSize, sort, direction);
            ViewBag.Filters = filters;
            var currentAssignments = await _context.ApproverAssignments
                .Where(a => a.DepartmentId.HasValue)
                .OrderByDescending(a => a.AssignedAt)
                .ToListAsync();

            ViewBag.Approvers = await GetApprovedApprovers();
            ViewBag.CurrentApprovers = currentAssignments
                .GroupBy(a => a.DepartmentId!.Value)
                .ToDictionary(g => g.Key, g => g.First().ApproverUserId);
            return View(departments);
        }

        [AppRateLimit(AppRateLimitPolicies.Search)]
        public async Task<IActionResult> DepartmentDetails(
            int id, [FromQuery] OrganizationDocumentFiltersViewModel? filters = null)
        {
            filters ??= new OrganizationDocumentFiltersViewModel();
            var department = await _context.Departments.FindAsync(id);
            if (department == null)
                return NotFound();

            var programs = await _context.EducationalPrograms
                .Where(p => !p.IsArchived && p.Assignments.Any(a => a.DepartmentId == id))
                .Include(p => p.Assignments).ThenInclude(a => a.Department)
                .Include(p => p.Assignments).ThenInclude(a => a.Faculty)
                .Include(p => p.Elements)
                .AsSplitQuery()
                .OrderBy(p => p.CodeReferral)
                .ToListAsync();

            FilterOrganizationDocuments(programs, filters);

            return View("OrganizationDocuments", new OrganizationDocumentsViewModel
            {
                PageTitle = "Документы кафедры",
                EntityType = "Department",
                EntityId = department.Id,
                EntityName = department.Name,
                Programs = programs,
                Filters = filters
            });
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [AppRateLimit(AppRateLimitPolicies.AdminStructureMutation)]
        public async Task<IActionResult> CreateDepartment(string codeDepartment, string name)
        {
            var validationError = EntityInputValidator.Department(codeDepartment, name);
            if (validationError != null)
            {
                TempData["ErrorMessage"] = validationError;
                return RedirectToAction(nameof(Departments));
            }
            codeDepartment = codeDepartment.Trim();
            name = name.Trim();
            if (await _context.Departments.AnyAsync(d => d.CodeDepartment == codeDepartment || d.Name == name))
            {
                TempData["ErrorMessage"] = "Кафедра с таким кодом или наименованием уже существует.";
                return RedirectToAction(nameof(Departments));
            }
            var department = new Departments { CodeDepartment = codeDepartment, Name = name };
            _context.Departments.Add(department);
            await _context.SaveChangesAsync();
            _auditService.Record(GetCurrentUserId(), "Department", department.Id, "Created", $"Создана кафедра {department.Name}.",
                newValues: new { department.CodeDepartment, department.Name });
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Departments));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [AppRateLimit(AppRateLimitPolicies.AdminStructureMutation)]
        public async Task<IActionResult> EditDepartment(int id, string codeDepartment, string name)
        {
            var department = await _context.Departments.FindAsync(id);
            if (department == null) return NotFound();
            var validationError = EntityInputValidator.Department(codeDepartment, name);
            if (validationError != null) return BadRequest(validationError);
            var normalizedCode = codeDepartment.Trim();
            var normalizedName = name.Trim();
            if (await _context.Departments.AnyAsync(d => d.Id != id &&
                    (d.CodeDepartment == normalizedCode || d.Name == normalizedName)))
                return BadRequest("Кафедра с таким кодом или наименованием уже существует.");
            var previousDepartment = new { department.CodeDepartment, department.Name };
            department.CodeDepartment = normalizedCode;
            department.Name = normalizedName;
            _auditService.Record(GetCurrentUserId(), "Department", department.Id, "Edited", "Данные кафедры изменены.",
                previousDepartment,
                new { department.CodeDepartment, department.Name });
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Departments));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [AppRateLimit(AppRateLimitPolicies.AdminStructureMutation)]
        public async Task<IActionResult> DeleteDepartment(int id)
        {
            var department = await _context.Departments.FindAsync(id);
            if (department == null) return NotFound();
            if (await _context.EducationalProgramAssignments.AnyAsync(a => a.DepartmentId == id) ||
                await _context.ApproverAssignments.AnyAsync(a => a.DepartmentId == id))
            {
                TempData["ErrorMessage"] = "Кафедра используется в привязках ОПОП или согласующих и не может быть удалена.";
                return RedirectToAction(nameof(Departments));
            }
            _auditService.Record(GetCurrentUserId(), "Department", department.Id, "Deleted", $"Удалена кафедра {department.Name}.",
                previousValues: new { department.CodeDepartment, department.Name });
            _context.Departments.Remove(department);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Departments));
        }

        [AppRateLimit(AppRateLimitPolicies.Search)]
        public async Task<IActionResult> Faculties(
            int page = 1, string sort = "name", string direction = "asc",
            [FromQuery] FacultyListFiltersViewModel? filters = null)
        {
            filters ??= new FacultyListFiltersViewModel();
            const int pageSize = 25;
            page = Math.Max(page, 1);
            var allFaculties = await _context.Facultys.AsNoTracking().ToListAsync();
            IEnumerable<Facultys> query = allFaculties.Where(faculty =>
                (!filters.Id.HasValue || faculty.Id == filters.Id.Value) &&
                ListFilterMatcher.Text(faculty.Name, filters.Name));
            var descending = direction.Equals("desc", StringComparison.OrdinalIgnoreCase);
            query = sort == "id"
                ? (descending ? query.OrderByDescending(f => f.Id) : query.OrderBy(f => f.Id))
                : (descending ? query.OrderByDescending(f => f.Name) : query.OrderBy(f => f.Name));
            var totalCount = query.Count();
            var faculties = query.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            SetPagination(totalCount, page, pageSize, sort, direction);
            ViewBag.Filters = filters;
            var currentAssignments = await _context.ApproverAssignments
                .Where(a => a.FacultyId.HasValue)
                .OrderByDescending(a => a.AssignedAt)
                .ToListAsync();

            ViewBag.Approvers = await GetApprovedApprovers();
            ViewBag.CurrentApprovers = currentAssignments
                .GroupBy(a => a.FacultyId!.Value)
                .ToDictionary(g => g.Key, g => g.First().ApproverUserId);
            return View(faculties);
        }

        [AppRateLimit(AppRateLimitPolicies.Search)]
        public async Task<IActionResult> FacultyDetails(
            int id, [FromQuery] OrganizationDocumentFiltersViewModel? filters = null)
        {
            filters ??= new OrganizationDocumentFiltersViewModel();
            var faculty = await _context.Facultys.FindAsync(id);
            if (faculty == null)
                return NotFound();

            var programs = await _context.EducationalPrograms
                .Where(p => !p.IsArchived && p.Assignments.Any(a => a.FacultyId == id))
                .Include(p => p.Assignments).ThenInclude(a => a.Department)
                .Include(p => p.Assignments).ThenInclude(a => a.Faculty)
                .Include(p => p.Elements)
                .AsSplitQuery()
                .OrderBy(p => p.CodeReferral)
                .ToListAsync();

            FilterOrganizationDocuments(programs, filters);

            return View("OrganizationDocuments", new OrganizationDocumentsViewModel
            {
                PageTitle = "Документы факультета",
                EntityType = "Faculty",
                EntityId = faculty.Id,
                EntityName = faculty.Name,
                Programs = programs,
                Filters = filters
            });
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [AppRateLimit(AppRateLimitPolicies.AdminStructureMutation)]
        public async Task<IActionResult> CreateFaculty(string name)
        {
            var validationError = EntityInputValidator.Faculty(name);
            if (validationError != null)
            {
                TempData["ErrorMessage"] = validationError;
                return RedirectToAction(nameof(Faculties));
            }
            name = name.Trim();
            if (await _context.Facultys.AnyAsync(f => f.Name == name))
            {
                TempData["ErrorMessage"] = "Факультет с таким наименованием уже существует.";
                return RedirectToAction(nameof(Faculties));
            }
            var faculty = new Facultys { Name = name };
            _context.Facultys.Add(faculty);
            await _context.SaveChangesAsync();
            _auditService.Record(GetCurrentUserId(), "Faculty", faculty.Id, "Created", $"Создан факультет {faculty.Name}.",
                newValues: new { faculty.Name });
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Faculties));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [AppRateLimit(AppRateLimitPolicies.AdminStructureMutation)]
        public async Task<IActionResult> EditFaculty(int id, string name)
        {
            var faculty = await _context.Facultys.FindAsync(id);
            if (faculty == null) return NotFound();
            var validationError = EntityInputValidator.Faculty(name);
            if (validationError != null) return BadRequest(validationError);
            var normalizedName = name.Trim();
            if (await _context.Facultys.AnyAsync(f => f.Id != id && f.Name == normalizedName))
                return BadRequest("Факультет с таким наименованием уже существует.");
            var previousFaculty = new { faculty.Name };
            faculty.Name = normalizedName;
            _auditService.Record(GetCurrentUserId(), "Faculty", faculty.Id, "Edited", "Данные факультета изменены.",
                previousFaculty,
                new { faculty.Name });
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Faculties));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [AppRateLimit(AppRateLimitPolicies.AdminStructureMutation)]
        public async Task<IActionResult> DeleteFaculty(int id)
        {
            var faculty = await _context.Facultys.FindAsync(id);
            if (faculty == null) return NotFound();
            if (await _context.EducationalProgramAssignments.AnyAsync(a => a.FacultyId == id) ||
                await _context.ApproverAssignments.AnyAsync(a => a.FacultyId == id))
            {
                TempData["ErrorMessage"] = "Факультет используется в привязках ОПОП или согласующих и не может быть удалён.";
                return RedirectToAction(nameof(Faculties));
            }
            _auditService.Record(GetCurrentUserId(), "Faculty", faculty.Id, "Deleted", $"Удалён факультет {faculty.Name}.",
                previousValues: new { faculty.Name });
            _context.Facultys.Remove(faculty);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Faculties));
        }

        private IActionResult ProgramConflict(int programId)
        {
            TempData["ErrorMessage"] = "ОПОП уже изменена другим пользователем. Страница обновлена, повторите действие.";
            return RedirectToAction(nameof(ProgramDetails), new { id = programId });
        }

        private IActionResult ElementConflict(int programId)
        {
            TempData["ErrorMessage"] = "Элемент уже изменён другим пользователем. Страница обновлена, повторите действие.";
            return RedirectToAction(nameof(ProgramDetails), new { id = programId });
        }

        private IActionResult ArchivedProgram(int programId)
        {
            TempData["ErrorMessage"] = "Архивную ОПОП сначала необходимо восстановить.";
            return RedirectToAction(nameof(ProgramDetails), new { id = programId });
        }

        private static void FilterOrganizationDocuments(
            List<EducationalProgram> programs,
            OrganizationDocumentFiltersViewModel filters)
        {
            programs.RemoveAll(program => !ListFilterMatcher.AnyText(
                [program.CodeReferral, program.Name], filters.Program));

            if (!filters.HasElementFilter)
                return;

            foreach (var program in programs)
            {
                program.Elements = program.Elements
                    .Where(element =>
                        ListFilterMatcher.Exact(element.TypeElement, filters.Type) &&
                        ListFilterMatcher.Text(element.Name, filters.Name) &&
                        ListFilterMatcher.Text(element.Description, filters.Description) &&
                        (filters.Status == ElementListFiltersViewModel.NotUploadedFilterValue
                            ? string.IsNullOrWhiteSpace(element.StatusApprovals)
                            : ListFilterMatcher.Exact(element.StatusApprovals, filters.Status)) &&
                        ListFilterMatcher.Date(element.UploadDate, filters.DateFrom, filters.DateTo))
                    .ToList();
            }

            programs.RemoveAll(program => program.Elements.Count == 0);
        }

        private void SetPagination(int totalCount, int page, int pageSize, string sort, string direction)
        {
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalCount = totalCount;
            ViewBag.TotalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            ViewBag.Sort = sort;
            ViewBag.Direction = direction.Equals("desc", StringComparison.OrdinalIgnoreCase) ? "desc" : "asc";
        }

        private async Task<List<User>> GetApprovedApprovers()
        {
            return await GetApprovedUsersInRole(AppRoles.Approver);
        }

        private async Task<List<User>> GetApprovedUsersInRole(string roleName)
        {
            var users = await _userManager.GetUsersInRoleAsync(roleName);
            return users
                .Where(u => u.ApprovalStatus == UserApprovalStatus.Approved)
                .OrderBy(u => u.FullName)
                .ToList();
        }

        private static string GetRoleName(int roleId)
        {
            return roleId switch
            {
                AppRoles.ManagerId => AppRoles.Manager,
                AppRoles.ApproverId => AppRoles.Approver,
                AppRoles.ModeratorId => AppRoles.Moderator,
                AppRoles.AdminId => AppRoles.Admin,
                _ => throw new ArgumentOutOfRangeException(nameof(roleId))
            };
        }

        private static string GetContentType(string? fileName)
        {
            return Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant() switch
            {
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                _ => "application/pdf"
            };
        }
    }
}
