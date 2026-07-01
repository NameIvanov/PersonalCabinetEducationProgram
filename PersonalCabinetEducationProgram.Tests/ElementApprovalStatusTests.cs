using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Tests;

public class ElementApprovalStatusTests
{
    [Theory]
    [InlineData(null, ElementApprovalStatus.NotUploaded)]
    [InlineData("", ElementApprovalStatus.NotUploaded)]
    [InlineData("На рассмотрении", ElementApprovalStatus.OnApproval)]
    [InlineData("Отклонено", ElementApprovalStatus.RevisionRequired)]
    [InlineData(ElementApprovalStatus.Approved, ElementApprovalStatus.Approved)]
    public void Normalize_ReturnsCanonicalStatus(string? source, string expected)
    {
        Assert.Equal(expected, ElementApprovalStatus.Normalize(source));
    }

    [Theory]
    [InlineData(ElementApprovalStatus.Approved, true)]
    [InlineData(ElementApprovalStatus.Published, true)]
    [InlineData(ElementApprovalStatus.Uploaded, false)]
    [InlineData(ElementApprovalStatus.RevisionRequired, false)]
    [InlineData(ElementApprovalStatus.NotUploaded, false)]
    public void IsLockedForNonAdmin_LocksOnlyApprovedAndPublished(string status, bool expected)
    {
        Assert.Equal(expected, ElementApprovalStatus.IsLockedForNonAdmin(status));
    }
}
