using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Tests;

public class AppRolesTests
{
    [Fact]
    public void RoleIds_AreUnique()
    {
        Assert.Equal(AppRoles.AllIds.Length, AppRoles.AllIds.Distinct().Count());
    }

    [Fact]
    public void SelfRegistration_AllowsOnlyManagerAndApprover()
    {
        Assert.Equal(
            [AppRoles.ManagerId, AppRoles.ApproverId],
            AppRoles.SelfRegistrationIds);
    }
}
