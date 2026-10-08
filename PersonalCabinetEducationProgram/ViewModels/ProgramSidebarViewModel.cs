using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.ViewModels;

public sealed class ProgramSidebarViewModel
{
    public IReadOnlyList<EducationalProgram> Programs { get; init; } = [];
    public int? SelectedProgramId { get; init; }
}
