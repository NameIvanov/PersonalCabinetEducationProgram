namespace PersonalCabinetEducationProgram.ViewModels;

public sealed class AdminAiAssistantViewModel
{
    public bool IsConfigured { get; init; }
    public int MaxQuestionLength { get; init; }
}

public sealed class AdminAiQuestionRequest
{
    public string? Question { get; init; }
    public string? CurrentPage { get; init; }
    public int? ProgramId { get; init; }
    public string? Period { get; init; }
}
