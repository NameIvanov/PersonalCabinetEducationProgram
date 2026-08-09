using PersonalCabinetEducationProgram.Models;
using PersonalCabinetEducationProgram.Services;

namespace PersonalCabinetEducationProgram.ViewModels
{
    public sealed class CurriculumImportIndexViewModel
    {
        public EducationalProgram Program { get; init; } = null!;
        public IReadOnlyList<CurriculumImport> Imports { get; init; } = [];
        public CurriculumImportListFiltersViewModel Filters { get; init; } = new();
        public int Page { get; init; }
        public int TotalPages { get; init; }
        public string Sort { get; init; } = "date";
        public string Direction { get; init; } = "desc";
    }

    public sealed class CurriculumImportPreviewViewModel
    {
        public EducationalProgram Program { get; init; } = null!;
        public PlxImportPreview Preview { get; init; } = null!;
        public string Token { get; init; } = string.Empty;
        public string OriginalFileName { get; init; } = string.Empty;
        public bool RequiresMismatchConfirmation { get; init; }
    }
}
