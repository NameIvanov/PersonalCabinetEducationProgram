using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Data
{
    public class ApplicationDbContext : IdentityDbContext<User, Role, int>
    {
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
            ConfigureIdentityTables(modelBuilder);

            modelBuilder.Entity<EducationalProgram>(entity =>
            {
                entity.HasOne(p => p.User)
                    .WithMany(u => u.EducationalPrograms)
                    .HasForeignKey(p => p.UserId)
                    .OnDelete(DeleteBehavior.SetNull)
                    .HasConstraintName("fk_prog_user");
            });

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

            // Seed Roles
            modelBuilder.Entity<Role>().HasData(
                new Role { Id = AppRoles.ManagerId, Name = AppRoles.Manager, NormalizedName = "MANAGER", Description = "Руководитель ОПОП", ConcurrencyStamp = "role-manager" },
                new Role { Id = AppRoles.ApproverId, Name = AppRoles.Approver, NormalizedName = "APPROVER", Description = "Согласующий", ConcurrencyStamp = "role-approver" },
                new Role { Id = AppRoles.ModeratorId, Name = AppRoles.Moderator, NormalizedName = "MODERATOR", Description = "Модератор", ConcurrencyStamp = "role-moderator" },
                new Role { Id = AppRoles.AdminId, Name = AppRoles.Admin, NormalizedName = "ADMIN", Description = "Администратор", ConcurrencyStamp = "role-admin" }
            );

            // Seed Users
            modelBuilder.Entity<User>().HasData(
                CreateSeedUser(1, "manager", "866485796cfa8d7c0cf7111640205b83076433547577511d81f8030ae99ecea5", "Иванов Иван Иванович", "Заведующий кафедрой"),
                CreateSeedUser(2, "approver", "1c391319644c0c6e9f5955e44e55862a8fd27b3b9d9863456500096ccf512db3", "Петрова Анна Сергеевна", "Декан факультета"),
                CreateSeedUser(3, "moderator", "4c8425b174053ea6935b29c2b0e0aa4e2eab1a01b784e6ac91b8bdce9c26235a", "Сидоров Петр Алексеевич", "Модератор"),
                CreateSeedUser(4, "admin", "240be518fabd2724ddb6f04eeb1da5967448d7e831c08c8fa822809f74c720a9", "Козлова Мария Ивановна", "Администратор")
            );

            modelBuilder.Entity<IdentityUserRole<int>>().HasData(
                new IdentityUserRole<int> { UserId = 1, RoleId = AppRoles.ManagerId },
                new IdentityUserRole<int> { UserId = 2, RoleId = AppRoles.ApproverId },
                new IdentityUserRole<int> { UserId = 3, RoleId = AppRoles.ModeratorId },
                new IdentityUserRole<int> { UserId = 4, RoleId = AppRoles.AdminId }
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

        private static void ConfigureIdentityTables(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("users");
                entity.Property(u => u.Id).HasColumnName("Id");
                entity.Property(u => u.UserName).HasColumnName("username").HasMaxLength(100);
                entity.Property(u => u.NormalizedUserName).HasColumnName("normalized_username").HasMaxLength(100);
                entity.Property(u => u.Email).HasColumnName("email").HasMaxLength(256);
                entity.Property(u => u.NormalizedEmail).HasColumnName("normalized_email").HasMaxLength(256);
                entity.Property(u => u.EmailConfirmed).HasColumnName("email_confirmed");
                entity.Property(u => u.PasswordHash).HasColumnName("password_hash");
                entity.Property(u => u.SecurityStamp).HasColumnName("security_stamp");
                entity.Property(u => u.ConcurrencyStamp).HasColumnName("concurrency_stamp");
                entity.Property(u => u.PhoneNumber).HasColumnName("phone_number");
                entity.Property(u => u.PhoneNumberConfirmed).HasColumnName("phone_confirmed");
                entity.Property(u => u.TwoFactorEnabled).HasColumnName("two_factor_enabled");
                entity.Property(u => u.LockoutEnd).HasColumnName("lockout_end");
                entity.Property(u => u.LockoutEnabled).HasColumnName("lockout_enabled");
                entity.Property(u => u.AccessFailedCount).HasColumnName("access_failed_count");
                entity.HasIndex(u => u.NormalizedUserName).HasDatabaseName("ux_users_name").IsUnique();
                entity.HasIndex(u => u.NormalizedEmail).HasDatabaseName("ix_users_email");
            });

            modelBuilder.Entity<Role>(entity =>
            {
                entity.ToTable("roles");
                entity.Property(r => r.Id).HasColumnName("Id");
                entity.Property(r => r.Name).HasColumnName("Name").HasMaxLength(100);
                entity.Property(r => r.NormalizedName).HasColumnName("normalized_name").HasMaxLength(100);
                entity.Property(r => r.ConcurrencyStamp).HasColumnName("concurrency_stamp");
                entity.HasIndex(r => r.NormalizedName).HasDatabaseName("ux_roles_name").IsUnique();
            });

            modelBuilder.Entity<IdentityUserRole<int>>(entity =>
            {
                entity.ToTable("user_roles");
                entity.Property(x => x.UserId).HasColumnName("user_id");
                entity.Property(x => x.RoleId).HasColumnName("role_id");
                entity.HasKey(x => new { x.UserId, x.RoleId }).HasName("pk_user_roles");
                entity.HasIndex(x => x.RoleId).HasDatabaseName("ix_ur_role");
            });

            modelBuilder.Entity<IdentityUserClaim<int>>(entity =>
            {
                entity.ToTable("user_claims");
                entity.Property(x => x.Id).HasColumnName("Id");
                entity.Property(x => x.UserId).HasColumnName("user_id");
                entity.Property(x => x.ClaimType).HasColumnName("claim_type");
                entity.Property(x => x.ClaimValue).HasColumnName("claim_value");
                entity.HasIndex(x => x.UserId).HasDatabaseName("ix_uc_user");
            });

            modelBuilder.Entity<IdentityRoleClaim<int>>(entity =>
            {
                entity.ToTable("role_claims");
                entity.Property(x => x.Id).HasColumnName("Id");
                entity.Property(x => x.RoleId).HasColumnName("role_id");
                entity.Property(x => x.ClaimType).HasColumnName("claim_type");
                entity.Property(x => x.ClaimValue).HasColumnName("claim_value");
                entity.HasIndex(x => x.RoleId).HasDatabaseName("ix_rc_role");
            });

            modelBuilder.Entity<IdentityUserLogin<int>>(entity =>
            {
                entity.ToTable("user_logins");
                entity.Property(x => x.LoginProvider).HasColumnName("login_provider").HasMaxLength(128);
                entity.Property(x => x.ProviderKey).HasColumnName("provider_key").HasMaxLength(128);
                entity.Property(x => x.ProviderDisplayName).HasColumnName("provider_name");
                entity.Property(x => x.UserId).HasColumnName("user_id");
                entity.HasKey(x => new { x.LoginProvider, x.ProviderKey }).HasName("pk_user_logins");
                entity.HasIndex(x => x.UserId).HasDatabaseName("ix_ul_user");
            });

            modelBuilder.Entity<IdentityUserToken<int>>(entity =>
            {
                entity.ToTable("user_tokens");
                entity.Property(x => x.UserId).HasColumnName("user_id");
                entity.Property(x => x.LoginProvider).HasColumnName("login_provider").HasMaxLength(128);
                entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(128);
                entity.Property(x => x.Value).HasColumnName("value");
                entity.HasKey(x => new { x.UserId, x.LoginProvider, x.Name }).HasName("pk_user_tokens");
            });
        }

        private static User CreateSeedUser(int id, string username, string passwordHash, string fullName, string post)
        {
            return new User
            {
                Id = id,
                UserName = username,
                NormalizedUserName = username.ToUpperInvariant(),
                PasswordHash = passwordHash,
                SecurityStamp = $"security-{id}",
                ConcurrencyStamp = $"user-{id}",
                FullName = fullName,
                Post = post,
                ApprovalStatus = UserApprovalStatus.Approved,
                LockoutEnabled = true
            };
        }
    }
}
