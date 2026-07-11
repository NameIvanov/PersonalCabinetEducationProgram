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
    }
}
