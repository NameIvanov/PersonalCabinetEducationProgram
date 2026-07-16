namespace PersonalCabinetEducationProgram.Services
{
    public sealed class PlxImportPreview
    {
        public string PlanCode { get; init; } = string.Empty;
        public string PlanName { get; init; } = string.Empty;
        public string EducationalLevel { get; init; } = string.Empty;
        public string EducationForm { get; init; } = string.Empty;
        public int? AdmissionYear { get; init; }
        public int? CoursesCount { get; init; }
        public string PlanKind { get; init; } = string.Empty;
        public string SourceAppVersion { get; init; } = string.Empty;
        public List<PlxElementCandidate> Elements { get; init; } = [];
        public List<string> Warnings { get; init; } = [];
        public int ExcludedRowsCount { get; init; }
    }

    public sealed class PlxElementCandidate
    {
        public string ExternalKey { get; init; } = string.Empty;
        public string? ParentExternalKey { get; init; }
        public string TypeElement { get; init; } = string.Empty;
        public string Code { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Details { get; init; } = string.Empty;
        public string? SourceObjectId { get; init; }
    }

    public sealed class CurriculumImportResult
    {
        public int ImportId { get; init; }
        public int CreatedCount { get; init; }
        public int UpdatedCount { get; init; }
        public int ArchivedCount { get; init; }
        public int SkippedCount { get; init; }
        public List<string> Warnings { get; init; } = [];
    }

    public sealed class StagedPlxFile
    {
        public string Token { get; init; } = string.Empty;
        public string OriginalFileName { get; init; } = string.Empty;
        public string FilePath { get; init; } = string.Empty;
        public int UserId { get; init; }
        public int ProgramId { get; init; }
        public DateTime CreatedAtUtc { get; init; }
    }
}
