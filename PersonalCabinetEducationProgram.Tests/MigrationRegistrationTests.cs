using Microsoft.EntityFrameworkCore;
using PersonalCabinetEducationProgram.Data;

namespace PersonalCabinetEducationProgram.Tests;

public class MigrationRegistrationTests
{
    [Fact]
    public void FileGroupMigration_IsRegistered()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql(
                "Server=localhost;Database=test;Uid=test;Pwd=test;",
                new MySqlServerVersion(new Version(8, 0, 21)))
            .Options;

        using var context = new ApplicationDbContext(options);

        Assert.Contains("20260711000100_AddElementFileGroups", context.Database.GetMigrations());
        Assert.Contains("20260716000100_AddProgramRevisionRequiredStatus", context.Database.GetMigrations());
        Assert.Contains("20260717000100_AddPlxCurriculumImports", context.Database.GetMigrations());
        Assert.Contains("20260809000100_AddFileRemovalTracking", context.Database.GetMigrations());
        Assert.Contains("20260809000200_AddUserThemePreference", context.Database.GetMigrations());
        Assert.False(context.Database.HasPendingModelChanges());
    }
}
