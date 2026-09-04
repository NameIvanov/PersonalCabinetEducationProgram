using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PersonalCabinetEducationProgram.Data;
using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Services;

/// <summary>Builds a bounded, de-identified read-only summary for the cloud model.</summary>
public sealed partial class AdminAiContextService
{
    private const int MaxSecurityEvents = 35;
    private const int MaxRequestGroups = 20;
    private const int MaxBlockedIps = 20;
    private readonly ApplicationDbContext _context;

    public AdminAiContextService(ApplicationDbContext context) => _context = context;

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
