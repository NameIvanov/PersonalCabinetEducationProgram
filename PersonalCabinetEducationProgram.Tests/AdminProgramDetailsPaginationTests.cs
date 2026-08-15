using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalCabinetEducationProgram.Data;
using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Tests;

public sealed class AdminProgramDetailsPaginationTests
{
    [Fact]
    public async Task ProgramDetails_PaginatesEverySectionIndependently()
    {
        using var factory = new CustomWebApplicationFactory();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            for (var index = 1; index <= 30; index++)
            {
                context.EducationalProgramElements.Add(new EducationalProgramElement
                {
                    EducationalProgramId = 1,
                    TypeElement = EducationalProgramElementTypes.Discipline,
                    Name = $"Тестовая дисциплина {index:D2}",
                    Description = $"ТД.{index:D2}",
                    StatusApprovals = string.Empty
                });
            }

            context.EducationalProgramElements.Add(new EducationalProgramElement
            {
                EducationalProgramId = 1,
                TypeElement = EducationalProgramElementTypes.Practice,
                Name = "яяя Практика должна быть на первой странице",
                Description = "ПР.ТЕСТ",
                StatusApprovals = string.Empty
            });
            await context.SaveChangesAsync();
        }

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Add("X-Test-UserId", "4");
        client.DefaultRequestHeaders.Add("X-Test-Role", AppRoles.Admin);

        using var firstPage = await client.GetAsync("/Admin/ProgramDetails?id=1");
        var firstPageHtml = await firstPage.Content.ReadAsStringAsync();
        var decodedFirstPageHtml = WebUtility.HtmlDecode(firstPageHtml);
        Assert.Equal(HttpStatusCode.OK, firstPage.StatusCode);
        Assert.Contains("яяя Практика должна быть на первой странице", decodedFirstPageHtml);
        Assert.Contains("disciplinePage=2", decodedFirstPageHtml);
        Assert.Contains("#section-Discipline", decodedFirstPageHtml);

        using var secondDisciplinePage = await client.GetAsync(
            "/Admin/ProgramDetails?id=1&disciplinePage=2");
        var secondPageHtml = await secondDisciplinePage.Content.ReadAsStringAsync();
        var decodedSecondPageHtml = WebUtility.HtmlDecode(secondPageHtml);
        Assert.Equal(HttpStatusCode.OK, secondDisciplinePage.StatusCode);
        Assert.Contains("яяя Практика должна быть на первой странице", decodedSecondPageHtml);
        Assert.Contains("2 из 2", decodedSecondPageHtml);
    }

    [Theory]
    [InlineData("/ManagerHome/Index?programId=1", "1", AppRoles.Manager)]
    [InlineData("/ApproverHome/Index?programId=1", "2", AppRoles.Approver)]
    [InlineData("/ModeratorHome/Index?programId=1", "3", AppRoles.Moderator)]
    public async Task RoleElementPagination_ReturnsToActiveSection(
        string url,
        string userId,
        string role)
    {
        using var factory = new CustomWebApplicationFactory();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            for (var index = 1; index <= 30; index++)
            {
                context.EducationalProgramElements.Add(new EducationalProgramElement
                {
                    EducationalProgramId = 1,
                    TypeElement = EducationalProgramElementTypes.Discipline,
                    Name = $"Проверка якоря {index:D2}",
                    Description = $"ЯК.{index:D2}",
                    StatusApprovals = string.Empty
                });
            }

            if (role == AppRoles.Approver)
            {
                context.ApproverAssignments.Add(new ApproverAssignment
                {
                    ApproverUserId = 2,
                    DepartmentId = 1,
                    FacultyId = 1,
                    AssignedByUserId = 4,
                    AssignedAt = DateTime.UtcNow
                });
            }

            await context.SaveChangesAsync();
        }

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId);
        client.DefaultRequestHeaders.Add("X-Test-Role", role);

        using var response = await client.GetAsync(url);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("#element-page-section", html);
    }
}
