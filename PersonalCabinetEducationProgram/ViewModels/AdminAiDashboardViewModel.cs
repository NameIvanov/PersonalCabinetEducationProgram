using PersonalCabinetEducationProgram.Services;

namespace PersonalCabinetEducationProgram.ViewModels;

public sealed class AdminAiDashboardViewModel
{
    public string Period { get; init; } = "today";
    public string PageArea { get; init; } = string.Empty;
    public int? ProgramId { get; init; }
    public bool ShowAutomaticSummary { get; init; } = true;
    public long SectionChanges { get; init; }
    public long ActivePrograms { get; init; }
    public long Elements { get; init; }
    public long NotUploaded { get; init; }
    public long Uploaded { get; init; }
    public long OnApproval { get; init; }
    public long RevisionRequired { get; init; }
    public long Approved { get; init; }
    public long Published { get; init; }
    public long FilesAdded { get; init; }
    public long WorkflowChanges { get; init; }
    public long Revisions { get; init; }
    public long Approvals { get; init; }
    public long Publications { get; init; }
    public long UnreadNotifications { get; init; }
    public long NewNotifications { get; init; }
    public long AdministrationRequests { get; init; }
    public long AdministrationClientErrors { get; init; }
    public long AdministrationServerErrors { get; init; }
    public long AdministrationSecurityEvents { get; init; }
    public long AdministrationOpenSecurityEvents { get; init; }
    public long AdministrationActiveIpBlocks { get; init; }
    public long AdministrationBlockedAccounts { get; init; }
    public long AdministrationAuditActions { get; init; }
    public IReadOnlyList<string> Priorities { get; init; } = [];
    public AiAssistantMetricsSnapshot Metrics { get; init; } = new();
}
