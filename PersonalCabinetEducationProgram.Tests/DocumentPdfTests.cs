using Microsoft.Extensions.Options;
using PersonalCabinetEducationProgram.Models;
using PersonalCabinetEducationProgram.Services.Documents;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace PersonalCabinetEducationProgram.Tests;

public sealed class DocumentPdfTests
{
    [Fact]
    public async Task ImageOnlyPdf_ReportsMissingTextLayerAndRotation_WhenOcrIsNotConfigured()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pdf");
        try
        {
            var builder = new PdfDocumentBuilder();
            var page = builder.AddPage(PageSize.A4);
            page.SetRotation(new PageRotationDegrees(90));
            page.AddPng(Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+ip1sAAAAASUVORK5CYII="), new PdfRectangle(0, 0, 500, 700));
            await File.WriteAllBytesAsync(path, builder.Build());
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var result = await new PdfTextExtractor(Options.Create(new DocumentAnalysisOptions())).ExtractAsync(path, timeout.Token);
            var diagnostic = Assert.Single(result.Pages);
            Assert.False(diagnostic.HasTextLayer);
            Assert.False(diagnostic.IsBlank); // An image isn't asserted to be a visually blank scan.
            Assert.Equal(90, diagnostic.Rotation);
            Assert.True(diagnostic.LowReadability);
            Assert.Contains("OCR", result.OcrStatus);
            var unavailable = await new PdfTextExtractor(Options.Create(new DocumentAnalysisOptions { OcrExecutable = "nonexistent-opop-ocr-executable" })).ExtractAsync(path, timeout.Token);
            Assert.Contains("OCR недоступен", unavailable.OcrStatus);
            Assert.Empty(Assert.Single(unavailable.Pages).Text);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task RealPdf_ChildProcessExtractsPageNumbersTextAndBlankPages()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pdf");
        try
        {
            var builder = new PdfDocumentBuilder();
            var font = builder.AddStandard14Font(Standard14Font.Helvetica);
            builder.AddPage(PageSize.A4).AddText("Program starts 31.02.2026", 12, new PdfPoint(50, 700), font);
            builder.AddPage(PageSize.A4);
            await File.WriteAllBytesAsync(path, builder.Build());
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var result = await new PdfTextExtractor(Options.Create(new DocumentAnalysisOptions())).ExtractAsync(path, timeout.Token);
            Assert.Equal(2, result.Pages.Count);
            Assert.Contains("31.02.2026", result.Pages[0].Text);
            Assert.True(result.Pages[0].HasTextLayer);
            Assert.Equal(2, result.Pages[1].Number);
            Assert.True(result.Pages[1].IsBlank);
            Assert.Empty(result.Pages[1].Text);
            Assert.ThrowsAny<Exception>(() => PdfExtractionWorker.Extract(path, 1, 10000, 100000));
            Assert.ThrowsAny<Exception>(() => PdfExtractionWorker.Extract(path, 200, 3, 100000));
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new PdfTextExtractor(Options.Create(new DocumentAnalysisOptions())).ExtractAsync(path, cancelled.Token));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task InvalidPdf_IsReportedWithoutLeakingParserContents()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pdf");
        try
        {
            await File.WriteAllTextAsync(path, "%PDF-private-content-corrupt");
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var exception = await Assert.ThrowsAsync<DocumentAnalysisException>(() => new PdfTextExtractor(Options.Create(new DocumentAnalysisOptions())).ExtractAsync(path, timeout.Token));
            Assert.DoesNotContain("private-content", exception.Message);
            Assert.Contains("ручная проверка", exception.Message);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Rules_CheckDatesCodesExplicitSumsRepeatedFieldsAndTemplate_WithExactEvidence()
    {
        var pages = new[] { Page(1, "Цели\nДата: 31.02.2026\n09.03.01\n12 + 20 = 40 часов\nВсего часов: 144\nНазвание программы: Другая программа"),
            Page(2, "Всего часов: 150\n29.02.2024") };
        var findings = DocumentRules.Check(pages, new EducationalProgram { CodeReferral = "01.03.02", Name = "Программа" },
            new DocumentRequirementTemplate { Id = "org", RequiredSections = ["Цели", "Результаты"] });
        foreach (var type in new[] { "InvalidDate", "ProgramCode", "Arithmetic", "ConflictingField", "ProgramName", "RequiredContent" })
            Assert.Contains(findings, f => f.Type == type);
        Assert.All(findings, finding => Assert.True(DocumentRules.HasValidEvidence(finding, pages)));
        Assert.DoesNotContain(findings, f => f.Type == "InvalidDate" && f.Quote == "29.02.2024");
    }

    [Fact]
    public void Scans_DoNotGenerateInventedMissingSectionsOrQuotes()
    {
        var scan = new PdfPageText(1, "", false, false, false, 90);
        var findings = DocumentRules.Check([scan], new(), new() { Id = "org", RequiredSections = ["Цели"] });
        Assert.Empty(findings);
        Assert.False(DocumentRules.HasValidEvidence(new() { Title = "X", Description = "X", Type = "Semantic", PageNumber = 1, Quote = "выдуманная цитата", Confidence = .9 }, [scan]));
        Assert.Empty(DocumentRules.Check([Page(1, "Заголовок"), scan with { Number = 2 }], new(), new() { RequiredSections = ["Цели"] }));
    }

    [Fact]
    public void DifferentUnlabelledDatesAndHours_AreNotAutomaticallyContradictions()
    {
        Assert.Empty(DocumentRules.Check([Page(1, "01.09.2026 01.09.2027 144 часов 36 часов")], new(), null));
    }

    [Fact]
    public void VersionComparison_AlignsInsertedDeletedPagesAndHighlightsChangedDatesAndNumbers()
    {
        var before = new[] { Page(1, "Титул"), Page(2, "Дата: 01.09.2025\nВсего часов: 144"), Page(3, "Приложение") };
        var after = new[] { Page(1, "Титул"), Page(2, "Новый раздел"), Page(3, "Дата: 01.09.2025\nВсего часов: 144"), Page(4, "Приложение") };
        var inserted = Assert.Single(DocumentVersionComparer.Compare(before, after));
        Assert.Equal("Added", inserted.Kind);
        Assert.Equal(2, inserted.NewPageNumber);
        Assert.Equal("Removed", Assert.Single(DocumentVersionComparer.Compare(after, before)).Kind);
        var changed = Assert.Single(DocumentVersionComparer.Compare([before[1]], [Page(1, "Дата: 01.09.2026\nВсего часов: 150")]));
        Assert.Contains("даты", changed.Description);
        Assert.Contains("час", changed.Description);
        Assert.Contains("150", changed.AfterQuote);
        Assert.Contains(changed.BeforeQuote, before[1].Text);
    }

    public static PdfPageText Page(int number, string text) => new(number, text, true, false, false, 0);
}
