using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using PersonalCabinetEducationProgram.Data;
using PersonalCabinetEducationProgram.Models;
using PersonalCabinetEducationProgram.Services;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

namespace PersonalCabinetEducationProgram.Tests;

public sealed class ProgramYearIntegrationTests
{
    [Fact]
    public void MigrationMovesAllExistingProgramsOnce_AndMatchesModel()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseMySql(
            "Server=localhost;Database=design_only;Uid=design;Pwd=design;",
            new MySqlServerVersion(new Version(8, 0, 21)),
            mysql => mysql.SchemaBehavior(MySqlSchemaBehavior.Ignore)).Options;
        using var context = new ApplicationDbContext(options);
        const string migration = "20261008195249_GroupProgramsByYear";
        Assert.Contains(migration, context.Database.GetMigrations());
        Assert.False(context.Database.HasPendingModelChanges());
        var sql = context.GetService<IMigrator>().GenerateScript("20260907203923_AddDocumentAnalysis", migration);
        Assert.Contains("UPDATE `educational_programs`", sql);
        Assert.Contains("`year_approvals` = 2026", sql);
        Assert.DoesNotContain("WHERE `Id`", sql);
        Assert.DoesNotContain("is_archived", sql);
    }

    [Theory]
    [InlineData("ManagerHome", "1", AppRoles.Manager)]
    [InlineData("ApproverHome", "2", AppRoles.Approver)]
    [InlineData("ModeratorHome", "3", AppRoles.Moderator)]
    [InlineData("ManagerHome", "4", AppRoles.Admin)]
    public async Task SidebarPlacesProgramsInTheirYear_AndOpensSelectedFolder(
        string controller, string userId, string role)
    {
        using var factory = new CustomWebApplicationFactory();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var programs = await context.EducationalPrograms.OrderBy(p => p.Id).ToListAsync();
            Assert.All(programs, p => Assert.Equal(2026, p.YearApprovals));
            programs[1].YearApprovals = 2024;
            programs[1].CodeReferral = programs[0].CodeReferral;
            programs[1].Name = programs[0].Name;
            programs[2].IsArchived = true;
            if (role == AppRoles.Approver)
                context.ApproverAssignments.Add(new ApproverAssignment
                {
                    ApproverUserId = 2, FacultyId = 1, DepartmentId = 1,
                    AssignedByUserId = 4, AssignedAt = DateTime.UtcNow
                });
            await context.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId);
        client.DefaultRequestHeaders.Add("X-Test-Role", role);
        var html = WebUtility.HtmlDecode(await client.GetStringAsync($"/{controller}/Index?programId=2"));
        var sidebar = html[html.IndexOf("id=\"programYearFolders\"", StringComparison.Ordinal)..];
        sidebar = sidebar[..sidebar.IndexOf("app-content", StringComparison.Ordinal)];
        for (var year = 2020; year <= 2026; year++)
            Assert.Contains($"data-program-year=\"{year}\"", sidebar);
        Assert.Matches("id=\"programYear_2024\" class=\"accordion-collapse collapse show\"", sidebar);
        Assert.Matches("id=\"programYear_2026\" class=\"accordion-collapse collapse \"", sidebar);
        var yearStart = sidebar.IndexOf("data-program-year=\"2024\"", StringComparison.Ordinal);
        var yearEnd = sidebar.IndexOf("data-program-year=\"2023\"", StringComparison.Ordinal);
        var yearFolder = sidebar[yearStart..yearEnd];
        Assert.Contains("programId=2", yearFolder);
        Assert.DoesNotContain("programId=1", yearFolder);
        Assert.DoesNotContain("programId=3", sidebar);
        var ids = Regex.Matches(sidebar, "id=\"(program(?:Year|Level)[^\"]+)\"")
            .Select(match => match.Groups[1].Value).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public async Task AdminCanCreateProgramFor2020_AndMoveItTo2024()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-UserId", "4");
        client.DefaultRequestHeaders.Add("X-Test-Role", AppRoles.Admin);
        var html = await client.GetStringAsync("/Admin/Programs");
        Assert.Contains("name=\"yearApprovals\"", html);
        Assert.Contains("value=\"2020\"", html);
        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(html),
            ["codeReferral"] = "01.03.02", ["name"] = "Программа разных лет",
            ["educationalLevel"] = "Бакалавриат", ["yearApprovals"] = "2020",
            ["departmentId"] = "1", ["facultyId"] = "1", ["managerUserId"] = "1"
        };
        var response = await client.PostAsync("/Admin/CreateProgram", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        int programId;
        int version;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var program = await context.EducationalPrograms.SingleAsync(p => p.Name == "Программа разных лет");
            Assert.Equal(2020, program.YearApprovals);
            programId = program.Id;
            version = program.Version;
        }
        html = await client.GetStringAsync($"/Admin/ProgramDetails?id={programId}");
        form["__RequestVerificationToken"] = Token(html);
        form["programId"] = programId.ToString();
        form["version"] = version.ToString();
        form["yearApprovals"] = "2024";
        response = await client.PostAsync("/Admin/EditProgram", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        await using var verification = factory.Services.CreateAsyncScope();
        var db = verification.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(2024, (await db.EducationalPrograms.FindAsync(programId))!.YearApprovals);
        Assert.All(await db.EducationalPrograms.Where(p => p.Id <= 3).ToListAsync(),
            p => Assert.Equal(2026, p.YearApprovals));
    }

    [Theory]
    [InlineData(2019, false)]
    [InlineData(2020, true)]
    [InlineData(2026, true)]
    public void ProgramYearValidationStartsAt2020(int year, bool valid)
    {
        Assert.Equal(valid, EntityInputValidator.Program("01.03.02", "Программа", "Бакалавриат", year) == null);
    }

    private static string Token(string html) => WebUtility.HtmlDecode(Regex.Match(html,
        "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
}
