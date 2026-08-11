using PersonalCabinetEducationProgram.Models;
using PersonalCabinetEducationProgram.Services;

namespace PersonalCabinetEducationProgram.ViewModels
{
    public abstract class AdministrationPageViewModel
    {
        public required AdministrationNavigationViewModel Navigation { get; init; }
        public required string ActiveSection { get; init; }
    }

    public sealed class AdministrationNavigationViewModel
    {
        public long LogsToday { get; init; }
        public long OpenSecurityEvents { get; init; }
        public bool ServerAvailable { get; init; }
        public bool StorageAvailable { get; init; }
        public int? StorageUsedPercent { get; init; }
        public DateTime CheckedAtUtc { get; init; }
    }

    public sealed class AdministrationLogsViewModel : AdministrationPageViewModel
    {
        public required IReadOnlyList<SystemRequestLog> Entries { get; init; }
        public required SystemRequestLogFilters Filters { get; init; }
        public required SystemHealthSnapshot Health { get; init; }
        public required AdministrationPagination Pagination { get; init; }
        public required string Sort { get; init; }
        public required string Direction { get; init; }
    }

    public sealed class AdministrationServerViewModel : AdministrationPageViewModel
    {
        public required SystemHealthSnapshot Health { get; init; }
    }

    public sealed class AdministrationStorageViewModel : AdministrationPageViewModel
    {
        public required StorageHealthSnapshot Storage { get; init; }
    }

    public sealed class AdministrationSecurityViewModel : AdministrationPageViewModel
    {
        public required IReadOnlyList<SecurityEventLog> Entries { get; init; }
        public required SecurityEventFilters Filters { get; init; }
        public required AdministrationPagination Pagination { get; init; }
        public required string Sort { get; init; }
        public required string Direction { get; init; }
        public long NewCount { get; init; }
        public long InvestigatingCount { get; init; }
        public long HighAndCriticalCount { get; init; }
        public long ResolvedTodayCount { get; init; }
    }

    public sealed class AdministrationAuditViewModel : AdministrationPageViewModel
    {
        public required IReadOnlyList<AuditLog> Entries { get; init; }
        public required AdministrationAuditFilters Filters { get; init; }
        public required AdministrationPagination Pagination { get; init; }
        public required string Sort { get; init; }
        public required string Direction { get; init; }
    }

    public sealed class AdministrationRequestDetailsViewModel : AdministrationPageViewModel
    {
        public required SystemRequestLog Entry { get; init; }
    }

    public sealed record AdministrationPagination(int Page, int PageSize, long TotalCount)
    {
        public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
        public int FirstItem => TotalCount == 0 ? 0 : (Page - 1) * PageSize + 1;
        public int LastItem => (int)Math.Min((long)Page * PageSize, TotalCount);
    }

    public sealed class SystemRequestLogFilters
    {
        public string? UserOrIp { get; set; }
        public string? EventType { get; set; }
        public string? Result { get; set; }
        public DateOnly? DateFrom { get; set; }
        public DateOnly? DateTo { get; set; }
    }

    public sealed class SecurityEventFilters
    {
        public string? Search { get; set; }
        public string? EventType { get; set; }
        public string? Severity { get; set; }
        public string? Status { get; set; }
        public DateOnly? DateFrom { get; set; }
        public DateOnly? DateTo { get; set; }
    }

    public sealed class AdministrationAuditFilters
    {
        public string? User { get; set; }
        public string? Entity { get; set; }
        public string? Action { get; set; }
        public string? IpOrTrace { get; set; }
        public DateOnly? DateFrom { get; set; }
        public DateOnly? DateTo { get; set; }
    }
}
