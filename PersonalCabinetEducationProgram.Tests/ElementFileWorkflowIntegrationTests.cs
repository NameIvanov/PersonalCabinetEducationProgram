using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalCabinetEducationProgram.Data;
using PersonalCabinetEducationProgram.Models;
using PersonalCabinetEducationProgram.Services;

namespace PersonalCabinetEducationProgram.Tests;

public sealed class ElementFileWorkflowIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ElementFileWorkflowIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RemoveAndReplaceCurrentFiles_PreservePreviousFilesInHistory()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var workflow = scope.ServiceProvider.GetRequiredService<ElementWorkflowService>();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await workflow.MarkFilesUploadedAsync(1, 1,
            [("stored-first.pdf", "first.pdf"), ("stored-second.pdf", "second.pdf")]);
        var first = await context.EducationalProgramElementFiles.SingleAsync(f => f.OriginalFileName == "first.pdf");
        var second = await context.EducationalProgramElementFiles.SingleAsync(f => f.OriginalFileName == "second.pdf");

        await workflow.RemoveCurrentFileAsync(first.Id, 1);
        await workflow.ReplaceCurrentFileAsync(second.Id, 1, "stored-new.pdf", "new.pdf");

        var files = await context.EducationalProgramElementFiles
            .Where(f => f.EducationalProgramElementId == 1)
            .OrderBy(f => f.Id)
            .ToListAsync();
        Assert.Equal(3, files.Count);
        Assert.Equal(2, files.Count(f => f.IsRemoved && !f.IsCurrent));
        Assert.Single(files, f => f.IsCurrent && !f.IsRemoved);
        Assert.Equal("new.pdf", files.Single(f => f.IsCurrent).OriginalFileName);
        Assert.Contains(await context.ElementStatusHistory.Where(h => h.EducationalProgramElementId == 1).ToListAsync(),
            h => h.Comment.Contains("заменён", StringComparison.OrdinalIgnoreCase));
    }
}
