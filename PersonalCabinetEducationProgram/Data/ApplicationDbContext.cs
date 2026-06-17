using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PersonalCabinetEducationProgram.Models;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

namespace PersonalCabinetEducationProgram.Data
{
    public class ApplicationDbContext : IdentityDbContext<User, IdentityRole<int>, int>
    {
        public new DbSet<User> Users { get; set; }
        public DbSet<EducationalProgram> EducationalPrograms { get; set; }
        public DbSet<EducationalProgramElement> EducationalProgramElements { get; set; }
        public DbSet<EducationalProgramElementComment> EducationalProgramElementComment { get; set; }
        public DbSet<PinningDepartmentFaculty> PinningDepartmentFaculties { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<Faculty> Faculties { get; set; }
        public DbSet<EducationalProgramManager> EducationalProgramManagers { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<User>(entity =>
            {
                entity.ToTable("users", "personal_cabinet");
                entity.Property(u => u.FullName).HasColumnName("full_name");
                entity.Property(u => u.Post).HasColumnName("post");
                entity.Property(u => u.LinkRole).HasColumnName("link_role");
            });

            builder.Entity<IdentityRole<int>>(entity =>
            {
                entity.ToTable("roles", "personal_cabinet");
            });

            builder.Entity<IdentityUserRole<int>>(entity =>
            {
                entity.ToTable("user_roles", "personal_cabinet");
            });

            builder.Entity<IdentityUserClaim<int>>(entity =>
            {
                entity.ToTable("user_claims", "personal_cabinet");
            });

            builder.Entity<IdentityUserLogin<int>>(entity =>
            {
                entity.ToTable("user_logins", "personal_cabinet");
            });

            builder.Entity<IdentityUserToken<int>>(entity =>
            {
                entity.ToTable("user_tokens", "personal_cabinet");
            });

            builder.Entity<EducationalProgramElement>(entity =>
            {
                entity.HasOne(e => e.EducationalProgram)
                      .WithMany(p => p.Elements)
                      .HasForeignKey(e => e.EducationalProgramId);
            });

            builder.Entity<EducationalProgram>(entity =>
            {
                entity.HasOne(e => e.User)
                      .WithMany(u => u.EducationalPrograms)
                      .HasForeignKey(e => e.UserId);
            });

            builder.Entity<EducationalProgramElementComment>(entity =>
            {
                entity.HasOne(c => c.Element)
                      .WithMany(e => e.Comments)
                      .HasForeignKey(c => c.EducationalProgramElementId);

                entity.HasOne(c => c.User)
                      .WithMany(u => u.Comments)
                      .HasForeignKey(c => c.UserId);
            });
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();

            var connectionString = config.GetConnectionString("DefaultConnection");
            optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString),
                mySqlOptions => mySqlOptions.SchemaBehavior(MySqlSchemaBehavior.Ignore));
        }
    }
}
