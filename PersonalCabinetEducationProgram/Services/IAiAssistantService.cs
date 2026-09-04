namespace PersonalCabinetEducationProgram.Services;

public interface IAiAssistantService
{
    bool IsConfigured { get; }
    Task<AiAssistantResult> AskAsync(string question, string safeContext, CancellationToken cancellationToken);
}

public sealed record AiAssistantResult(bool Succeeded, bool IsConfigured, string Message)
{
    public static AiAssistantResult NotConfigured() => new(
        false,
        false,
        "ИИ-помощник пока не настроен: добавьте параметры Ai в User Secrets приложения.");
}
