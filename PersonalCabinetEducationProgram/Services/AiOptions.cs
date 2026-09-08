namespace PersonalCabinetEducationProgram.Services;

/// <summary>Settings for the administrator-only cloud assistant. Secrets belong in User Secrets.</summary>
public sealed class AiOptions
{
    public const string SectionName = "Ai";

    public string Provider { get; set; } = "Groq";
    public string? ApiKey { get; set; }
    public string BaseUrl { get; set; } = "https://api.groq.com/openai/v1/";
    public string? Model { get; set; }
    public int RequestTimeoutSeconds { get; set; } = 30;
    public int MaxQuestionLength { get; set; } = 800;
    public int MaxAnswerCharacters { get; set; } = 4_000;
    public int ConversationHistoryRetentionDays { get; set; } = 30;
    public int MaxStoredConversationMessages { get; set; } = 50;
    public int MaxStoredConversationMessageLength { get; set; } = 4_000;

    // Optional compatible fallback provider. Leave Model empty to keep it disabled.
    public string? FallbackProvider { get; set; }
    public string? FallbackApiKey { get; set; }
    public string? FallbackBaseUrl { get; set; }
    public string? FallbackModel { get; set; }
}
