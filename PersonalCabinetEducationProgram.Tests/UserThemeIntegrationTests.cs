using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Tests;

public sealed class UserThemeIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public UserThemeIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AuthenticatedUser_CanSaveDarkTheme()
    {
        using var client = CreateManagerClient();
        var initialPage = await client.GetStringAsync("/ManagerHome/Index?programId=1");
        var antiforgeryToken = ExtractAntiforgeryToken(initialPage);

        var response = await client.PostAsync("/user-preferences/theme", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["theme"] = UserTheme.Dark,
            ["__RequestVerificationToken"] = antiforgeryToken
        }));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var updatedPage = await client.GetStringAsync("/ManagerHome/Index?programId=1");
        Assert.Contains("<html lang=\"ru\" data-theme=\"dark\">", updatedPage);
        Assert.Contains("class=\"theme-toggle\"", updatedPage);
        Assert.Contains("class=\"notification-bell\"", updatedPage);
    }

    [Fact]
    public async Task UnknownTheme_IsRejected()
    {
        using var client = CreateManagerClient();
        var page = await client.GetStringAsync("/ManagerHome/Index?programId=1");

        var response = await client.PostAsync("/user-preferences/theme", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["theme"] = "contrast",
            ["__RequestVerificationToken"] = ExtractAntiforgeryToken(page)
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private HttpClient CreateManagerClient()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-UserId", "1");
        client.DefaultRequestHeaders.Add("X-Test-Role", AppRoles.Manager);
        return client;
    }

    private static string ExtractAntiforgeryToken(string html)
    {
        var match = Regex.Match(
            html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"",
            RegexOptions.CultureInvariant);

        Assert.True(match.Success, "На странице отсутствует antiforgery-токен формы темы.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }
}
