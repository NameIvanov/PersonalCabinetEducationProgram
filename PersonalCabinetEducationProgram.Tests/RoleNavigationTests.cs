using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Tests;

public sealed class RoleNavigationTests
{
    [Theory]
    [InlineData(1, AppRoles.Manager, "/ManagerHome")]
    [InlineData(2, AppRoles.Approver, "/ApproverHome")]
    [InlineData(3, AppRoles.Moderator, "/ModeratorHome")]
    [InlineData(4, AppRoles.Admin, "/Admin/Users")]
    public async Task BrandReturnsEachRoleToItsCabinet_AndAssistantEntryIsVisible(int id, string role, string expectedTarget)
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-UserId", id.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Role", role);
        var html = await client.GetStringAsync("/Account/AccessDenied");
        var brand = Regex.Match(html, "class=\"app-brand\"[^>]*href=\"([^\"]+)\"");
        Assert.True(brand.Success);
        Assert.Equal("/Account/RedirectByRole", brand.Groups[1].Value);
        var redirect = await client.GetAsync(brand.Groups[1].Value);
        Assert.Equal(HttpStatusCode.Redirect, redirect.StatusCode);
        Assert.StartsWith(expectedTarget, redirect.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(redirect.Headers.Location)).StatusCode);
        Assert.Contains("ИИ-помощник", WebUtility.HtmlDecode(html));
        if (role == AppRoles.Admin) Assert.Contains("id=\"assistantToggle\"", html);
        else
        {
            Assert.Contains("href=\"/DocumentAssistant/Index\"", html);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/DocumentAssistant/Index")).StatusCode);
            Assert.DoesNotContain("id=\"assistantToggle\"", html);
        }
    }

    [Fact]
    public async Task AnonymousBrandOpensLogin()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        var html = await client.GetStringAsync("/Account/Login");
        var destination = Regex.Match(html, "class=\"app-brand\"[^>]*href=\"([^\"]+)\"").Groups[1].Value;
        Assert.Contains(destination, new[] { "/", "/Account/Login" });
        Assert.Contains("action=\"/Account/Login\"", await client.GetStringAsync(destination));
    }
}
