using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.ViewModels;

public sealed class DocumentAnalysisViewModel
{
    public EducationalProgramElementFile File { get; set; } = null!;
    public List<EducationalProgramElementFile> PreviousFiles { get; set; } = [];
    public List<DocumentAnalysis> History { get; set; } = [];
    public DocumentAnalysis? Selected { get; set; }
    public int HistoryPage { get; set; }
    public bool HasMoreHistory { get; set; }
    public bool CanStart { get; set; }
    public bool CloudEnabled { get; set; }
    public long MaxFileBytes { get; set; }
}
