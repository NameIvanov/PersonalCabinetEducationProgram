using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PersonalCabinetEducationProgram.Data;
using PersonalCabinetEducationProgram.Models;
using PersonalCabinetEducationProgram.Services;
using PersonalCabinetEducationProgram.ViewModels;

namespace PersonalCabinetEducationProgram.Controllers
{
    [Authorize(Roles = AppRoles.Admin)]
    public sealed class AdministrationController : Controller
    {
        private const int PageSize = 25;
        private const int ExportLimit = 100_000;
        private readonly ApplicationDbContext _context;
        private readonly SystemHealthService _systemHealthService;
        private readonly StorageHealthService _storageHealthService;
        private readonly AuditService _auditService;
        private readonly ObjectAuthorizationIncidentService _authorizationIncidents;
        private readonly IpAddressSecurityService _ipSecurityService;

        public AdministrationController(
            ApplicationDbContext context,
            SystemHealthService systemHealthService,
            StorageHealthService storageHealthService,
            AuditService auditService,
            ObjectAuthorizationIncidentService authorizationIncidents,
            IpAddressSecurityService ipSecurityService)
        {
            _context = context;
            _systemHealthService = systemHealthService;
            _storageHealthService = storageHealthService;
            _auditService = auditService;
            _authorizationIncidents = authorizationIncidents;
            _ipSecurityService = ipSecurityService;
        }

        public IActionResult Index() => RedirectToAction(nameof(Logs));

        [AppRateLimit(AppRateLimitPolicies.Search)]
        public async Task<IActionResult> Logs(
            int page = 1,
            string sort = "date",
            string direction = "desc",
            [FromQuery] SystemRequestLogFilters? filters = null,
            CancellationToken cancellationToken = default)
        {
            filters ??= new SystemRequestLogFilters();
            var query = ApplyRequestFilters(_context.SystemRequestLogs.AsNoTracking(), filters);
            query = SortRequestLogs(query, sort, direction);
            var totalCount = await query.LongCountAsync(cancellationToken);
            var pagination = NormalizePagination(page, totalCount);
            var entries = await query
                .Skip((pagination.Page - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToListAsync(cancellationToken);
            var health = await _systemHealthService.GetSnapshotAsync(cancellationToken);

            return View(new AdministrationLogsViewModel
            {
                ActiveSection = "logs",
                Navigation = await BuildNavigationAsync(health.DatabaseAvailable, cancellationToken),
                Entries = entries,
                Filters = filters,
                Health = health,
                Pagination = pagination,
                Sort = NormalizeRequestSort(sort),
                Direction = NormalizeDirection(direction)
            });
        }

        [AppRateLimit(AppRateLimitPolicies.Search)]
        public async Task<IActionResult> RequestDetails(long id, CancellationToken cancellationToken)
        {
            var entry = await _context.SystemRequestLogs.AsNoTracking()
                .SingleOrDefaultAsync(log => log.Id == id, cancellationToken);
            if (entry == null)
                return NotFound();

            return View(new AdministrationRequestDetailsViewModel
            {
                ActiveSection = "logs",
                Navigation = await BuildNavigationAsync(true, cancellationToken),
                Entry = entry
            });
        }

        [AppRateLimit(AppRateLimitPolicies.FileDownload)]
        public async Task<IActionResult> ExportLogs(
            [FromQuery] SystemRequestLogFilters? filters = null,
            CancellationToken cancellationToken = default)
        {
            filters ??= new SystemRequestLogFilters();
            var entries = await ApplyRequestFilters(_context.SystemRequestLogs.AsNoTracking(), filters)
                .OrderByDescending(log => log.OccurredAtUtc)
                .Take(ExportLimit)
                .ToListAsync(cancellationToken);

            var csv = new StringBuilder("Время UTC;ID пользователя;Пользователь;Роль;IP;Метод;Путь;Контроллер;Действие;Код;Результат;Время ответа, мс;Trace ID\r\n");
            foreach (var entry in entries)
            {
                AppendCsvRow(csv,
                    entry.OccurredAtUtc.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    entry.UserId?.ToString(),
                    entry.UserFullName ?? entry.UserLogin ?? "Не авторизован",
                    entry.UserRole,
                    entry.IpAddress,
                    entry.HttpMethod,
                    entry.Path,
                    entry.Controller,
                    entry.Action,
                    entry.StatusCode.ToString(),
                    entry.Result,
                    entry.DurationMs.ToString(),
                    entry.TraceId);
            }

            return CsvFile(csv, $"request-logs-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
        }

        [AppRateLimit(AppRateLimitPolicies.Search)]
        public async Task<IActionResult> Server(CancellationToken cancellationToken)
        {
            var health = await _systemHealthService.GetSnapshotAsync(cancellationToken);
            return View(new AdministrationServerViewModel
            {
                ActiveSection = "server",
                Navigation = await BuildNavigationAsync(health.DatabaseAvailable, cancellationToken),
                Health = health
            });
        }

        [AppRateLimit(AppRateLimitPolicies.Search)]
        public async Task<IActionResult> Storage(CancellationToken cancellationToken)
        {
            var storage = await _storageHealthService.GetSnapshotAsync(cancellationToken);
            return View(new AdministrationStorageViewModel
            {
                ActiveSection = "storage",
                Navigation = await BuildNavigationAsync(true, cancellationToken),
                Storage = storage
            });
        }

        [AppRateLimit(AppRateLimitPolicies.Search)]
        public async Task<IActionResult> Security(
            int page = 1,
            string sort = "date",
            string direction = "desc",
            [FromQuery] SecurityEventFilters? filters = null,
            CancellationToken cancellationToken = default)
        {
            filters ??= new SecurityEventFilters();
            var query = ApplySecurityFilters(_context.SecurityEventLogs.AsNoTracking(), filters);
            query = SortSecurityEvents(query, sort, direction);
            var totalCount = await query.LongCountAsync(cancellationToken);
            var pagination = NormalizePagination(page, totalCount);
            var entries = await query
                .Skip((pagination.Page - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToListAsync(cancellationToken);
            var today = ToUtcStart(DateOnly.FromDateTime(DateTime.Now));

            return View(new AdministrationSecurityViewModel
            {
                ActiveSection = "security",
                Navigation = await BuildNavigationAsync(true, cancellationToken),
                Entries = entries,
                Filters = filters,
                Pagination = pagination,
                Sort = NormalizeSecuritySort(sort),
                Direction = NormalizeDirection(direction),
                NewCount = await _context.SecurityEventLogs.LongCountAsync(log =>
                    log.Status == SecurityEventStatuses.New && log.Severity != SecurityEventSeverities.Information,
                    cancellationToken),
                InvestigatingCount = await _context.SecurityEventLogs.LongCountAsync(log => log.Status == SecurityEventStatuses.Investigating, cancellationToken),
                HighAndCriticalCount = await _context.SecurityEventLogs.LongCountAsync(log =>
                    log.Status != SecurityEventStatuses.Resolved && log.Status != SecurityEventStatuses.FalsePositive &&
                    (log.Severity == SecurityEventSeverities.High || log.Severity == SecurityEventSeverities.Critical), cancellationToken),
                ResolvedTodayCount = await _context.SecurityEventLogs.LongCountAsync(log =>
                    log.ReviewedAtUtc >= today && log.Status == SecurityEventStatuses.Resolved, cancellationToken)
            });
        }

        [HttpPost]
        [AppRateLimit(AppRateLimitPolicies.AdminStructureMutation)]
        public async Task<IActionResult> UpdateSecurityEvent(
            long id,
            string status,
            string? reviewNote,
            CancellationToken cancellationToken)
        {
            if (!SecurityEventStatuses.IsValid(status))
                return BadRequest("Неизвестный статус события безопасности.");
            reviewNote = reviewNote?.Trim();
            if (reviewNote?.Length > 2000)
                return BadRequest("Комментарий не должен превышать 2000 символов.");
            if (status is SecurityEventStatuses.Resolved or SecurityEventStatuses.FalsePositive &&
                string.IsNullOrWhiteSpace(reviewNote))
            {
                TempData["AdministrationError"] = "Для закрытия события укажите итог расследования.";
                return RedirectToAction(nameof(Security));
            }

            var entry = await _context.SecurityEventLogs.SingleOrDefaultAsync(log => log.Id == id, cancellationToken);
            if (entry == null)
                return NotFound();

            var previousStatus = entry.Status;
            entry.Status = status;
            entry.ReviewNote = reviewNote;
            entry.ReviewedByUserId = GetCurrentUserId();
            entry.ReviewedAtUtc = DateTime.UtcNow;
            _auditService.Record(
                GetCurrentUserId(),
                "SecurityEvent",
                entry.Id,
                "ReviewStatusChanged",
                $"Статус события безопасности изменён с {previousStatus} на {status}.",
                new { Status = previousStatus },
                new { Status = status, ReviewNote = reviewNote });
            await _context.SaveChangesAsync(cancellationToken);

            if (entry.UserId.HasValue &&
                entry.Severity is SecurityEventSeverities.High or SecurityEventSeverities.Critical &&
                IpAddressNormalizer.TryNormalize(entry.IpAddress, out var riskIpAddress))
            {
                await _ipSecurityService.EvaluateAccumulatedAccountRiskAsync(riskIpAddress, cancellationToken);
            }

            TempData["AdministrationSuccess"] = "Событие безопасности обновлено.";
            return RedirectToAction(nameof(Security));
        }

        [AppRateLimit(AppRateLimitPolicies.FileDownload)]
        public async Task<IActionResult> ExportSecurity(
            [FromQuery] SecurityEventFilters? filters = null,
            CancellationToken cancellationToken = default)
        {
            filters ??= new SecurityEventFilters();
            var entries = await ApplySecurityFilters(_context.SecurityEventLogs.AsNoTracking(), filters)
                .OrderByDescending(log => log.LastOccurredAtUtc)
                .Take(ExportLimit)
                .ToListAsync(cancellationToken);
            var csv = new StringBuilder("Последнее событие UTC;Важность;Тип;Заголовок;Пользователь;ID пользователя;IP;Количество;Статус;Trace ID;Итог расследования\r\n");
            foreach (var entry in entries)
            {
                AppendCsvRow(csv,
                    entry.LastOccurredAtUtc.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    entry.Severity,
                    entry.EventType,
                    entry.Title,
                    entry.UserFullName ?? entry.UserLogin ?? "Не авторизован",
                    entry.UserId?.ToString(),
                    entry.IpAddress,
                    entry.OccurrenceCount.ToString(),
                    entry.Status,
                    entry.TraceId,
                    entry.ReviewNote);
            }
            return CsvFile(csv, $"security-events-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
        }

        [AppRateLimit(AppRateLimitPolicies.Search)]
        public async Task<IActionResult> UserNetworks(
            int userId,
            int page = 1,
            string sort = "lastSeen",
            string direction = "desc",
            CancellationToken cancellationToken = default)
        {
            var user = await _context.Users.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
            if (user == null)
                return NotFound();

            var query = SortUserNetworks(
                _context.UserLoginLocations.AsNoTracking().Where(item => item.UserId == userId),
                sort,
                direction);
            var totalCount = await query.LongCountAsync(cancellationToken);
            var pagination = NormalizePagination(page, totalCount);
            var locations = await query
                .Skip((pagination.Page - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToListAsync(cancellationToken);

            return View(new AdministrationUserNetworksViewModel
            {
                ActiveSection = "security",
                Navigation = await BuildNavigationAsync(true, cancellationToken),
                User = user,
                Locations = locations,
                Pagination = pagination,
                Sort = NormalizeNetworkSort(sort),
                Direction = NormalizeDirection(direction)
            });
        }

        [AppRateLimit(AppRateLimitPolicies.Search)]
        public async Task<IActionResult> IpAddresses(
            string category = "all",
            int page = 1,
            [FromQuery] IpAddressFilters? filters = null,
            CancellationToken cancellationToken = default)
        {
            filters ??= new IpAddressFilters();
            category = category is "suspicious" or "blocked" or "history" ? category : "all";
            var now = DateTime.UtcNow;
            var accountRiskAddresses = await _context.IpAddressSecurityStates.AsNoTracking()
                .Where(item => item.AccountRiskMarkedAtUtc != null &&
                               !item.IsPermanentlyBlocked &&
                               (item.BlockedUntilUtc == null || item.BlockedUntilUtc <= now))
                .Select(item => item.IpAddress)
                .ToListAsync(cancellationToken);
            foreach (var address in accountRiskAddresses)
                await _ipSecurityService.EvaluateAccumulatedAccountRiskAsync(address, cancellationToken);

            var query = _context.IpAddressSecurityStates.AsNoTracking();

            var activityStart = filters.Activity switch
            {
                "week" => now.AddDays(-7),
                "month" => now.AddDays(-30),
                "all" => DateTime.MinValue,
                _ => now.AddHours(-24)
            };
            query = query.Where(item => item.LastSeenAtUtc >= activityStart);

            if (!string.IsNullOrWhiteSpace(filters.Search))
            {
                var search = filters.Search.Trim();
                query = query.Where(item =>
                    item.IpAddress.Contains(search) ||
                    (item.LastUserLogin != null && item.LastUserLogin.Contains(search)) ||
                    (item.LastUserFullName != null && item.LastUserFullName.Contains(search)) ||
                    (item.LastPath != null && item.LastPath.Contains(search)));
            }

            var blockedExpression = (System.Linq.Expressions.Expression<Func<IpAddressSecurityState, bool>>)
                (item => item.IsPermanentlyBlocked || item.BlockedUntilUtc > now);
            if (category == "blocked" || filters.State == "blocked")
                query = query.Where(blockedExpression);
            else if (category == "suspicious" || filters.State == "suspicious")
                query = query.Where(item =>
                    !item.IsPermanentlyBlocked &&
                    (item.BlockedUntilUtc == null || item.BlockedUntilUtc <= now) &&
                    (item.SuspiciousAttemptCount > 0 || item.AccountRiskMarkedAtUtc != null));
            else if (filters.State == "allowed")
                query = query.Where(item =>
                    !item.IsPermanentlyBlocked &&
                    (item.BlockedUntilUtc == null || item.BlockedUntilUtc <= now) &&
                    item.SuspiciousAttemptCount == 0 && item.AccountRiskMarkedAtUtc == null);
            else if (category == "history")
                query = query.Where(item => item.BlockedAtUtc != null || item.UnblockedAtUtc != null);

            if (filters.EscalationLevel.HasValue)
                query = query.Where(item => item.EscalationLevel == filters.EscalationLevel.Value ||
                                            item.AccountRiskEscalationLevel == filters.EscalationLevel.Value);
            if (filters.Account == "authenticated")
                query = query.Where(item => item.LastUserId != null);
            else if (filters.Account == "anonymous")
                query = query.Where(item => item.LastUserId == null);

            query = query.OrderByDescending(item =>
                    item.IsPermanentlyBlocked || item.BlockedUntilUtc > now)
                .ThenByDescending(item => item.SuspiciousAttemptCount)
                .ThenByDescending(item => item.AccountRiskScore)
                .ThenByDescending(item => item.LastSeenAtUtc);
            var totalCount = await query.LongCountAsync(cancellationToken);
            var pagination = NormalizePagination(page, totalCount);
            var entries = await query
                .Skip((pagination.Page - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToListAsync(cancellationToken);

            return View(new AdministrationIpAddressesViewModel
            {
                ActiveSection = "ip-addresses",
                Navigation = await BuildNavigationAsync(true, cancellationToken),
                Entries = entries,
                Filters = filters,
                Pagination = pagination,
                Category = category,
                ActiveLastDayCount = await _context.IpAddressSecurityStates.LongCountAsync(
                    item => item.LastSeenAtUtc >= now.AddHours(-24), cancellationToken),
                SuspiciousCount = await _context.IpAddressSecurityStates.LongCountAsync(
                    item => (item.SuspiciousAttemptCount > 0 || item.AccountRiskMarkedAtUtc != null) &&
                            !item.IsPermanentlyBlocked &&
                            (item.BlockedUntilUtc == null || item.BlockedUntilUtc <= now),
                    cancellationToken),
                BlockedCount = await _context.IpAddressSecurityStates.LongCountAsync(
                    item => item.IsPermanentlyBlocked || item.BlockedUntilUtc > now,
                    cancellationToken),
                RejectedRequestCount = await _context.SystemRequestLogs.LongCountAsync(
                    item => item.OccurredAtUtc >= now.AddHours(-24) && item.StatusCode == StatusCodes.Status403Forbidden,
                    cancellationToken)
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AppRateLimit(AppRateLimitPolicies.AdminStructureMutation)]
        public async Task<IActionResult> BlockIpAddress(
            string ipAddress,
            string? reviewNote,
            CancellationToken cancellationToken)
        {
            var result = await _ipSecurityService.BlockManuallyAsync(
                ipAddress,
                GetCurrentUserId(),
                reviewNote,
                cancellationToken);
            TempData[result.Succeeded ? "AdministrationSuccess" : "AdministrationError"] =
                result.Succeeded ? $"IP-адрес {result.State!.IpAddress} заблокирован." : result.Error;
            return RedirectToAction(nameof(IpAddresses), new { category = "blocked" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AppRateLimit(AppRateLimitPolicies.AdminStructureMutation)]
        public async Task<IActionResult> UnblockIpAddress(
            string ipAddress,
            string? reviewNote,
            CancellationToken cancellationToken)
        {
            var result = await _ipSecurityService.UnblockAsync(
                ipAddress,
                GetCurrentUserId(),
                reviewNote,
                cancellationToken);
            TempData[result.Succeeded ? "AdministrationSuccess" : "AdministrationError"] =
                result.Succeeded ? $"IP-адрес {result.State!.IpAddress} разблокирован." : result.Error;
            return RedirectToAction(nameof(IpAddresses));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AppRateLimit(AppRateLimitPolicies.AdminStructureMutation)]
        public async Task<IActionResult> ClearIpSuspicion(
            string ipAddress,
            string? reviewNote,
            CancellationToken cancellationToken)
        {
            var result = await _ipSecurityService.ClearSuspicionAsync(
                ipAddress,
                GetCurrentUserId(),
                reviewNote,
                cancellationToken);
            TempData[result.Succeeded ? "AdministrationSuccess" : "AdministrationError"] =
                result.Succeeded
                    ? $"С IP-адреса {result.State!.IpAddress} снят статус подозрительного. Накопление начато заново."
                    : result.Error;
            return RedirectToAction(nameof(IpAddresses), new { category = "suspicious" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AppRateLimit(AppRateLimitPolicies.AdminStructureMutation)]
        public async Task<IActionResult> SetNetworkTrust(
            int userId,
            long networkId,
            bool isTrusted,
            CancellationToken cancellationToken = default)
        {
            var location = await _context.UserLoginLocations.SingleOrDefaultAsync(item =>
                item.Id == networkId && item.UserId == userId,
                cancellationToken);
            if (location == null)
            {
                if (await _context.UserLoginLocations.AsNoTracking()
                        .AnyAsync(item => item.Id == networkId, cancellationToken))
                {
                    _authorizationIncidents.Record(
                        "UserLoginLocation",
                        networkId,
                        $"изменение доверия через подменённый userId {userId}");
                }
                return NotFound();
            }

            var previous = location.IsTrusted;
            location.IsTrusted = isTrusted;
            _auditService.Record(
                GetCurrentUserId(),
                "UserLoginLocation",
                location.Id,
                isTrusted ? "NetworkTrusted" : "NetworkTrustRemoved",
                $"Для пользователя ID {userId} изменено доверие к сети " +
                $"{location.NetworkAddress}/{location.NetworkPrefixLength}.",
                new { IsTrusted = previous, location.UserId },
                new { location.IsTrusted, location.UserId });
            await _context.SaveChangesAsync(cancellationToken);

            TempData["AdministrationSuccess"] = isTrusted
                ? "Сеть отмечена как доверенная."
                : "Доверие к сети снято.";
            return RedirectToAction(nameof(UserNetworks), new { userId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AppRateLimit(AppRateLimitPolicies.AdminStructureMutation)]
        public async Task<IActionResult> ArchiveUserNetwork(
            int userId,
            long networkId,
            CancellationToken cancellationToken = default)
        {
            var location = await _context.UserLoginLocations.SingleOrDefaultAsync(item =>
                item.Id == networkId && item.UserId == userId,
                cancellationToken);
            if (location == null)
            {
                if (await _context.UserLoginLocations.AsNoTracking()
                        .AnyAsync(item => item.Id == networkId, cancellationToken))
                {
                    _authorizationIncidents.Record(
                        "UserLoginLocation",
                        networkId,
                        $"архивирование через подменённый userId {userId}");
                }
                return NotFound();
            }

            var previous = new { location.IsArchived, location.IsTrusted, location.UserId };
            location.IsArchived = true;
            location.IsTrusted = false;
            _auditService.Record(
                GetCurrentUserId(),
                "UserLoginLocation",
                location.Id,
                "NetworkArchived",
                $"Для пользователя ID {userId} архивирована сеть " +
                $"{location.NetworkAddress}/{location.NetworkPrefixLength}.",
                previous,
                new { location.IsArchived, location.IsTrusted, location.UserId });
            await _context.SaveChangesAsync(cancellationToken);

            TempData["AdministrationSuccess"] = "Запись сети архивирована.";
            return RedirectToAction(nameof(UserNetworks), new { userId });
        }

        [AppRateLimit(AppRateLimitPolicies.Search)]
        public async Task<IActionResult> Audit(
            int page = 1,
            string sort = "date",
            string direction = "desc",
            [FromQuery] AdministrationAuditFilters? filters = null,
            CancellationToken cancellationToken = default)
        {
            filters ??= new AdministrationAuditFilters();
            var query = ApplyAuditFilters(_context.AuditLogs.AsNoTracking(), filters);
            query = SortAuditLogs(query, sort, direction);
            var totalCount = await query.LongCountAsync(cancellationToken);
            var pagination = NormalizePagination(page, totalCount);
            var entries = await query
                .Skip((pagination.Page - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToListAsync(cancellationToken);

            return View(new AdministrationAuditViewModel
            {
                ActiveSection = "audit",
                Navigation = await BuildNavigationAsync(true, cancellationToken),
                Entries = entries,
                Filters = filters,
                Pagination = pagination,
                Sort = NormalizeAuditSort(sort),
                Direction = NormalizeDirection(direction)
            });
        }

        [AppRateLimit(AppRateLimitPolicies.FileDownload)]
        public async Task<IActionResult> ExportAudit(
            [FromQuery] AdministrationAuditFilters? filters = null,
            CancellationToken cancellationToken = default)
        {
            filters ??= new AdministrationAuditFilters();
            var entries = await ApplyAuditFilters(_context.AuditLogs.AsNoTracking(), filters)
                .OrderByDescending(log => log.CreatedAt)
                .Take(ExportLimit)
                .ToListAsync(cancellationToken);
            var csv = new StringBuilder("Время UTC;Пользователь;ID пользователя;Роль;IP;Объект;ID объекта;Действие;Описание;Trace ID;До;После\r\n");
            foreach (var entry in entries)
            {
                AppendCsvRow(csv,
                    entry.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    entry.UserFullName ?? entry.UserLogin ?? $"Пользователь #{entry.UserId}",
                    entry.UserId.ToString(),
                    entry.UserRole,
                    entry.IpAddress,
                    entry.EntityType,
                    entry.EntityId.ToString(),
                    entry.Action,
                    entry.Details,
                    entry.TraceId,
                    entry.PreviousValues,
                    entry.NewValues);
            }
            return CsvFile(csv, $"audit-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
        }

        [HttpPost]
        [AppRateLimit(AppRateLimitPolicies.AdminStructureMutation)]
        public async Task<IActionResult> CleanupLogs(
            int olderThanDays,
            bool confirm,
            CancellationToken cancellationToken)
        {
            if (!confirm)
            {
                TempData["AdministrationError"] = "Подтвердите удаление старых технических логов.";
                return RedirectToAction(nameof(Logs));
            }
            if (olderThanDays is < 30 or > 3650)
                return BadRequest("Срок хранения должен быть от 30 до 3650 дней.");

            var cutoff = DateTime.UtcNow.AddDays(-olderThanDays);
            var query = _context.SystemRequestLogs.Where(log => log.OccurredAtUtc < cutoff);
            var deletedCount = await query.LongCountAsync(cancellationToken);
            if (_context.Database.IsRelational())
                await query.ExecuteDeleteAsync(cancellationToken);
            else
            {
                _context.SystemRequestLogs.RemoveRange(await query.ToListAsync(cancellationToken));
                await _context.SaveChangesAsync(cancellationToken);
            }

            _auditService.Record(
                GetCurrentUserId(),
                "SystemRequestLog",
                0,
                "OldLogsDeleted",
                $"Удалено {deletedCount} технических логов старше {olderThanDays} дней.",
                new { OlderThanDays = olderThanDays, CutoffUtc = cutoff },
                new { DeletedCount = deletedCount });
            await _context.SaveChangesAsync(cancellationToken);
            TempData["AdministrationSuccess"] = $"Удалено записей: {deletedCount}.";
            return RedirectToAction(nameof(Logs));
        }

        private async Task<AdministrationNavigationViewModel> BuildNavigationAsync(
            bool serverAvailable,
            CancellationToken cancellationToken)
        {
            var storage = _storageHealthService.GetSidebarSnapshot();
            var today = ToUtcStart(DateOnly.FromDateTime(DateTime.Now));
            long logsToday = 0;
            long openSecurityEvents = 0;
            long blockedIpAddresses = 0;
            try
            {
                logsToday = await _context.SystemRequestLogs.LongCountAsync(log => log.OccurredAtUtc >= today, cancellationToken);
                openSecurityEvents = await _context.SecurityEventLogs.LongCountAsync(log =>
                    log.Severity != SecurityEventSeverities.Information &&
                    (log.Status == SecurityEventStatuses.New || log.Status == SecurityEventStatuses.Investigating),
                    cancellationToken);
                var now = DateTime.UtcNow;
                blockedIpAddresses = await _context.IpAddressSecurityStates.LongCountAsync(state =>
                    state.IsPermanentlyBlocked || state.BlockedUntilUtc > now,
                    cancellationToken);
            }
            catch
            {
                serverAvailable = false;
            }

            return new AdministrationNavigationViewModel
            {
                LogsToday = logsToday,
                OpenSecurityEvents = openSecurityEvents,
                BlockedIpAddresses = blockedIpAddresses,
                ServerAvailable = serverAvailable,
                StorageAvailable = storage.Available,
                StorageUsedPercent = storage.UsedSpacePercent,
                CheckedAtUtc = DateTime.UtcNow
            };
        }

        private static IQueryable<SystemRequestLog> ApplyRequestFilters(
            IQueryable<SystemRequestLog> query,
            SystemRequestLogFilters filters)
        {
            if (!string.IsNullOrWhiteSpace(filters.UserOrIp))
            {
                var value = filters.UserOrIp.Trim();
                var hasId = int.TryParse(value, out var userId);
                query = query.Where(log =>
                    (hasId && log.UserId == userId) ||
                    (log.UserLogin != null && log.UserLogin.Contains(value)) ||
                    (log.UserFullName != null && log.UserFullName.Contains(value)) ||
                    log.IpAddress.Contains(value) ||
                    log.TraceId.Contains(value));
            }
            if (!string.IsNullOrWhiteSpace(filters.EventType))
                query = query.Where(log => log.EventType == filters.EventType);
            if (!string.IsNullOrWhiteSpace(filters.Result))
                query = query.Where(log => log.Result == filters.Result);
            query = ApplyDateRange(query, filters.DateFrom, filters.DateTo, log => log.OccurredAtUtc);
            return query;
        }

        private static IQueryable<SecurityEventLog> ApplySecurityFilters(
            IQueryable<SecurityEventLog> query,
            SecurityEventFilters filters)
        {
            if (!string.IsNullOrWhiteSpace(filters.Search))
            {
                var value = filters.Search.Trim();
                var hasId = int.TryParse(value, out var userId);
                query = query.Where(log =>
                    (hasId && log.UserId == userId) ||
                    log.Title.Contains(value) ||
                    (log.Description != null && log.Description.Contains(value)) ||
                    (log.UserLogin != null && log.UserLogin.Contains(value)) ||
                    (log.UserFullName != null && log.UserFullName.Contains(value)) ||
                    log.IpAddress.Contains(value) ||
                    (log.NetworkAddress != null && log.NetworkAddress.Contains(value)) ||
                    (log.CountryName != null && log.CountryName.Contains(value)) ||
                    (log.CountryCode != null && log.CountryCode.Contains(value)) ||
                    (log.TraceId != null && log.TraceId.Contains(value)));
            }
            if (!string.IsNullOrWhiteSpace(filters.EventType))
                query = query.Where(log => log.EventType == filters.EventType);
            if (!string.IsNullOrWhiteSpace(filters.Severity))
                query = query.Where(log => log.Severity == filters.Severity);
            if (!string.IsNullOrWhiteSpace(filters.Status))
                query = query.Where(log => log.Status == filters.Status);
            query = ApplyDateRange(query, filters.DateFrom, filters.DateTo, log => log.LastOccurredAtUtc);
            return query;
        }

        private static IQueryable<AuditLog> ApplyAuditFilters(
            IQueryable<AuditLog> query,
            AdministrationAuditFilters filters)
        {
            if (!string.IsNullOrWhiteSpace(filters.User))
            {
                var value = filters.User.Trim();
                var hasId = int.TryParse(value, out var userId);
                query = query.Where(log =>
                    (hasId && log.UserId == userId) ||
                    (log.UserFullName != null && log.UserFullName.Contains(value)) ||
                    (log.UserLogin != null && log.UserLogin.Contains(value)));
            }
            if (!string.IsNullOrWhiteSpace(filters.Entity))
            {
                var value = filters.Entity.Trim();
                var hasId = long.TryParse(value, out var entityId);
                query = query.Where(log => (hasId && log.EntityId == entityId) || log.EntityType.Contains(value));
            }
            if (!string.IsNullOrWhiteSpace(filters.Action))
            {
                var value = filters.Action.Trim();
                query = query.Where(log => log.Action.Contains(value) || log.Details.Contains(value));
            }
            if (!string.IsNullOrWhiteSpace(filters.IpOrTrace))
            {
                var value = filters.IpOrTrace.Trim();
                query = query.Where(log =>
                    (log.IpAddress != null && log.IpAddress.Contains(value)) ||
                    (log.TraceId != null && log.TraceId.Contains(value)));
            }
            query = ApplyDateRange(query, filters.DateFrom, filters.DateTo, log => log.CreatedAt);
            return query;
        }

        private static IQueryable<T> ApplyDateRange<T>(
            IQueryable<T> query,
            DateOnly? dateFrom,
            DateOnly? dateTo,
            System.Linq.Expressions.Expression<Func<T, DateTime>> selector)
        {
            if (dateFrom.HasValue)
            {
                var from = ToUtcStart(dateFrom.Value);
                query = query.Where(BuildComparison(selector, from, greaterThanOrEqual: true));
            }
            if (dateTo.HasValue)
            {
                var to = ToUtcStart(dateTo.Value.AddDays(1));
                query = query.Where(BuildComparison(selector, to, greaterThanOrEqual: false));
            }
            return query;
        }

        private static System.Linq.Expressions.Expression<Func<T, bool>> BuildComparison<T>(
            System.Linq.Expressions.Expression<Func<T, DateTime>> selector,
            DateTime value,
            bool greaterThanOrEqual)
        {
            var comparison = greaterThanOrEqual
                ? System.Linq.Expressions.Expression.GreaterThanOrEqual(selector.Body, System.Linq.Expressions.Expression.Constant(value))
                : System.Linq.Expressions.Expression.LessThan(selector.Body, System.Linq.Expressions.Expression.Constant(value));
            return System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(comparison, selector.Parameters);
        }

        private static DateTime ToUtcStart(DateOnly localDate)
        {
            var localStart = DateTime.SpecifyKind(localDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTimeToUtc(localStart, TimeZoneInfo.Local);
        }

        private static IQueryable<SystemRequestLog> SortRequestLogs(IQueryable<SystemRequestLog> query, string sort, string direction)
        {
            var descending = NormalizeDirection(direction) == "desc";
            return NormalizeRequestSort(sort) switch
            {
                "user" => descending ? query.OrderByDescending(log => log.UserFullName).ThenByDescending(log => log.OccurredAtUtc) : query.OrderBy(log => log.UserFullName).ThenBy(log => log.OccurredAtUtc),
                "ip" => descending ? query.OrderByDescending(log => log.IpAddress).ThenByDescending(log => log.OccurredAtUtc) : query.OrderBy(log => log.IpAddress).ThenBy(log => log.OccurredAtUtc),
                "action" => descending ? query.OrderByDescending(log => log.Controller).ThenByDescending(log => log.Action) : query.OrderBy(log => log.Controller).ThenBy(log => log.Action),
                "status" => descending ? query.OrderByDescending(log => log.StatusCode).ThenByDescending(log => log.OccurredAtUtc) : query.OrderBy(log => log.StatusCode).ThenBy(log => log.OccurredAtUtc),
                "duration" => descending ? query.OrderByDescending(log => log.DurationMs).ThenByDescending(log => log.OccurredAtUtc) : query.OrderBy(log => log.DurationMs).ThenBy(log => log.OccurredAtUtc),
                _ => descending ? query.OrderByDescending(log => log.OccurredAtUtc) : query.OrderBy(log => log.OccurredAtUtc)
            };
        }

        private static IQueryable<SecurityEventLog> SortSecurityEvents(IQueryable<SecurityEventLog> query, string sort, string direction)
        {
            var descending = NormalizeDirection(direction) == "desc";
            return NormalizeSecuritySort(sort) switch
            {
                "severity" => descending
                    ? query.OrderByDescending(log =>
                        log.Severity == SecurityEventSeverities.Critical ? 4 :
                        log.Severity == SecurityEventSeverities.High ? 3 :
                        log.Severity == SecurityEventSeverities.Warning ? 2 : 1)
                        .ThenByDescending(log => log.LastOccurredAtUtc)
                    : query.OrderBy(log =>
                        log.Severity == SecurityEventSeverities.Information ? 1 :
                        log.Severity == SecurityEventSeverities.Warning ? 2 :
                        log.Severity == SecurityEventSeverities.High ? 3 : 4)
                        .ThenBy(log => log.LastOccurredAtUtc),
                "type" => descending ? query.OrderByDescending(log => log.EventType).ThenByDescending(log => log.LastOccurredAtUtc) : query.OrderBy(log => log.EventType).ThenBy(log => log.LastOccurredAtUtc),
                "user" => descending ? query.OrderByDescending(log => log.UserFullName).ThenByDescending(log => log.LastOccurredAtUtc) : query.OrderBy(log => log.UserFullName).ThenBy(log => log.LastOccurredAtUtc),
                "count" => descending ? query.OrderByDescending(log => log.OccurrenceCount).ThenByDescending(log => log.LastOccurredAtUtc) : query.OrderBy(log => log.OccurrenceCount).ThenBy(log => log.LastOccurredAtUtc),
                "status" => descending ? query.OrderByDescending(log => log.Status).ThenByDescending(log => log.LastOccurredAtUtc) : query.OrderBy(log => log.Status).ThenBy(log => log.LastOccurredAtUtc),
                _ => descending ? query.OrderByDescending(log => log.LastOccurredAtUtc) : query.OrderBy(log => log.LastOccurredAtUtc)
            };
        }

        private static IQueryable<UserLoginLocation> SortUserNetworks(
            IQueryable<UserLoginLocation> query,
            string sort,
            string direction)
        {
            var descending = NormalizeDirection(direction) == "desc";
            return NormalizeNetworkSort(sort) switch
            {
                "network" => descending
                    ? query.OrderByDescending(item => item.NetworkAddress).ThenByDescending(item => item.NetworkPrefixLength)
                    : query.OrderBy(item => item.NetworkAddress).ThenBy(item => item.NetworkPrefixLength),
                "country" => descending
                    ? query.OrderByDescending(item => item.CountryName).ThenByDescending(item => item.LastSeenAtUtc)
                    : query.OrderBy(item => item.CountryName).ThenBy(item => item.LastSeenAtUtc),
                "firstSeen" => descending
                    ? query.OrderByDescending(item => item.FirstSeenAtUtc)
                    : query.OrderBy(item => item.FirstSeenAtUtc),
                "count" => descending
                    ? query.OrderByDescending(item => item.SuccessfulLoginCount).ThenByDescending(item => item.LastSeenAtUtc)
                    : query.OrderBy(item => item.SuccessfulLoginCount).ThenBy(item => item.LastSeenAtUtc),
                _ => descending
                    ? query.OrderByDescending(item => item.LastSeenAtUtc)
                    : query.OrderBy(item => item.LastSeenAtUtc)
            };
        }

        private static IQueryable<AuditLog> SortAuditLogs(IQueryable<AuditLog> query, string sort, string direction)
        {
            var descending = NormalizeDirection(direction) == "desc";
            return NormalizeAuditSort(sort) switch
            {
                "user" => descending ? query.OrderByDescending(log => log.UserFullName).ThenByDescending(log => log.CreatedAt) : query.OrderBy(log => log.UserFullName).ThenBy(log => log.CreatedAt),
                "entity" => descending ? query.OrderByDescending(log => log.EntityType).ThenByDescending(log => log.EntityId) : query.OrderBy(log => log.EntityType).ThenBy(log => log.EntityId),
                "action" => descending ? query.OrderByDescending(log => log.Action).ThenByDescending(log => log.CreatedAt) : query.OrderBy(log => log.Action).ThenBy(log => log.CreatedAt),
                "ip" => descending ? query.OrderByDescending(log => log.IpAddress).ThenByDescending(log => log.CreatedAt) : query.OrderBy(log => log.IpAddress).ThenBy(log => log.CreatedAt),
                _ => descending ? query.OrderByDescending(log => log.CreatedAt) : query.OrderBy(log => log.CreatedAt)
            };
        }

        private static AdministrationPagination NormalizePagination(int page, long totalCount)
        {
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)PageSize));
            return new AdministrationPagination(Math.Clamp(page, 1, totalPages), PageSize, totalCount);
        }

        private static string NormalizeDirection(string? direction) =>
            string.Equals(direction, "asc", StringComparison.OrdinalIgnoreCase) ? "asc" : "desc";

        private static string NormalizeRequestSort(string? sort) =>
            sort is "user" or "ip" or "action" or "status" or "duration" ? sort : "date";

        private static string NormalizeSecuritySort(string? sort) =>
            sort is "severity" or "type" or "user" or "count" or "status" ? sort : "date";

        private static string NormalizeNetworkSort(string? sort) =>
            sort is "network" or "country" or "firstSeen" or "count" ? sort : "lastSeen";

        private static string NormalizeAuditSort(string? sort) =>
            sort is "user" or "entity" or "action" or "ip" ? sort : "date";

        private int GetCurrentUserId() =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        private FileContentResult CsvFile(StringBuilder csv, string fileName)
        {
            var preamble = Encoding.UTF8.GetPreamble();
            var body = Encoding.UTF8.GetBytes(csv.ToString());
            var bytes = new byte[preamble.Length + body.Length];
            Buffer.BlockCopy(preamble, 0, bytes, 0, preamble.Length);
            Buffer.BlockCopy(body, 0, bytes, preamble.Length, body.Length);
            return File(bytes, "text/csv; charset=utf-8", fileName);
        }

        private static void AppendCsvRow(StringBuilder builder, params string?[] values)
        {
            builder.AppendLine(string.Join(';', values.Select(EscapeCsv)));
        }

        private static string EscapeCsv(string? value)
        {
            value ??= string.Empty;
            if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@')
                value = "'" + value;
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
    }
}
