using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PersonalCabinetEducationProgram.Services.Documents;

namespace PersonalCabinetEducationProgram.Tests;

public sealed class DocumentSemanticAnalyzerTests
{
    [Fact]
    public async Task Provider_ReceivesOnlySelectedPages_RejectsWrongPageAndInventedQuote()
    {
        var handler = new Handler(JsonSerializer.Serialize(new { findings = new[] {
            new { title = "Риск", description = "Объяснение", pageNumber = 1, quote = "выбранный PDF", confidence = .8, suggestion = "Уточнить" },
            new { title = "Вымысел", description = "Объяснение", pageNumber = 1, quote = "чужой PDF", confidence = .8, suggestion = "Уточнить" },
            new { title = "Не та страница", description = "Объяснение", pageNumber = 2, quote = "выбранный PDF", confidence = .8, suggestion = "Уточнить" }
        } }));
        var analyzer = Create(handler);
        var result = await analyzer.AnalyzeAsync([DocumentPdfTests.Page(1, "Это выбранный PDF")], default);
        Assert.Single(result.Findings);
        Assert.False(result.Complete);
        using var request = JsonDocument.Parse(handler.Request!);
        var data = request.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!;
        using var pages = JsonDocument.Parse(data);
        Assert.Equal("Это выбранный PDF", pages.RootElement[0].GetProperty("text").GetString());
        Assert.Single(pages.RootElement.EnumerateArray());
        Assert.DoesNotContain("secret-token", handler.Request!);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task ProviderFailures_AreHonestAndDoNotEchoResponse(HttpStatusCode status)
    {
        var result = await Create(new Handler("private-provider-error", status)).AnalyzeAsync([DocumentPdfTests.Page(1, "PDF")], default);
        Assert.False(result.Complete);
        Assert.Empty(result.Findings);
        Assert.DoesNotContain("private", result.Message);
    }

    private static DocumentSemanticAnalyzer Create(Handler handler) => new(new Factory(handler), Options.Create(new DocumentAnalysisOptions
        { SemanticAnalysisEnabled = true, ApiKey = "secret-token", Model = "test-model" }));
    private sealed class Factory(Handler handler) : IHttpClientFactory { public HttpClient CreateClient(string name) => new(handler, false); }
    private sealed class Handler(string answer, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public string? Request;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Request = await request.Content!.ReadAsStringAsync(ct);
            return new(status) { Content = new StringContent(JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = answer } } } }), Encoding.UTF8, "application/json") };
        }
    }
}
