using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.ViewModels;

public sealed class DocumentAssistantViewModel
{
    public DocumentAnalysisPurpose Purpose { get; set; }
    public List<EducationalProgram> Programs { get; set; } = [];
    public int? ProgramId { get; set; }
    public List<EducationalProgramElementFile> Files { get; set; } = [];
    public EducationalProgramElementFile? SelectedFile { get; set; }
    public int Page { get; set; }
    public bool HasNextPage { get; set; }
}
