namespace PersonalCabinetEducationProgram.Services.Documents;

public sealed class DocumentAnalysisOptions
{
    public const string SectionName = "DocumentAnalysis";
    public long MaxFileBytes { get; set; } = 20 * 1024 * 1024;
    public int MaxPages { get; set; } = 200;
    public int MaxTextCharacters { get; set; } = 120000;
    public int AnalysisTimeoutSeconds { get; set; } = 120;
    public int QueueCapacity { get; set; } = 20;
    public int MaxQueuedPerUser { get; set; } = 2;
    public int MaxQueueWaitSeconds { get; set; } = 900;
    public string DotnetExecutable { get; set; } = "dotnet";
    public string? OcrExecutable { get; set; }
    public string OcrLanguages { get; set; } = "rus+eng";
    public string OrganizationKey { get; set; } = "default";
    public List<DocumentRequirementTemplate> Templates { get; set; } = [];
    public bool SemanticAnalysisEnabled { get; set; }
    public string Endpoint { get; set; } = "https://api.groq.com/openai/v1/chat/completions";
    public string Model { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public int ProviderTimeoutSeconds { get; set; } = 45;
    public int MaxFindings { get; set; } = 40;
}

public sealed class DocumentRequirementTemplate
{
    public string Id { get; set; } = "";
    public int Version { get; set; } = 1;
    public string OrganizationKey { get; set; } = "default";
    public string ElementType { get; set; } = "*";
    public string EducationalLevel { get; set; } = "*";
    public List<string> RequiredSections { get; set; } = [];
    public List<string> RequiredFields { get; set; } = [];
}

public sealed class DocumentAnalysisException(string message) : Exception(message);
