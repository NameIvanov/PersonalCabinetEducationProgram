using Microsoft.EntityFrameworkCore;
using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Data
{
    public class ApplicationDbContext : DbContext
    {
        public DbSet<Role> Roles { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<EducationalProgram> EducationalPrograms { get; set; }
        public DbSet<EducationalProgramElement> EducationalProgramElements { get; set; }
        public DbSet<EducationalProgramElementComment> EducationalProgramElementComment { get; set; }
        public DbSet<EducationalProgramManager> EducationalProgramManagers { get; set; }
        public DbSet<Departments> Departments { get; set; }
        public DbSet<Facultys> Facultys { get; set; }
        public DbSet<EducationalProgramAssignment> EducationalProgramAssignments { get; set; }
        public DbSet<ElementStatusHistory> ElementStatusHistory { get; set; }
        public DbSet<ApproverAssignment> ApproverAssignments { get; set; }

        public ApplicationDbContext() { }
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ApproverAssignment>(entity =>
            {
                entity.HasOne(a => a.ApproverUser)
                    .WithMany(u => u.ApproverAssignments)
                    .HasForeignKey(a => a.ApproverUserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(a => a.AssignedByUser)
                    .WithMany()
                    .HasForeignKey(a => a.AssignedByUserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<EducationalProgramManager>(entity =>
            {
                entity.HasOne(m => m.User)
                    .WithMany(u => u.EducationalProgramManagers)
                    .HasForeignKey(m => m.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(m => m.AssignedByUser)
                    .WithMany()
                    .HasForeignKey(m => m.AssignedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasOne(u => u.Role)
                    .WithMany(r => r.Users)
                    .HasForeignKey(u => u.RoleId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Seed Roles
            modelBuilder.Entity<Role>().HasData(
                new Role { Id = 1, Name = AppRoles.Manager, Description = "Руководитель ОПОП" },
                new Role { Id = 2, Name = AppRoles.Approver, Description = "Согласующий" },
                new Role { Id = 3, Name = AppRoles.Moderator, Description = "Модератор" },
                new Role { Id = 4, Name = AppRoles.Admin, Description = "Администратор" }
            );

            // Seed Users
            modelBuilder.Entity<User>().HasData(
                new User { Id = 1, Username = "manager", PasswordHash = "866485796cfa8d7c0cf7111640205b83076433547577511d81f8030ae99ecea5", FullName = "Иванов Иван Иванович", LinkRole = AppRoles.Manager, RoleId = 1, Post = "Заведующий кафедрой", ApprovalStatus = UserApprovalStatus.Approved },
                new User { Id = 2, Username = "approver", PasswordHash = "1c391319644c0c6e9f5955e44e55862a8fd27b3b9d9863456500096ccf512db3", FullName = "Петрова Анна Сергеевна", LinkRole = AppRoles.Approver, RoleId = 2, Post = "Декан факультета", ApprovalStatus = UserApprovalStatus.Approved },
                new User { Id = 3, Username = "moderator", PasswordHash = "4c8425b174053ea6935b29c2b0e0aa4e2eab1a01b784e6ac91b8bdce9c26235a", FullName = "Сидоров Петр Алексеевич", LinkRole = AppRoles.Moderator, RoleId = 3, Post = "Модератор", ApprovalStatus = UserApprovalStatus.Approved },
                new User { Id = 4, Username = "admin", PasswordHash = "240be518fabd2724ddb6f04eeb1da5967448d7e831c08c8fa822809f74c720a9", FullName = "Козлова Мария Ивановна", LinkRole = AppRoles.Admin, RoleId = 4, Post = "Администратор", ApprovalStatus = UserApprovalStatus.Approved }
            );

            // Seed Faculties
            modelBuilder.Entity<Facultys>().HasData(
                new Facultys { Id = 1, Name = "Факультет информационных технологий" },
                new Facultys { Id = 2, Name = "Факультет математики и механики" },
                new Facultys { Id = 3, Name = "Факультет педагогического образования" }
            );

            // Seed Departments
            modelBuilder.Entity<Departments>().HasData(
                new Departments { Id = 1, CodeDepartment = "Каф.ПМИ", Name = "Кафедра прикладной математики и информатики" },
                new Departments { Id = 2, CodeDepartment = "Каф.ИВТ", Name = "Кафедра информационных вычислительных технологий" },
                new Departments { Id = 3, CodeDepartment = "Каф.МАТЕМ", Name = "Кафедра математического анализа" }
            );

            // Seed Educational Programs
            modelBuilder.Entity<EducationalProgram>().HasData(
                new EducationalProgram
                {
                    Id = 1,
                    CodeReferral = "01.03.02",
                    Name = "Прикладная математика и информатика",
                    EducationalLevel = "Бакалавриат",
                    Status = EducationalProgramStatus.Draft,
                    UserId = 1
                },
                new EducationalProgram
                {
                    Id = 2,
                    CodeReferral = "09.03.01",
                    Name = "Информатика и вычислительная техника",
                    EducationalLevel = "Бакалавриат",
                    Status = EducationalProgramStatus.Draft,
                    UserId = 1
                },
                new EducationalProgram
                {
                    Id = 3,
                    CodeReferral = "44.03.05",
                    Name = "Педагогическое образование (Математика. Информатика)",
                    EducationalLevel = "Бакалавриат",
                    Status = EducationalProgramStatus.Draft,
                    UserId = 1
                }
            );

            // Seed Managers
            modelBuilder.Entity<EducationalProgramManager>().HasData(
                new EducationalProgramManager { Id = 1, EducationalProgramId = 1, UserId = 1, AssignedByUserId = 4, AssignedAt = new DateTime(2026, 5, 30, 10, 0, 0) },
                new EducationalProgramManager { Id = 2, EducationalProgramId = 2, UserId = 1, AssignedByUserId = 4, AssignedAt = new DateTime(2026, 5, 30, 10, 5, 0) },
                new EducationalProgramManager { Id = 3, EducationalProgramId = 3, UserId = 1, AssignedByUserId = 4, AssignedAt = new DateTime(2026, 5, 30, 10, 10, 0) }
            );

            // Seed Assignments
            modelBuilder.Entity<EducationalProgramAssignment>().HasData(
                new EducationalProgramAssignment { Id = 1, EducationalProgramId = 1, DepartmentId = 1, FacultyId = 1 },
                new EducationalProgramAssignment { Id = 2, EducationalProgramId = 2, DepartmentId = 2, FacultyId = 1 },
                new EducationalProgramAssignment { Id = 3, EducationalProgramId = 3, DepartmentId = 1, FacultyId = 3 }
            );

            // Seed Elements
            modelBuilder.Entity<EducationalProgramElement>().HasData(
                new EducationalProgramElement
                {
                    Id = 1,
                    EducationalProgramId = 1,
                    TypeElement = "Main",
                    Name = "Учебный план (очный)",
                    Description = "Основной учебный план",
                    StatusApprovals = ""
                },
                new EducationalProgramElement
                {
                    Id = 2,
                    EducationalProgramId = 1,
                    TypeElement = "Main",
                    Name = "Пояснительная записка",
                    Description = "Общая информация",
                    StatusApprovals = ElementApprovalStatus.RevisionRequired
                },
                new EducationalProgramElement
                {
                    Id = 3,
                    EducationalProgramId = 1,
                    TypeElement = "Main",
                    Name = "Календарный учебный график",
                    Description = "График обучения",
                    StatusApprovals = ""
                },
                new EducationalProgramElement
                {
                    Id = 4,
                    EducationalProgramId = 1,
                    TypeElement = "Main",
                    Name = "Программа воспитательной работы",
                    Description = "Воспитательная программа",
                    StatusApprovals = ""
                },
                new EducationalProgramElement
                {
                    Id = 5,
                    EducationalProgramId = 1,
                    TypeElement = "Main",
                    Name = "Календарный план воспитательной работы",
                    Description = "Календарный план",
                    StatusApprovals = ""
                },
                new EducationalProgramElement
                {
                    Id = 6,
                    EducationalProgramId = 1,
                    TypeElement = "Discipline",
                    Name = "Философия",
                    Description = "Б1.О.01",
                    StatusApprovals = ElementApprovalStatus.Approved
                },
                new EducationalProgramElement
                {
                    Id = 7,
                    EducationalProgramId = 1,
                    TypeElement = "Discipline",
                    Name = "Математический анализ",
                    Description = "Б1.О.02",
                    StatusApprovals = ElementApprovalStatus.OnApproval
                },
                new EducationalProgramElement
                {
                    Id = 8,
                    EducationalProgramId = 1,
                    TypeElement = "Discipline",
                    Name = "Линейная алгебра",
                    Description = "Б1.О.03",
                    StatusApprovals = ""
                },
                new EducationalProgramElement
                {
                    Id = 9,
                    EducationalProgramId = 1,
                    TypeElement = "Discipline",
                    Name = "Программирование",
                    Description = "Б1.О.04",
                    StatusApprovals = ElementApprovalStatus.Approved
                },
                new EducationalProgramElement
                {
                    Id = 10,
                    EducationalProgramId = 1,
                    TypeElement = "Discipline",
                    Name = "Базы данных",
                    Description = "Б1.О.05",
                    StatusApprovals = ""
                },
                new EducationalProgramElement
                {
                    Id = 11,
                    EducationalProgramId = 1,
                    TypeElement = "Practice",
                    Name = "Учебная практика",
                    Description = "Практика 1",
                    StatusApprovals = ""
                },
                new EducationalProgramElement
                {
                    Id = 12,
                    EducationalProgramId = 1,
                    TypeElement = "Practice",
                    Name = "Производственная практика",
                    Description = "Практика 2",
                    StatusApprovals = ""
                },
                new EducationalProgramElement
                {
                    Id = 13,
                    EducationalProgramId = 1,
                    TypeElement = "GIA",
                    Name = "Государственный экзамен",
                    Description = "ГИА",
                    StatusApprovals = ""
                },
                new EducationalProgramElement
                {
                    Id = 14,
                    EducationalProgramId = 1,
                    TypeElement = "GIA",
                    Name = "Выпускная квалификационная работа",
                    Description = "ВКР",
                    StatusApprovals = ""
                }
            );
        }
    }
}
