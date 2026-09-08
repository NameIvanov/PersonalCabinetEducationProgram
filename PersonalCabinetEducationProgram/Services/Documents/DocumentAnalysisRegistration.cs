namespace PersonalCabinetEducationProgram.Services.Documents;

public static class DocumentAnalysisRegistration
{
    public static IServiceCollection AddDocumentAnalysis(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DocumentAnalysisOptions>().Bind(configuration.GetSection(DocumentAnalysisOptions.SectionName))
            .Validate(o => o.MaxFileBytes is > 0 and <= 52428800 && o.MaxPages is > 0 and <= 500 &&
                o.MaxTextCharacters is >= 1000 and <= 500000 && o.AnalysisTimeoutSeconds is >= 10 and <= 600 &&
                o.QueueCapacity is >= 1 and <= 100 && o.MaxQueuedPerUser is >= 1 and <= 10 &&
                o.MaxQueueWaitSeconds is >= 30 and <= 3600 && o.MaxFindings is >= 1 and <= 100,
                "Недопустимые ограничения DocumentAnalysis.")
            .Validate(o => o.Templates.All(t => !string.IsNullOrWhiteSpace(t.Id) && t.Id.Length <= 100 &&
                t.Version > 0 && t.RequiredSections.Concat(t.RequiredFields).All(s => !string.IsNullOrWhiteSpace(s) && s.Length <= 200)),
                "Некорректный шаблон DocumentAnalysis.").ValidateOnStart();
        services.AddHttpClient("DocumentSemantic", c => c.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddScoped<DocumentAnalysisAccessService>();
        services.AddScoped<DocumentAnalysisService>();
        services.AddScoped<IPdfTextExtractor, PdfTextExtractor>();
        services.AddScoped<IDocumentSemanticAnalyzer, DocumentSemanticAnalyzer>();
        services.AddHostedService<DocumentAnalysisWorker>();
        return services;
    }
}
