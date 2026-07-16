using PersonalCabinetEducationProgram.Models;
using PersonalCabinetEducationProgram.Services;

namespace PersonalCabinetEducationProgram.Tests;

public class EducationalProgramStatusTests
{
    [Fact]
    public void CalculateProgramStatus_RevisionRequiredHasPriority()
    {
        var status = ElementWorkflowService.CalculateProgramStatus(
            [ElementApprovalStatus.Published, ElementApprovalStatus.RevisionRequired]);

        Assert.Equal(EducationalProgramStatus.RevisionRequired, status);
    }

    [Fact]
    public void CalculateProgramStatus_ClearsRevisionStatusAfterApproval()
    {
        var status = ElementWorkflowService.CalculateProgramStatus(
            [ElementApprovalStatus.Approved, ElementApprovalStatus.Published]);

        Assert.Equal(EducationalProgramStatus.Approved, status);
    }

    [Theory]
    [InlineData(ElementApprovalStatus.Published, ElementApprovalStatus.Published, EducationalProgramStatus.Published)]
    [InlineData(ElementApprovalStatus.Uploaded, ElementApprovalStatus.Approved, EducationalProgramStatus.Draft)]
    [InlineData("", ElementApprovalStatus.Approved, EducationalProgramStatus.Draft)]
    public void CalculateProgramStatus_ReturnsExpectedStatus(string first, string second, string expected)
    {
        Assert.Equal(expected, ElementWorkflowService.CalculateProgramStatus([first, second]));
    }
}
