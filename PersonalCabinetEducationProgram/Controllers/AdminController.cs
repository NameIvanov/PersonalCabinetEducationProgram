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
    [Authorize(Roles = "Admin,Moderator")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IFileStorageService _fileStorageService;
        private readonly FileStorageSettings _storageSettings;
        private readonly ElementWorkflowService _workflowService;
        private readonly UserManager<User> _userManager;

        public AdminController(
            ApplicationDbContext context,
            IFileStorageService fileStorageService,
            IOptions<FileStorageSettings> storageSettings,
            ElementWorkflowService workflowService,
            UserManager<User> userManager)
        {
            _context = context;
            _fileStorageService = fileStorageService;
            _storageSettings = storageSettings.Value;
            _workflowService = workflowService;
            _userManager = userManager;
        }

        private int GetCurrentUserId()
        {
            return int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Users()
        {
            var users = await _userManager.Users.OrderBy(u => u.Id).ToListAsync();
            foreach (var user in users)
            {
                user.RoleName = (await _userManager.GetRolesAsync(user)).SingleOrDefault() ?? string.Empty;
            }

            return View(users);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ChangeApprovalStatus(int id, string approvalStatus, string? rejectionReason)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null)
                return NotFound();

            if (GetCurrentUserId() == id && approvalStatus == UserApprovalStatus.Rejected)
            {
                TempData["UsersError"] = "Нельзя отклонить собственный аккаунт.";
                return RedirectToAction(nameof(Users));
            }

            user.ApprovalStatus = approvalStatus;
            user.RejectionReason = approvalStatus == UserApprovalStatus.Rejected ? rejectionReason : null;

            await _userManager.UpdateAsync(user);
            await _userManager.UpdateSecurityStampAsync(user);
            return RedirectToAction(nameof(Users));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateUser(
            string fullName,
            int roleId,
            string post,
            string? username,
            string? password)
        {
            if (!AppRoles.AllIds.Contains(roleId))
                return BadRequest();

            username = string.IsNullOrWhiteSpace(username)
                ? $"user{Guid.NewGuid():N}"[..16]
                : username.Trim();
            password = string.IsNullOrWhiteSpace(password)
                ? $"Temp-{Guid.NewGuid():N}"[..14]
                : password;

            var user = new User
            {
                UserName = username,
                FullName = fullName,
                Post = post,
                ApprovalStatus = UserApprovalStatus.Approved
            };

            var createResult = await _userManager.CreateAsync(user, password);
            if (!createResult.Succeeded)
            {
                TempData["UsersError"] = string.Join(" ", createResult.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(Users));
            }

            await _userManager.AddToRoleAsync(user, GetRoleName(roleId));
            return RedirectToAction(nameof(Users));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> EditUser(int id, string fullName, int roleId, string post)
        {
            if (!AppRoles.AllIds.Contains(roleId))
                return BadRequest();

            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user != null)
            {
                user.FullName = fullName;
                user.Post = post;
                await _userManager.UpdateAsync(user);

                var currentRoles = await _userManager.GetRolesAsync(user);
                if (currentRoles.Count > 0)
                {
                    await _userManager.RemoveFromRolesAsync(user, currentRoles);
                }

                await _userManager.AddToRoleAsync(user, GetRoleName(roleId));
                await _userManager.UpdateSecurityStampAsync(user);
            }

            return RedirectToAction(nameof(Users));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user != null)
            {
                await _userManager.DeleteAsync(user);
            }

            return RedirectToAction(nameof(Users));
        }

        public async Task<IActionResult> Programs()
        {
            var programs = await _context.EducationalPrograms
                .Include(p => p.User)
                .Include(p => p.Managers).ThenInclude(m => m.User)
                .Include(p => p.Assignments).ThenInclude(a => a.Department)
                .Include(p => p.Assignments).ThenInclude(a => a.Faculty)
                .ToListAsync();

            ViewBag.Departments = await _context.Departments.ToListAsync();
            ViewBag.Facultys = await _context.Facultys.ToListAsync();
            ViewBag.Managers = await GetApprovedUsersInRole(AppRoles.Manager);

            return View(programs);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ProgramDetails(int id)
        {
            var program = await _context.EducationalPrograms
                .Include(p => p.User)
                .Include(p => p.Assignments).ThenInclude(a => a.Department)
                .Include(p => p.Assignments).ThenInclude(a => a.Faculty)
                .Include(p => p.Elements)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (program == null)
                return NotFound();

            return View(program);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Assignments()
        {
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

            var assignments = approverAssignments
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
                .OrderByDescending(a => a.AssignedAt.HasValue)
                .ThenByDescending(a => a.AssignedAt)
                .ThenBy(a => a.UserFullName)
                .ToList();

            return View(assignments);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateProgram(string codeReferral, string name, string educationalLevel,
            int yearApprovals, int departmentId, int facultyId, int? managerUserId)
        {
            var assignedManagerId = managerUserId ?? 1;

            var program = new EducationalProgram
            {
                CodeReferral = codeReferral,
                Name = name,
                EducationalLevel = educationalLevel,
                YearApprovals = yearApprovals,
                Status = EducationalProgramStatus.Draft,
                UserId = assignedManagerId
            };

            _context.EducationalPrograms.Add(program);
            await _context.SaveChangesAsync();

            _context.EducationalProgramAssignments.Add(new EducationalProgramAssignment
            {
                EducationalProgramId = program.Id,
                DepartmentId = departmentId,
                FacultyId = facultyId
            });

            _context.EducationalProgramManagers.Add(new EducationalProgramManager
            {
                EducationalProgramId = program.Id,
                UserId = assignedManagerId,
                AssignedByUserId = GetCurrentUserId(),
                AssignedAt = DateTime.Now
            });

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Programs));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AssignProgramManager(int programId, int managerUserId)
        {
            var program = await _context.EducationalPrograms
                .Include(p => p.Managers)
                .FirstOrDefaultAsync(p => p.Id == programId);

            var manager = await _userManager.FindByIdAsync(managerUserId.ToString());

            if (program == null ||
                manager == null ||
                manager.ApprovalStatus != UserApprovalStatus.Approved ||
                !await _userManager.IsInRoleAsync(manager, AppRoles.Manager))
                return NotFound();

            program.UserId = managerUserId;

            var currentAssignments = await _context.EducationalProgramManagers
                .Where(m => m.EducationalProgramId == programId)
                .ToListAsync();

            _context.EducationalProgramManagers.RemoveRange(currentAssignments);
            _context.EducationalProgramManagers.Add(new EducationalProgramManager
            {
                EducationalProgramId = programId,
                UserId = managerUserId,
                AssignedByUserId = GetCurrentUserId(),
                AssignedAt = DateTime.Now
            });

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Programs));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateProgramElement(int programId, string typeElement, string name, string description)
        {
            if (!await _context.EducationalPrograms.AnyAsync(p => p.Id == programId))
                return NotFound();

            if (!EducationalProgramElementTypes.All.Contains(typeElement))
                return BadRequest("Неизвестный тип элемента ОПОП");

            _context.EducationalProgramElements.Add(new EducationalProgramElement
            {
                EducationalProgramId = programId,
                TypeElement = typeElement,
                Name = name,
                Description = description ?? string.Empty,
                StatusApprovals = ElementApprovalStatus.NotUploaded
            });

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(ProgramDetails), new { id = programId });
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UploadElement(int elementId, IFormFile file)
        {
            var element = await _context.EducationalProgramElements.FindAsync(elementId);
            if (element == null)
                return NotFound();

            if (file != null && file.Length > 0)
            {
                var uniqueFileName = await _fileStorageService.SaveFileAsync(file);
                await _workflowService.MarkUploadedAsync(elementId, GetCurrentUserId(), uniqueFileName, file.FileName, adminOverride: true);
            }

            return RedirectToAction(nameof(ProgramDetails), new { id = element.EducationalProgramId });
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ApproveElement(int elementId, string? comment)
        {
            return await ChangeElementStatus(elementId, ElementApprovalStatus.Approved, comment ?? "Согласовано администратором");
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SendElementToRevision(int elementId, string? comment)
        {
            return await ChangeElementStatus(elementId, ElementApprovalStatus.RevisionRequired, comment ?? "Отправлено на доработку администратором");
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
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
        public async Task<IActionResult> DownloadElement(int elementId)
        {
            var element = await _context.EducationalProgramElements.FindAsync(elementId);
            if (element == null || string.IsNullOrEmpty(element.FilePath))
                return NotFound();

            var filePath = Path.Combine(_storageSettings.StoragePath, element.FilePath);
            if (!System.IO.File.Exists(filePath))
                return NotFound();

            var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
            return File(fileBytes, GetContentType(element.FileName), element.FileName ?? "download");
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> PreviewElement(int elementId)
        {
            var element = await _context.EducationalProgramElements.FindAsync(elementId);
            if (element == null || string.IsNullOrEmpty(element.FilePath))
                return NotFound();

            var filePath = Path.Combine(_storageSettings.StoragePath, element.FilePath);
            if (!System.IO.File.Exists(filePath))
                return NotFound();

            var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
            Response.Headers.Append("Content-Disposition", $"inline; filename=\"{element.FileName ?? "preview"}\"");
            return File(fileBytes, GetContentType(element.FileName));
        }

        private async Task<IActionResult> ChangeElementStatus(int elementId, string newStatus, string comment)
        {
            var element = await _workflowService.ChangeStatusAsync(elementId, GetCurrentUserId(), newStatus, comment, adminOverride: true);
            if (element == null)
                return NotFound();

            return RedirectToAction(nameof(ProgramDetails), new { id = element.EducationalProgramId });
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AssignApproverToFaculty(int approverUserId, int facultyId)
        {
            return await CreateApproverAssignment(approverUserId, facultyId, null, nameof(Faculties));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AssignApproverToDepartment(int approverUserId, int departmentId)
        {
            return await CreateApproverAssignment(approverUserId, null, departmentId, nameof(Departments));
        }

        private async Task<IActionResult> CreateApproverAssignment(int approverUserId, int? facultyId, int? departmentId, string redirectAction)
        {
            if (facultyId == null && departmentId == null)
                return BadRequest();

            var approver = await _userManager.FindByIdAsync(approverUserId.ToString());

            if (approver == null ||
                approver.ApprovalStatus != UserApprovalStatus.Approved ||
                !await _userManager.IsInRoleAsync(approver, AppRoles.Approver))
                return NotFound();

            if (facultyId != null && !await _context.Facultys.AnyAsync(f => f.Id == facultyId))
                return NotFound();

            if (departmentId != null && !await _context.Departments.AnyAsync(d => d.Id == departmentId))
                return NotFound();

            var exists = await _context.ApproverAssignments.AnyAsync(a =>
                a.ApproverUserId == approverUserId && a.FacultyId == facultyId && a.DepartmentId == departmentId);

            if (!exists)
            {
                _context.ApproverAssignments.Add(new ApproverAssignment
                {
                    ApproverUserId = approverUserId,
                    FacultyId = facultyId,
                    DepartmentId = departmentId,
                    AssignedByUserId = GetCurrentUserId(),
                    AssignedAt = DateTime.Now
                });

                await _context.SaveChangesAsync();
            }

            return RedirectToAction(redirectAction);
        }

        public async Task<IActionResult> Departments()
        {
            var departments = await _context.Departments.ToListAsync();
            ViewBag.Approvers = await GetApprovedApprovers();
            return View(departments);
        }

        public async Task<IActionResult> DepartmentDetails(int id)
        {
            var department = await _context.Departments.FindAsync(id);
            if (department == null)
                return NotFound();

            var programs = await _context.EducationalPrograms
                .Where(p => p.Assignments.Any(a => a.DepartmentId == id))
                .Include(p => p.Assignments).ThenInclude(a => a.Department)
                .Include(p => p.Assignments).ThenInclude(a => a.Faculty)
                .Include(p => p.Elements)
                .OrderBy(p => p.CodeReferral)
                .ToListAsync();

            return View("OrganizationDocuments", new OrganizationDocumentsViewModel
            {
                PageTitle = "Документы кафедры",
                EntityType = "Department",
                EntityId = department.Id,
                EntityName = department.Name,
                Programs = programs
            });
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateDepartment(string codeDepartment, string name)
        {
            _context.Departments.Add(new Departments { CodeDepartment = codeDepartment, Name = name });
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Departments));
        }

        public async Task<IActionResult> Faculties()
        {
            var faculties = await _context.Facultys.ToListAsync();
            ViewBag.Approvers = await GetApprovedApprovers();
            return View(faculties);
        }

        public async Task<IActionResult> FacultyDetails(int id)
        {
            var faculty = await _context.Facultys.FindAsync(id);
            if (faculty == null)
                return NotFound();

            var programs = await _context.EducationalPrograms
                .Where(p => p.Assignments.Any(a => a.FacultyId == id))
                .Include(p => p.Assignments).ThenInclude(a => a.Department)
                .Include(p => p.Assignments).ThenInclude(a => a.Faculty)
                .Include(p => p.Elements)
                .OrderBy(p => p.CodeReferral)
                .ToListAsync();

            return View("OrganizationDocuments", new OrganizationDocumentsViewModel
            {
                PageTitle = "Документы факультета",
                EntityType = "Faculty",
                EntityId = faculty.Id,
                EntityName = faculty.Name,
                Programs = programs
            });
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateFaculty(string name)
        {
            _context.Facultys.Add(new Facultys { Name = name });
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Faculties));
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
            return Path.GetExtension(fileName).ToLowerInvariant() switch
            {
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                _ => "application/pdf"
            };
        }
    }
}
