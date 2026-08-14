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
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<EducationalProgramElementFile> EducationalProgramElementFiles { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<CurriculumImport> CurriculumImports { get; set; }
        public DbSet<SystemRequestLog> SystemRequestLogs { get; set; }
        public DbSet<SecurityEventLog> SecurityEventLogs { get; set; }
        public DbSet<UserLoginLocation> UserLoginLocations { get; set; }
        public DbSet<UserLoginSession> UserLoginSessions { get; set; }
        public DbSet<IpAddressSecurityState> IpAddressSecurityStates { get; set; }

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            ConfigureIdentityTables(modelBuilder);

            modelBuilder.Entity<EducationalProgram>(entity =>
            {
                entity.Property(p => p.Version).IsConcurrencyToken().HasDefaultValue(1);
                entity.HasIndex(p => p.IsArchived).HasDatabaseName("ix_prog_archived");
                entity.HasOne(p => p.User)
                    .WithMany(u => u.EducationalPrograms)
                    .HasForeignKey(p => p.UserId)
                    .OnDelete(DeleteBehavior.SetNull)
                    .HasConstraintName("fk_prog_user");

                entity.HasIndex(p => p.UserId).HasDatabaseName("ix_prog_user");
            });

            modelBuilder.Entity<ApproverAssignment>(entity =>
            {
                entity.HasOne(a => a.ApproverUser)
                    .WithMany(u => u.ApproverAssignments)
                    .HasForeignKey(a => a.ApproverUserId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("fk_appr_user");

                entity.HasOne(a => a.AssignedByUser)
                    .WithMany()
                    .HasForeignKey(a => a.AssignedByUserId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("fk_appr_by");

                entity.HasOne(a => a.Faculty)
                    .WithMany()
                    .HasForeignKey(a => a.FacultyId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("fk_appr_fac");

                entity.HasOne(a => a.Department)
                    .WithMany()
                    .HasForeignKey(a => a.DepartmentId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("fk_appr_dept");

                entity.HasIndex(a => a.ApproverUserId).HasDatabaseName("ix_appr_user");
                entity.HasIndex(a => a.AssignedByUserId).HasDatabaseName("ix_appr_by");
                entity.HasIndex(a => a.FacultyId).HasDatabaseName("ix_appr_fac");
                entity.HasIndex(a => a.DepartmentId).HasDatabaseName("ix_appr_dept");
            });

            modelBuilder.Entity<EducationalProgramManager>(entity =>
            {
                entity.HasOne(m => m.EducationalProgram)
                    .WithMany(p => p.Managers)
                    .HasForeignKey(m => m.EducationalProgramId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("fk_mgr_prog");

                entity.HasOne(m => m.User)
                    .WithMany(u => u.EducationalProgramManagers)
                    .HasForeignKey(m => m.UserId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("fk_mgr_user");

                entity.HasOne(m => m.AssignedByUser)
                    .WithMany()
                    .HasForeignKey(m => m.AssignedByUserId)
                    .OnDelete(DeleteBehavior.SetNull)
                    .HasConstraintName("fk_mgr_by");

                entity.HasIndex(m => m.EducationalProgramId).HasDatabaseName("ix_mgr_prog");
                entity.HasIndex(m => m.UserId).HasDatabaseName("ix_mgr_user");
                entity.HasIndex(m => m.AssignedByUserId).HasDatabaseName("ix_mgr_by");
            });

            modelBuilder.Entity<EducationalProgramAssignment>(entity =>
            {
                entity.HasOne(a => a.EducationalProgram)
                    .WithMany(p => p.Assignments)
                    .HasForeignKey(a => a.EducationalProgramId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("fk_epa_prog");

                entity.HasOne(a => a.Department)
                    .WithMany()
                    .HasForeignKey(a => a.DepartmentId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("fk_epa_dept");

                entity.HasOne(a => a.Faculty)
                    .WithMany()
                    .HasForeignKey(a => a.FacultyId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("fk_epa_fac");

                entity.HasIndex(a => a.EducationalProgramId).HasDatabaseName("ix_epa_prog");
                entity.HasIndex(a => a.DepartmentId).HasDatabaseName("ix_epa_dept");
                entity.HasIndex(a => a.FacultyId).HasDatabaseName("ix_epa_fac");
                entity.HasIndex(a => new { a.EducationalProgramId, a.DepartmentId, a.FacultyId })
                    .IsUnique()
                    .HasDatabaseName("ux_epa_program_department_faculty");
            });

            modelBuilder.Entity<EducationalProgramElement>(entity =>
            {
                entity.Property(e => e.Version).IsConcurrencyToken().HasDefaultValue(1);
                entity.Property(e => e.ExternalSource).HasMaxLength(20);
                entity.Property(e => e.ExternalKey).HasMaxLength(300);
                entity.Property(e => e.ParentExternalKey).HasMaxLength(300);
                entity.HasIndex(e => e.IsArchived).HasDatabaseName("ix_elem_archived");
                entity.HasOne(e => e.EducationalProgram)
                    .WithMany(p => p.Elements)
                    .HasForeignKey(e => e.EducationalProgramId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("fk_elem_prog");

                entity.HasIndex(e => e.EducationalProgramId).HasDatabaseName("ix_elem_prog");
                entity.HasIndex(e => new { e.EducationalProgramId, e.ExternalSource, e.ExternalKey })
                    .IsUnique()
                    .HasDatabaseName("ux_elem_external_key");
            });

            modelBuilder.Entity<CurriculumImport>(entity =>
            {
                entity.HasOne(import => import.EducationalProgram)
                    .WithMany(program => program.CurriculumImports)
                    .HasForeignKey(import => import.EducationalProgramId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("fk_curriculum_import_program");

                entity.HasOne(import => import.ImportedByUser)
                    .WithMany(user => user.CurriculumImports)
                    .HasForeignKey(import => import.ImportedByUserId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("fk_curriculum_import_user");

                entity.HasIndex(import => import.EducationalProgramId).HasDatabaseName("ix_curriculum_import_program");
                entity.HasIndex(import => import.ImportedByUserId).HasDatabaseName("ix_curriculum_import_user");
                entity.HasIndex(import => import.ImportedAt).HasDatabaseName("ix_curriculum_import_date");
            });

            modelBuilder.Entity<EducationalProgramElementComment>(entity =>
            {
                entity.HasOne(c => c.Element)
                    .WithMany(e => e.Comments)
                    .HasForeignKey(c => c.EducationalProgramElementId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("fk_comm_elem");

                entity.HasOne(c => c.User)
                    .WithMany(u => u.Comments)
                    .HasForeignKey(c => c.UserId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("fk_comm_user");

                entity.HasIndex(c => c.EducationalProgramElementId).HasDatabaseName("ix_comm_elem");
                entity.HasIndex(c => c.UserId).HasDatabaseName("ix_comm_user");
            });

            modelBuilder.Entity<ElementStatusHistory>(entity =>
            {
                entity.HasOne(h => h.Element)
                    .WithMany()
                    .HasForeignKey(h => h.EducationalProgramElementId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("fk_hist_elem");

                entity.HasOne(h => h.User)
                    .WithMany()
                    .HasForeignKey(h => h.UserId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("fk_hist_user");

                entity.HasIndex(h => h.EducationalProgramElementId).HasDatabaseName("ix_hist_elem");
                entity.HasIndex(h => h.UserId).HasDatabaseName("ix_hist_user");
            });

            modelBuilder.Entity<Notification>(entity =>
            {
                entity.HasOne(n => n.User)
                    .WithMany(u => u.Notifications)
                    .HasForeignKey(n => n.UserId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("fk_notif_user");

                entity.HasOne(n => n.Element)
                    .WithMany(e => e.Notifications)
                    .HasForeignKey(n => n.EducationalProgramElementId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("fk_notif_elem");

                entity.HasIndex(n => n.UserId).HasDatabaseName("ix_notif_user");
                entity.HasIndex(n => n.EducationalProgramElementId).HasDatabaseName("ix_notif_elem");
                entity.HasIndex(n => new { n.UserId, n.IsRead }).HasDatabaseName("ix_notif_unread");
            });

            modelBuilder.Entity<EducationalProgramElementFile>(entity =>
            {
                entity.Property(f => f.RemovalReason).HasMaxLength(100);
                entity.HasOne(f => f.Element)
                    .WithMany(e => e.Files)
                    .HasForeignKey(f => f.EducationalProgramElementId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("fk_elem_file_element");

                entity.HasOne(f => f.UploadedByUser)
                    .WithMany(u => u.UploadedElementFiles)
                    .HasForeignKey(f => f.UploadedByUserId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("fk_elem_file_user");

                entity.HasIndex(f => f.EducationalProgramElementId).HasDatabaseName("ix_elem_file_element");
                entity.HasIndex(f => new { f.EducationalProgramElementId, f.RevisionNumber })
                    .HasDatabaseName("ix_elem_file_revision");
            });

            modelBuilder.Entity<AuditLog>(entity =>
            {
                entity.Property(a => a.EntityType).HasMaxLength(100);
                entity.Property(a => a.Action).HasMaxLength(100);
                entity.Property(a => a.CreatedAt).HasPrecision(6);
                entity.HasIndex(a => a.UserId).HasDatabaseName("ix_audit_user");
                entity.HasIndex(a => new { a.EntityType, a.EntityId }).HasDatabaseName("ix_audit_entity");
                entity.HasIndex(a => a.CreatedAt).HasDatabaseName("ix_audit_created");
            });

            modelBuilder.Entity<SystemRequestLog>(entity =>
            {
                entity.Property(log => log.OccurredAtUtc).HasPrecision(6);
                entity.HasIndex(log => log.OccurredAtUtc).HasDatabaseName("ix_request_created");
                entity.HasIndex(log => new { log.UserId, log.OccurredAtUtc }).HasDatabaseName("ix_request_user_created");
                entity.HasIndex(log => new { log.IpAddress, log.OccurredAtUtc }).HasDatabaseName("ix_request_ip_created");
                entity.HasIndex(log => new { log.StatusCode, log.OccurredAtUtc }).HasDatabaseName("ix_request_status_created");
                entity.HasIndex(log => log.TraceId).HasDatabaseName("ix_request_trace");
            });

            modelBuilder.Entity<SecurityEventLog>(entity =>
            {
                entity.Property(log => log.FirstOccurredAtUtc).HasPrecision(6);
                entity.Property(log => log.LastOccurredAtUtc).HasPrecision(6);
                entity.Property(log => log.ReviewedAtUtc).HasPrecision(6);
                entity.HasIndex(log => new { log.Status, log.LastOccurredAtUtc }).HasDatabaseName("ix_security_status_date");
                entity.HasIndex(log => new { log.Severity, log.LastOccurredAtUtc }).HasDatabaseName("ix_security_severity_date");
                entity.HasIndex(log => new { log.UserId, log.LastOccurredAtUtc }).HasDatabaseName("ix_security_user_date");
                entity.HasIndex(log => new { log.IpAddress, log.LastOccurredAtUtc }).HasDatabaseName("ix_security_ip_date");
                entity.HasIndex(log => new { log.NetworkAddress, log.NetworkPrefixLength, log.LastOccurredAtUtc })
                    .HasDatabaseName("ix_security_network_date");
                entity.HasIndex(log => log.TraceId).HasDatabaseName("ix_security_trace");
            });

            modelBuilder.Entity<UserLoginLocation>(entity =>
            {
                entity.Property(location => location.FirstSeenAtUtc).HasPrecision(6);
                entity.Property(location => location.LastSeenAtUtc).HasPrecision(6);
                entity.HasOne(location => location.User)
                    .WithMany(user => user.LoginLocations)
                    .HasForeignKey(location => location.UserId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("fk_login_location_user");
                entity.HasIndex(location => location.UserId).HasDatabaseName("ix_login_location_user");
                entity.HasIndex(location => new
                    {
                        location.UserId,
                        location.NetworkAddress,
                        location.NetworkPrefixLength
                    })
                    .IsUnique()
                    .HasDatabaseName("ux_login_location_user_network");
                entity.HasIndex(location => location.LastSeenAtUtc).HasDatabaseName("ix_login_location_last_seen");
                entity.HasIndex(location => location.CountryCode).HasDatabaseName("ix_login_location_country");
            });

            modelBuilder.Entity<UserLoginSession>(entity =>
            {
                entity.Property(session => session.CreatedAtUtc).HasPrecision(6);
                entity.Property(session => session.LastActivityAtUtc).HasPrecision(6);
                entity.Property(session => session.EndedAtUtc).HasPrecision(6);
                entity.HasOne(session => session.User)
                    .WithMany(user => user.LoginSessions)
                    .HasForeignKey(session => session.UserId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("fk_login_session_user");
                entity.HasIndex(session => session.SessionId)
                    .IsUnique()
                    .HasDatabaseName("ux_login_session_id");
                entity.HasIndex(session => new { session.UserId, session.IsActive, session.LastActivityAtUtc })
                    .HasDatabaseName("ix_login_session_user_active");
            });

            modelBuilder.Entity<IpAddressSecurityState>(entity =>
            {
                entity.Property(state => state.FirstSeenAtUtc).HasPrecision(6);
                entity.Property(state => state.LastSeenAtUtc).HasPrecision(6);
                entity.Property(state => state.AttemptWindowStartedAtUtc).HasPrecision(6);
                entity.Property(state => state.EscalationStartedAtUtc).HasPrecision(6);
                entity.Property(state => state.AccountRiskMarkedAtUtc).HasPrecision(6);
                entity.Property(state => state.AccountRiskWindowResetAtUtc).HasPrecision(6);
                entity.Property(state => state.AccountRiskLastBlockedAtUtc).HasPrecision(6);
                entity.Property(state => state.BlockedUntilUtc).HasPrecision(6);
                entity.Property(state => state.BlockedAtUtc).HasPrecision(6);
                entity.Property(state => state.UnblockedAtUtc).HasPrecision(6);
                entity.HasIndex(state => state.IpAddress)
                    .IsUnique()
                    .HasDatabaseName("ux_ip_security_address");
                entity.HasIndex(state => state.LastSeenAtUtc)
                    .HasDatabaseName("ix_ip_security_last_seen");
                entity.HasIndex(state => new { state.IsPermanentlyBlocked, state.BlockedUntilUtc })
                    .HasDatabaseName("ix_ip_security_blocked");
                entity.HasIndex(state => new { state.EscalationLevel, state.LastSeenAtUtc })
                    .HasDatabaseName("ix_ip_security_escalation");
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
                entity.Property(u => u.PreferredTheme)
                    .HasColumnName("preferred_theme")
                    .HasMaxLength(16)
                    .HasDefaultValue(UserTheme.Light);
                entity.Property(u => u.ConsecutiveInvalidUploadCount)
                    .HasColumnName("consecutive_invalid_upload_count")
                    .HasDefaultValue(0);
                entity.Property(u => u.SecurityBlockedAtUtc)
                    .HasColumnName("security_blocked_at_utc")
                    .HasPrecision(6);
                entity.Property(u => u.SecurityBlockReason)
                    .HasColumnName("security_block_reason")
                    .HasMaxLength(500);
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
                entity.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).HasConstraintName("fk_ur_user");
                entity.HasOne<Role>().WithMany().HasForeignKey(x => x.RoleId).HasConstraintName("fk_ur_role");
            });

            modelBuilder.Entity<IdentityUserClaim<int>>(entity =>
            {
                entity.ToTable("user_claims");
                entity.Property(x => x.Id).HasColumnName("Id");
                entity.Property(x => x.UserId).HasColumnName("user_id");
                entity.Property(x => x.ClaimType).HasColumnName("claim_type");
                entity.Property(x => x.ClaimValue).HasColumnName("claim_value");
                entity.HasIndex(x => x.UserId).HasDatabaseName("ix_uc_user");
                entity.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).HasConstraintName("fk_uc_user");
            });

            modelBuilder.Entity<IdentityRoleClaim<int>>(entity =>
            {
                entity.ToTable("role_claims");
                entity.Property(x => x.Id).HasColumnName("Id");
                entity.Property(x => x.RoleId).HasColumnName("role_id");
                entity.Property(x => x.ClaimType).HasColumnName("claim_type");
                entity.Property(x => x.ClaimValue).HasColumnName("claim_value");
                entity.HasIndex(x => x.RoleId).HasDatabaseName("ix_rc_role");
                entity.HasOne<Role>().WithMany().HasForeignKey(x => x.RoleId).HasConstraintName("fk_rc_role");
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
                entity.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).HasConstraintName("fk_ul_user");
            });

            modelBuilder.Entity<IdentityUserToken<int>>(entity =>
            {
                entity.ToTable("user_tokens");
                entity.Property(x => x.UserId).HasColumnName("user_id");
                entity.Property(x => x.LoginProvider).HasColumnName("login_provider").HasMaxLength(128);
                entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(128);
                entity.Property(x => x.Value).HasColumnName("value");
                entity.HasKey(x => new { x.UserId, x.LoginProvider, x.Name }).HasName("pk_user_tokens");
                entity.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).HasConstraintName("fk_ut_user");
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
