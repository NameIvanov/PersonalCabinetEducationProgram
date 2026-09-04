using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Tests;

public sealed class WebApplicationIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public WebApplicationIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ManagerPage_WithoutAuthentication_IsUnauthorized()
    {
        using var client = CreateClient();
        var response = await client.GetAsync("/ManagerHome/Index?programId=1");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LoginForm_PostsToAccountLoginEndpoint()
    {
        using var client = CreateClient();

        var page = await client.GetStringAsync("/Account/Login");

        var action = Regex.Match(page, "<form[^>]*action=\"([^\"]*)\"", RegexOptions.IgnoreCase).Groups[1].Value;
        Assert.Equal("/Account/Login", action);
    }

    [Fact]
    public async Task AssignedManager_CanOpenProgramCabinet()
    {
        using var client = CreateClient(1, AppRoles.Manager);
        var response = await client.GetAsync("/ManagerHome/Index?programId=1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Approver_CannotOpenCurriculumImport()
    {
        using var client = CreateClient(2, AppRoles.Approver);
        var response = await client.GetAsync("/CurriculumImport/Index?programId=1");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Administrator_CanOpenAuditLog()
    {
        using var client = CreateClient(4, AppRoles.Admin);
        var response = await client.GetAsync("/Admin/Audit");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/Administration/Logs")]
    [InlineData("/Administration/Server")]
    [InlineData("/Administration/Security")]
    [InlineData("/Administration/Audit")]
    public async Task Administrator_CanOpenAdministrationPages(string url)
    {
        using var client = CreateClient(4, AppRoles.Admin);
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private HttpClient CreateClient(int? userId = null, string? role = null)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        if (userId.HasValue)
        {
            client.DefaultRequestHeaders.Add("X-Test-UserId", userId.Value.ToString());
            client.DefaultRequestHeaders.Add("X-Test-Role", role);
        }
        return client;
    }
}
