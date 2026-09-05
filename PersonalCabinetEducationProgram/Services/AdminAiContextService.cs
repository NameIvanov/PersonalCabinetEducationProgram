using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PersonalCabinetEducationProgram.Data;
using PersonalCabinetEducationProgram.Models;
using PersonalCabinetEducationProgram.ViewModels;

namespace PersonalCabinetEducationProgram.Services;

/// <summary>Builds a bounded, de-identified read-only summary for the cloud model.</summary>
public sealed partial class AdminAiContextService
{
    private const int MaxSecurityEvents = 35;
    private const int MaxRequestGroups = 20;
    private const int MaxBlockedIps = 20;
    private readonly ApplicationDbContext _context;
    private readonly AiAssistantMetrics _metrics;

    public AdminAiContextService(ApplicationDbContext context, AiAssistantMetrics? metrics = null)
    {
        _context = context;
        _metrics = metrics ?? new AiAssistantMetrics();
    }

    public static string NormalizePeriod(string? period) => period?.Trim().ToLowerInvariant() switch
    {
        "today" => "today",
        "24h" => "24h",
        "week" => "week",
        "month" => "month",
        _ => "today"
    };

    public async Task<AdminAiDashboardViewModel> GetDashboardAsync(
        string pageArea,
        int? adminUserId,
        int? selectedProgramId,
        string? period,
        CancellationToken cancellationToken = default)
    {
        var normalizedPeriod = NormalizePeriod(period);
        var since = normalizedPeriod switch
        {
            "today" => DateTime.UtcNow.Date,
            "week" => DateTime.UtcNow.AddDays(-7),
            "month" => DateTime.UtcNow.AddDays(-30),
            "24h" => DateTime.UtcNow.AddHours(-24),
            _ => DateTime.UtcNow.Date
        };
        var elementQuery = _context.EducationalProgramElements.AsNoTracking()
            .Where(item => !item.IsArchived && (selectedProgramId == null || item.EducationalProgramId == selectedProgramId));
        var statuses = await elementQuery.Select(item => item.StatusApprovals).ToListAsync(cancellationToken);
        var normalized = statuses.Select(ElementApprovalStatus.Normalize).ToList();
        var activePrograms = await _context.EducationalPrograms.AsNoTracking().LongCountAsync(
            item => !item.IsArchived && (selectedProgramId == null || item.Id == selectedProgramId), cancellationToken);
        var filesAdded = await _context.EducationalProgramElementFiles.AsNoTracking().LongCountAsync(
            item => !item.IsRemoved && item.UploadedAt >= since && (selectedProgramId == null || item.Element.EducationalProgramId == selectedProgramId), cancellationToken);
        var changes = await _context.ElementStatusHistory.AsNoTracking()
            .Where(item => item.ChangeDate >= since && (selectedProgramId == null || item.Element.EducationalProgramId == selectedProgramId))
            .Select(item => new { item.OldStatus, item.NewStatus })
            .ToListAsync(cancellationToken);
        var unread = adminUserId.HasValue
            ? await _context.Notifications.AsNoTracking().LongCountAsync(item => item.UserId == adminUserId && !item.IsRead, cancellationToken)
            : 0;
        var newNotifications = adminUserId.HasValue
            ? await _context.Notifications.AsNoTracking().LongCountAsync(item => item.UserId == adminUserId && item.CreatedAt >= since, cancellationToken)
            : 0;
        var isAdministration = pageArea == "раздел администрирования и журналов";
        var isFacultiesOrDepartments = pageArea is "раздел факультетов" or "раздел кафедр";
        var isChangeDrivenSection = pageArea is "раздел ОПОП" or "раздел пользователей" or "раздел назначений";
        var sectionAuditQuery = _context.AuditLogs.AsNoTracking().Where(item => item.CreatedAt >= since);
        // The summary reflects user-visible changes made through the corresponding
        // section. Background security automation is reported only in Administration.
        var sectionChanges = pageArea switch
        {
            "раздел ОПОП" => await sectionAuditQuery.LongCountAsync(item =>
                item.EntityType == "EducationalProgram" &&
                (item.Action == "Created" || item.Action == "Edited" || item.Action == "Archived" || item.Action == "Restored"), cancellationToken),
            "раздел пользователей" => await sectionAuditQuery.LongCountAsync(item =>
                item.EntityType == "User" &&
                (item.Action == "Created" || item.Action == "Edited" || item.Action == "Deleted" ||
                 item.Action == "ApprovalStatusChanged" || item.Action == "PasswordReset" ||
                 item.Action == "SecurityUnlocked"), cancellationToken),
            "раздел назначений" => await sectionAuditQuery.LongCountAsync(item =>
                item.EntityType == "EducationalProgram" &&
                (item.Action == "AssignmentsChanged" || item.Action == "ManagerAssigned"), cancellationToken),
            _ => 0
        };
        var administrationRequests = 0L;
        var administrationClientErrors = 0L;
        var administrationServerErrors = 0L;
        var administrationSecurityEvents = 0L;
        var administrationOpenSecurityEvents = 0L;
        var administrationActiveIpBlocks = 0L;
        var administrationBlockedAccounts = 0L;
        var administrationAuditActions = 0L;
        if (isAdministration)
        {
            var requestQuery = _context.SystemRequestLogs.AsNoTracking().Where(item =>
                item.OccurredAtUtc >= since && item.Controller != "AdminAiAssistant");
            administrationRequests = await requestQuery.LongCountAsync(cancellationToken);
            administrationClientErrors = await requestQuery.LongCountAsync(item => item.StatusCode >= 400 && item.StatusCode < 500, cancellationToken);
            administrationServerErrors = await requestQuery.LongCountAsync(item => item.StatusCode >= 500, cancellationToken);
            administrationSecurityEvents = await _context.SecurityEventLogs.AsNoTracking()
                .LongCountAsync(item => item.LastOccurredAtUtc >= since, cancellationToken);
            administrationOpenSecurityEvents = await _context.SecurityEventLogs.AsNoTracking()
                .LongCountAsync(item => item.LastOccurredAtUtc >= since &&
                    (item.Status == SecurityEventStatuses.New || item.Status == SecurityEventStatuses.Investigating), cancellationToken);
            administrationActiveIpBlocks = await _context.IpAddressSecurityStates.AsNoTracking()
                .LongCountAsync(item => item.IsPermanentlyBlocked || item.BlockedUntilUtc > DateTime.UtcNow, cancellationToken);
            administrationBlockedAccounts = await _context.Users.AsNoTracking()
                .LongCountAsync(item => item.SecurityBlockedAtUtc != null || item.LockoutEnd > DateTime.UtcNow, cancellationToken);
            administrationAuditActions = await sectionAuditQuery.LongCountAsync(cancellationToken);
        }
        var priorities = new List<string>();
        if (isAdministration)
        {
            if (administrationServerErrors > 0) priorities.Add($"Ошибки сервера за период: {administrationServerErrors}.");
            if (administrationOpenSecurityEvents > 0) priorities.Add($"Открытые события безопасности: {administrationOpenSecurityEvents}.");
            if (administrationActiveIpBlocks > 0) priorities.Add($"Активные блокировки IP: {administrationActiveIpBlocks}.");
            if (administrationBlockedAccounts > 0) priorities.Add($"Заблокированные учётные записи: {administrationBlockedAccounts}.");
        }
        else if (isChangeDrivenSection)
        {
            if (sectionChanges > 0) priorities.Add($"Изменений в разделе за период: {sectionChanges}.");
        }
        else if (!isFacultiesOrDepartments)
        {
            if (normalized.Count(status => status == ElementApprovalStatus.RevisionRequired) > 0)
                priorities.Add($"На доработке: {normalized.Count(status => status == ElementApprovalStatus.RevisionRequired)}.");
            if (normalized.Count(status => status == ElementApprovalStatus.OnApproval) > 0)
                priorities.Add($"Ожидают согласования: {normalized.Count(status => status == ElementApprovalStatus.OnApproval)}.");
            if (unread > 0) priorities.Add($"Непрочитанные уведомления: {unread}.");
        }
        if (priorities.Count == 0 && !isFacultiesOrDepartments)
            priorities.Add(isChangeDrivenSection
                ? "За выбранный период изменений в этом разделе нет."
                : "Критичных агрегированных показателей для первоочередной проверки нет.");

        return new AdminAiDashboardViewModel
        {
            Period = normalizedPeriod,
            PageArea = pageArea,
            ProgramId = selectedProgramId,
            ShowAutomaticSummary = !isFacultiesOrDepartments && (!isChangeDrivenSection || sectionChanges > 0),
            SectionChanges = sectionChanges,
            ActivePrograms = activePrograms,
            Elements = normalized.Count,
            NotUploaded = normalized.Count(status => status == ElementApprovalStatus.NotUploaded),
            Uploaded = normalized.Count(status => status == ElementApprovalStatus.Uploaded),
            OnApproval = normalized.Count(status => status == ElementApprovalStatus.OnApproval),
            RevisionRequired = normalized.Count(status => status == ElementApprovalStatus.RevisionRequired),
            Approved = normalized.Count(status => status == ElementApprovalStatus.Approved),
            Published = normalized.Count(status => status == ElementApprovalStatus.Published),
            FilesAdded = filesAdded,
            WorkflowChanges = changes.Count,
            Revisions = changes.Count(item => ElementApprovalStatus.Normalize(item.NewStatus) == ElementApprovalStatus.RevisionRequired),
            Approvals = changes.Count(item => ElementApprovalStatus.Normalize(item.NewStatus) == ElementApprovalStatus.Approved),
            Publications = changes.Count(item => ElementApprovalStatus.Normalize(item.NewStatus) == ElementApprovalStatus.Published),
            UnreadNotifications = unread,
            NewNotifications = newNotifications,
            AdministrationRequests = administrationRequests,
            AdministrationClientErrors = administrationClientErrors,
            AdministrationServerErrors = administrationServerErrors,
            AdministrationSecurityEvents = administrationSecurityEvents,
            AdministrationOpenSecurityEvents = administrationOpenSecurityEvents,
            AdministrationActiveIpBlocks = administrationActiveIpBlocks,
            AdministrationBlockedAccounts = administrationBlockedAccounts,
            AdministrationAuditActions = administrationAuditActions,
            Priorities = priorities,
            Metrics = _metrics.GetSnapshot()
        };
    }

    /// <summary>
    /// Converts a browser path into one of a fixed set of report areas. The client
    /// never controls a database filter or any text sent to the model through this value.
    /// </summary>
    public static string ResolvePageArea(string? path) => path?.Trim().ToLowerInvariant() switch
    {
        var value when value?.StartsWith("/managerhome", StringComparison.Ordinal) == true => "раздел руководителя ОПОП",
        var value when value?.StartsWith("/approverhome", StringComparison.Ordinal) == true => "раздел согласования",
        var value when value?.StartsWith("/moderatorhome", StringComparison.Ordinal) == true => "раздел публикации",
        var value when value?.StartsWith("/notifications", StringComparison.Ordinal) == true => "раздел уведомлений",
        var value when value?.StartsWith("/administration", StringComparison.Ordinal) == true => "раздел администрирования и журналов",
        var value when value?.StartsWith("/admin/faculties", StringComparison.Ordinal) == true => "раздел факультетов",
        var value when value?.StartsWith("/admin/departments", StringComparison.Ordinal) == true => "раздел кафедр",
        var value when value?.StartsWith("/admin/users", StringComparison.Ordinal) == true => "раздел пользователей",
        var value when value?.StartsWith("/admin/assignments", StringComparison.Ordinal) == true => "раздел назначений",
        var value when value?.StartsWith("/admin/programs", StringComparison.Ordinal) == true ||
                       value?.StartsWith("/admin/programdetails", StringComparison.Ordinal) == true => "раздел ОПОП",
        var value when value?.StartsWith("/admin", StringComparison.Ordinal) == true => "раздел управления ОПОП",
        _ => "текущий административный раздел"
    };

    public async Task<string> BuildSummaryAsync(string pageArea, CancellationToken cancellationToken = default)
        => await BuildSummaryAsync(pageArea, null, cancellationToken);

    public async Task<string> BuildSummaryAsync(string pageArea, int? adminUserId, CancellationToken cancellationToken = default)
    {
        var since = DateTime.UtcNow.AddHours(-24);
        var now = DateTime.UtcNow;
        var output = new StringBuilder();
        output.AppendLine($"Текущий раздел администратора: {pageArea}.");
        output.AppendLine("Безопасная сводка за последние 24 часа. Это единственные данные, доступные модели.");
        output.AppendLine("Пользовательские имена, ФИО, e-mail, пароли, токены, строки подключения, query string и stack trace исключены.");

        var eventRows = await _context.SecurityEventLogs.AsNoTracking()
            .Where(item => item.LastOccurredAtUtc >= since)
            .OrderByDescending(item => item.LastOccurredAtUtc)
            .Take(MaxSecurityEvents * 3)
            .Select(item => new { item.LastOccurredAtUtc, item.Severity, item.EventType, item.Title, item.Description, item.Path, item.Status, item.OccurrenceCount })
            .ToListAsync(cancellationToken);
        var events = eventRows
            .Select(item => new
            {
                item.LastOccurredAtUtc,
                item.Severity,
                item.EventType,
                Title = Sanitize(item.Title, 140),
                Description = GetSafeEventDescription(item.EventType, item.Description),
                Path = SanitizePath(item.Path),
                item.Status,
                item.OccurrenceCount
            })
            .GroupBy(item => new { item.Severity, item.EventType, item.Title, item.Description, item.Path, item.Status })
            .Select(group => new
            {
                LastOccurredAtUtc = group.Max(item => item.LastOccurredAtUtc),
                group.Key.Severity,
                group.Key.EventType,
                group.Key.Title,
                group.Key.Description,
                group.Key.Path,
                group.Key.Status,
                OccurrenceCount = group.Sum(item => Math.Max(1, item.OccurrenceCount))
            })
            .OrderByDescending(item => item.LastOccurredAtUtc)
            .Take(MaxSecurityEvents)
            .ToList();
        output.AppendLine($"События безопасности ({events.Count}, максимум {MaxSecurityEvents}):");
        foreach (var item in events)
        {
            output.AppendLine($"- {item.LastOccurredAtUtc:O}; {item.Severity}; {item.EventType}; статус {item.Status}; маршрут {item.Path}; " +
                $"количество {item.OccurrenceCount}; {item.Title}; {item.Description}");
        }

        var requestGroups = await _context.SystemRequestLogs.AsNoTracking()
            .Where(item => item.OccurredAtUtc >= since && (item.StatusCode >= 400 || item.Result == SystemRequestResults.ServerError))
            .GroupBy(item => new { item.Path, item.StatusCode, item.Result })
            .Select(group => new { group.Key.Path, group.Key.StatusCode, group.Key.Result, Count = group.Count(), Last = group.Max(x => x.OccurredAtUtc) })
            .OrderByDescending(item => item.Last)
            .Take(MaxRequestGroups)
            .ToListAsync(cancellationToken);
        output.AppendLine($"Проблемные HTTP-запросы ({requestGroups.Count}, агрегировано, максимум {MaxRequestGroups}):");
        foreach (var item in requestGroups)
        {
            var label = item.StatusCode >= 500 ? "Ошибка приложения" : item.Result;
            output.AppendLine($"- {item.Last:O}; маршрут {SanitizePath(item.Path)}; HTTP {item.StatusCode}; {label}; количество {item.Count}.");
        }

        var accountBlocks = await _context.Users.AsNoTracking()
            .Where(user => user.SecurityBlockedAtUtc != null || user.LockoutEnd > now)
            .CountAsync(cancellationToken);
        output.AppendLine($"Заблокированные или временно заблокированные учётные записи: {accountBlocks}. Идентификаторы и персональные данные не передавались.");

        var ipBlocks = await _context.IpAddressSecurityStates.AsNoTracking()
            .Where(item => item.IsPermanentlyBlocked || item.BlockedUntilUtc > now ||
                           item.SuspiciousAttemptCount > 0 || item.AccountRiskMarkedAtUtc != null)
            .OrderByDescending(item => item.BlockedAtUtc)
            .Take(MaxBlockedIps)
            .Select(item => new { item.IpAddress, item.IsPermanentlyBlocked, item.BlockedUntilUtc, item.SuspiciousAttemptCount, item.EscalationLevel })
            .ToListAsync(cancellationToken);
        output.AppendLine($"IP с блокировками или требующие внимания ({ipBlocks.Count}, максимум {MaxBlockedIps}):");
        foreach (var item in ipBlocks)
        {
            var blocked = item.IsPermanentlyBlocked || item.BlockedUntilUtc > now;
            var status = blocked
                ? (item.IsPermanentlyBlocked ? "постоянная блокировка" : $"блокировка до {item.BlockedUntilUtc:O}")
                : "требует ручной проверки, блокировка не активна";
            output.AppendLine($"- IP {SanitizeIp(item.IpAddress)}; {status}; подозрительных событий {item.SuspiciousAttemptCount}; уровень {item.EscalationLevel}.");
        }

        await AppendOperationalReportAsync(output, since, pageArea, adminUserId, cancellationToken);

        return output.ToString();
    }

    public Task<string> BuildSummaryAsync(CancellationToken cancellationToken = default) =>
        BuildSummaryAsync("текущий административный раздел", cancellationToken);

    private async Task AppendOperationalReportAsync(
        StringBuilder output,
        DateTime since,
        string pageArea,
        int? adminUserId,
        CancellationToken cancellationToken)
    {
        var activePrograms = await _context.EducationalPrograms.AsNoTracking()
            .LongCountAsync(program => !program.IsArchived, cancellationToken);
        var statuses = await _context.EducationalProgramElements.AsNoTracking()
            .Where(element => !element.IsArchived)
            .Select(element => element.StatusApprovals)
            .ToListAsync(cancellationToken);
        var normalizedStatuses = statuses.Select(ElementApprovalStatus.Normalize).ToList();
        var filesToday = await _context.EducationalProgramElementFiles.AsNoTracking()
            .LongCountAsync(file => !file.IsRemoved && file.UploadedAt >= since, cancellationToken);
        var submittedToday = await _context.EducationalProgramElementFiles.AsNoTracking()
            .LongCountAsync(file => !file.IsRemoved && file.IsSubmitted && file.UploadedAt >= since, cancellationToken);
        var statusChanges = await _context.ElementStatusHistory.AsNoTracking()
            .Where(item => item.ChangeDate >= since)
            .Select(item => new { item.OldStatus, item.NewStatus })
            .ToListAsync(cancellationToken);
        var revisionToday = statusChanges.Count(item =>
            ElementApprovalStatus.Normalize(item.NewStatus) == ElementApprovalStatus.RevisionRequired);
        var approvedToday = statusChanges.Count(item =>
            ElementApprovalStatus.Normalize(item.NewStatus) == ElementApprovalStatus.Approved);
        var publishedToday = statusChanges.Count(item =>
            ElementApprovalStatus.Normalize(item.NewStatus) == ElementApprovalStatus.Published);
        var unpublishedToday = statusChanges.Count(item =>
            ElementApprovalStatus.Normalize(item.OldStatus) == ElementApprovalStatus.Published &&
            ElementApprovalStatus.Normalize(item.NewStatus) == ElementApprovalStatus.Approved);

        output.AppendLine($"Краткий операционный отчёт для «{pageArea}»:");
        output.AppendLine($"- Активных ОПОП: {activePrograms}; активных элементов: {normalizedStatuses.Count}.");
        output.AppendLine($"- Элементы по статусам: не загружено — {normalizedStatuses.Count(status => status == ElementApprovalStatus.NotUploaded)}, " +
            $"загружено — {normalizedStatuses.Count(status => status == ElementApprovalStatus.Uploaded)}, " +
            $"на согласовании — {normalizedStatuses.Count(status => status == ElementApprovalStatus.OnApproval)}, " +
            $"на доработке — {normalizedStatuses.Count(status => status == ElementApprovalStatus.RevisionRequired)}, " +
            $"согласовано — {normalizedStatuses.Count(status => status == ElementApprovalStatus.Approved)}, " +
            $"опубликовано — {normalizedStatuses.Count(status => status == ElementApprovalStatus.Published)}.");
        output.AppendLine($"- За последние 24 часа: обработано изменений элементов — {statusChanges.Count}; добавлено файлов — {filesToday}; отмечено как отправленные — {submittedToday}; " +
            $"переведено на доработку — {revisionToday}; согласовано — {approvedToday}; опубликовано — {publishedToday}.");

        output.AppendLine($"Аналитика согласующего: ожидают решения — {normalizedStatuses.Count(status => status == ElementApprovalStatus.OnApproval)}; " +
            $"за последние 24 часа согласовано — {approvedToday}, возвращено на доработку — {revisionToday}.");
        output.AppendLine($"Аналитика модератора: готовы к публикации — {normalizedStatuses.Count(status => status == ElementApprovalStatus.Approved)}; " +
            $"сейчас опубликовано — {normalizedStatuses.Count(status => status == ElementApprovalStatus.Published)}; " +
            $"за последние 24 часа опубликовано — {publishedToday}, снято с публикации — {unpublishedToday}.");

        if (adminUserId.HasValue)
        {
            var notifications = await _context.Notifications.AsNoTracking()
                .Where(notification => notification.UserId == adminUserId.Value)
                .Select(notification => new { notification.Type, notification.CreatedAt, notification.IsRead })
                .ToListAsync(cancellationToken);
            var unread = notifications.Count(notification => !notification.IsRead);
            var createdRecently = notifications.Count(notification => notification.CreatedAt >= since);
            output.AppendLine($"Обычные уведомления текущего администратора: всего — {notifications.Count}; непрочитанные — {unread}; " +
                $"создано за последние 24 часа — {createdRecently}; о файлах — {notifications.Count(notification => notification.Type == NotificationType.FileUploaded && notification.CreatedAt >= since)}; " +
                $"об изменениях статусов — {notifications.Count(notification => notification.Type == NotificationType.StatusChanged && notification.CreatedAt >= since)}; " +
                $"о комментариях — {notifications.Count(notification => notification.Type == NotificationType.CommentAdded && notification.CreatedAt >= since)}; " +
                $"события безопасности — {notifications.Count(notification => notification.Type == NotificationType.Security && notification.CreatedAt >= since)}. Тексты и отправители исключены.");
        }

        if (pageArea == "раздел согласования")
            output.AppendLine("Фокус раздела: элементы со статусом «На согласовании» ожидают решения согласующего.");
        else if (pageArea == "раздел публикации")
            output.AppendLine("Фокус раздела: публикации доступны только для согласованных элементов; учитывайте число опубликованных и ожидающих согласования.");
        else if (pageArea == "раздел руководителя ОПОП")
            output.AppendLine("Фокус раздела: отслеживайте незагруженные элементы, доработки и новые файлы; это агрегированный отчёт по всем доступным ОПОП.");
        else if (pageArea == "раздел администрирования и журналов")
            output.AppendLine("Фокус раздела: сопоставляйте операционные показатели с ошибками приложения и событиями безопасности из сводки выше.");
        else if (pageArea == "раздел уведомлений")
            output.AppendLine("Фокус раздела: используйте только агрегированные счётчики уведомлений; тексты, отправители и привязки к элементам в облако не передаются.");
    }

    internal static string Sanitize(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return "нет краткого описания";
        var compact = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        compact = SecretPattern().Replace(compact, "[СКРЫТО]");
        compact = EmailPattern().Replace(compact, "[СКРЫТО]");
        compact = SqlPattern().Replace(compact, "[СКРЫТО]");
        return compact.Length <= maxLength ? compact : compact[..maxLength] + "…";
    }

    private static string SanitizePath(string? path) => Sanitize(path?.Split('?', 2)[0], 180);
    private static string SanitizeIp(string? ip) => string.IsNullOrWhiteSpace(ip) ? "не указан" : ip.Trim()[..Math.Min(45, ip.Trim().Length)];

    private static string GetSafeEventDescription(string eventType, string? description) => eventType switch
    {
        SecurityEventTypes.ServerError => "Ошибка приложения; не является виной пользователя. Технические детали исключены.",
        SecurityEventTypes.LoginSucceeded => "Зафиксирован успешный вход; данные учётной записи исключены.",
        SecurityEventTypes.LoginFailed => "Зафиксирована неудачная попытка входа; данные учётной записи исключены.",
        SecurityEventTypes.AccountLocked => "Учётная запись временно заблокирована после неудачных попыток входа; данные учётной записи исключены.",
        SecurityEventTypes.ForeignLogin or SecurityEventTypes.NewLoginNetwork or SecurityEventTypes.LoginCountryUnknown or
            SecurityEventTypes.ImpossibleTravel or SecurityEventTypes.FrequentNetworkChanges or
            SecurityEventTypes.NewNetworkAfterFailedLogins or SecurityEventTypes.ConcurrentForeignSessions =>
            "Зафиксировано событие входа или сети; данные учётной записи исключены.",
        SecurityEventTypes.InvalidFileUpload or SecurityEventTypes.LargeFileUpload =>
            "Отклонена потенциально небезопасная загрузка файла; имя файла исключено.",
        SecurityEventTypes.IdorAttempt or SecurityEventTypes.ProtectedObjectProbe =>
            "Отклонена попытка доступа к защищённому объекту.",
        SecurityEventTypes.AccessDenied or SecurityEventTypes.Unauthorized =>
            "Доступ к защищённому ресурсу отклонён.",
        SecurityEventTypes.RateLimitExceeded => "Превышен лимит запросов.",
        _ => Sanitize(description, 220)
    };

    [GeneratedRegex(@"(?i)(password|passwd|pwd|token|secret|api[_-]?key|authorization|connection\s*string)\s*[:=][^\s;,]+")]
    private static partial Regex SecretPattern();
    [GeneratedRegex(@"(?i)[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}")]
    private static partial Regex EmailPattern();
    [GeneratedRegex(@"(?i)(select|insert|update|delete|from)\s+.{0,160}")]
    private static partial Regex SqlPattern();
}
