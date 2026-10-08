using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalCabinetEducationProgram.Data;
using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Tests;

public sealed class ElementStatisticsIntegrationTests
{
    [Theory]
    [InlineData("ManagerHome", "1", AppRoles.Manager)]
    [InlineData("ApproverHome", "2", AppRoles.Approver)]
    [InlineData("ModeratorHome", "3", AppRoles.Moderator)]
    public async Task ChartCountsUnuploadedDocumentsAcrossWholeProgram(
        string controller, string userId, string role)
    {
        using var factory = new CustomWebApplicationFactory();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            foreach (var element in await context.EducationalProgramElements
                         .Where(e => e.EducationalProgramId == 1).ToListAsync())
                element.IsArchived = true;

            // Half the documents are unuploaded, including legacy stored labels.
            var statuses = new[] { "", "Новое", "Не загружено", "не загружено" }
                .SelectMany(status => Enumerable.Repeat(status, 5))
                .Concat(new[]
                {
                    ElementApprovalStatus.Uploaded, ElementApprovalStatus.OnApproval,
                    ElementApprovalStatus.Approved, ElementApprovalStatus.RevisionRequired,
                    ElementApprovalStatus.Published
                }.SelectMany(status => Enumerable.Repeat(status, 4)));
            var index = 0;
            foreach (var status in statuses)
                context.EducationalProgramElements.Add(new EducationalProgramElement
                {
                    EducationalProgramId = 1,
                    TypeElement = EducationalProgramElementTypes.Discipline,
                    Name = $"Статистика {++index:D2}",
                    StatusApprovals = status
                });

            if (role == AppRoles.Approver)
                context.ApproverAssignments.Add(new ApproverAssignment
                {
                    ApproverUserId = 2, DepartmentId = 1, FacultyId = 1,
                    AssignedByUserId = 4, AssignedAt = DateTime.UtcNow
                });
            await context.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId);
        client.DefaultRequestHeaders.Add("X-Test-Role", role);
        foreach (var suffix in new[] { "", "&page=2", "&Status=__not_uploaded" })
        {
            var html = WebUtility.HtmlDecode(await client.GetStringAsync(
                $"/{controller}/Index?programId=1{suffix}"));
            Assert.Contains("#6c757d 0% 50.00%", html);
            Assert.Matches("</span> Не загружено</td>\\s*<td class=\"fw-bold\">20</td>", html);
            Assert.Contains("<div class=\"fw-bold fs-4\">40</div>", html);
            var legend = Regex.Match(html,
                "<table class=\"table table-borderless align-middle mb-0 small\">.*?</table>",
                RegexOptions.Singleline).Value;
            Assert.Equal(6, Regex.Matches(legend, "<td class=\"fw-bold\">").Count);
            Assert.DoesNotContain("Новые", legend);
        }
    }
}
