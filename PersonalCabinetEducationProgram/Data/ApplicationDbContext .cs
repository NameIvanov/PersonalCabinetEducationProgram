using Microsoft.EntityFrameworkCore;
using PersonalCabinetEducationProgram.Models;
using System.Numerics;

namespace PersonalCabinetEducationProgram.Data
{
    public class ApplicationDbContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<EducationalProgram> EducationalPrograms { get; set; }
        public DbSet<EducationalProgramElement> EducationalProgramElements { get; set; }
        public DbSet<EducationalProgramElementComment> EducationalProgramElementComment { get; set; }
        public DbSet<PinningDepartmentFaculty> PinningDepartmentFaculties { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<Faculty> Faculties { get; set; }
        public DbSet<EducationalProgramManager> EducationalProgramManagers { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<EducationalProgramElement>(entity =>
            {
                entity.HasOne(e => e.EducationalProgram)
                      .WithMany(p => p.Elements)
                      .HasForeignKey(e => e.EducationalProgramId);
            });

            modelBuilder.Entity<EducationalProgram>(entity =>
            {
                entity.HasOne(e => e.User)
                      .WithMany(u => u.EducationalPrograms)
                      .HasForeignKey(e => e.UserId);
            });

            modelBuilder.Entity<EducationalProgramElementComment>(entity =>
            {
                entity.HasOne(c => c.Element)
                      .WithMany(e => e.Comments)
                      .HasForeignKey(c => c.EducationalProgramElementId);

                entity.HasOne(c => c.User)
                      .WithMany(u => u.Comments)
                      .HasForeignKey(c => c.UserId);
            });

            //modelBuilder.Entity<User>(entity =>
            //{
            //    entity.Ignore(u => u.EducationalProgramElements);
            //});
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();

            var connectionString = config.GetConnectionString("DefaultConnection");
            optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
        }
    }
}
