using PersonalCabinetEducationProgram.Models;
using PersonalCabinetEducationProgram.Services;

namespace PersonalCabinetEducationProgram.ViewModels
{
    public sealed class CurriculumImportIndexViewModel
    {
        public EducationalProgram Program { get; init; } = null!;
        public IReadOnlyList<CurriculumImport> Imports { get; init; } = [];
    }

    public sealed class CurriculumImportPreviewViewModel
    {
        public EducationalProgram Program { get; init; } = null!;
        public PlxImportPreview Preview { get; init; } = null!;
        public string Token { get; init; } = string.Empty;
        public string OriginalFileName { get; init; } = string.Empty;
    }
}
