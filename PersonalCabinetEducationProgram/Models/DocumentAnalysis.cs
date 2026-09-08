using System.ComponentModel.DataAnnotations;

namespace PersonalCabinetEducationProgram.Models;

public enum DocumentAnalysisPurpose { ManagerPreflight, ApproverReview, ModeratorPublication }
public enum DocumentAnalysisStatus { Queued, Running, Completed, NeedsManualReview, Failed, Cancelled }
public enum DocumentFindingStatus { Open, Accepted, Dismissed }

public sealed class DocumentAnalysis
{
    public long Id { get; set; }
    public int FileId { get; set; }
    public EducationalProgramElementFile File { get; set; } = null!;
    public int? RequestedByUserId { get; set; }
    public User? RequestedByUser { get; set; }
    [MaxLength(300)] public string RequestedByName { get; set; } = "";
    public DocumentAnalysisPurpose Purpose { get; set; }
    public int FileRevision { get; set; }
    [MaxLength(64)] public string FileHash { get; set; } = "";
    public int? PreviousFileId { get; set; }
    public EducationalProgramElementFile? PreviousFile { get; set; }
    [MaxLength(64)] public string? PreviousFileHash { get; set; }
    public DocumentAnalysisStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime DeadlineUtc { get; set; }
    // Nullable unique slots bound the durable queue across application instances.
    public int? QueueSlot { get; set; }
    [MaxLength(100)] public string? ActiveKey { get; set; }
    [MaxLength(100)] public string? UserQueueKey { get; set; }
    [ConcurrencyCheck] public Guid ConcurrencyToken { get; set; } = Guid.NewGuid();
    [MaxLength(1500)] public string Message { get; set; } = "";
    [MaxLength(4000)] public string Summary { get; set; } = "";
    [MaxLength(16000)] public string TemplateSnapshot { get; set; } = "";
    [MaxLength(100)] public string EngineVersion { get; set; } = "pdf-preflight-1";
    [MaxLength(200)] public string ProviderModel { get; set; } = "";
    public ICollection<DocumentFinding> Findings { get; set; } = [];
    public ICollection<DocumentPageDiagnostic> Pages { get; set; } = [];
    public ICollection<DocumentPageChange> Changes { get; set; } = [];
}

public sealed class DocumentFinding
{
    public long Id { get; set; }
    public long DocumentAnalysisId { get; set; }
    public DocumentAnalysis Analysis { get; set; } = null!;
    [MaxLength(200)] public string Title { get; set; } = "";
    [MaxLength(1500)] public string Description { get; set; } = "";
    [MaxLength(100)] public string Type { get; set; } = "";
    public int PageNumber { get; set; }
    [MaxLength(600)] public string Quote { get; set; } = "";
    public double Confidence { get; set; }
    public bool FromOcr { get; set; }
    [MaxLength(1500)] public string Suggestion { get; set; } = "";
    [ConcurrencyCheck] public Guid ConcurrencyToken { get; set; } = Guid.NewGuid();
    public DocumentFindingStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<DocumentFindingFeedback> Feedback { get; set; } = [];
}

public sealed class DocumentFindingFeedback
{
    public long Id { get; set; }
    public long DocumentFindingId { get; set; }
    public DocumentFinding Finding { get; set; } = null!;
    public int? UserId { get; set; }
    public User? User { get; set; }
    [MaxLength(300)] public string UserName { get; set; } = "";
    public DocumentFindingStatus Decision { get; set; }
    [MaxLength(1000)] public string Comment { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

// Technical diagnostics deliberately have no fabricated quote when the page has no text.
public sealed class DocumentPageDiagnostic
{
    public long Id { get; set; }
    public long DocumentAnalysisId { get; set; }
    public DocumentAnalysis Analysis { get; set; } = null!;
    public int PageNumber { get; set; }
    public bool HasTextLayer { get; set; }
    public bool IsBlank { get; set; }
    public bool LowReadability { get; set; }
    public int Rotation { get; set; }
    public bool FromOcr { get; set; }
    [MaxLength(500)] public string Message { get; set; } = "";
}

public sealed class DocumentPageChange
{
    public long Id { get; set; }
    public long DocumentAnalysisId { get; set; }
    public DocumentAnalysis Analysis { get; set; } = null!;
    public int? OldPageNumber { get; set; }
    public int? NewPageNumber { get; set; }
    [MaxLength(30)] public string Kind { get; set; } = "";
    [MaxLength(600)] public string BeforeQuote { get; set; } = "";
    [MaxLength(600)] public string AfterQuote { get; set; } = "";
    [MaxLength(500)] public string Description { get; set; } = "";
}
