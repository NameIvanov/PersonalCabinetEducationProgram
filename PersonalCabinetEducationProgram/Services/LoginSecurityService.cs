using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PersonalCabinetEducationProgram.Data;
using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Services;

public sealed record LoginSecurityResult(bool AccountBlocked);

public sealed class LoginSecurityService
{
    public const string SessionIdClaimType = "LoginSessionId";

    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IIpNetworkService _ipNetworkService;
    private readonly IIpGeolocationService _ipGeolocationService;
    private readonly NotificationService _notifications;
    private readonly AuditService _auditService;
    private readonly AccountSecurityService _accountSecurity;
    private readonly UserManager<User> _userManager;
    private readonly SecurityMonitoringOptions _options;
    private readonly TimeProvider _timeProvider;

    public LoginSecurityService(
        ApplicationDbContext context,
        IHttpContextAccessor httpContextAccessor,
        IIpNetworkService ipNetworkService,
        IIpGeolocationService ipGeolocationService,
        NotificationService notifications,
        AuditService auditService,
        AccountSecurityService accountSecurity,
        UserManager<User> userManager,
        IOptions<SecurityMonitoringOptions> options,
        TimeProvider timeProvider)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _ipNetworkService = ipNetworkService;
        _ipGeolocationService = ipGeolocationService;
        _notifications = notifications;
        _auditService = auditService;
        _accountSecurity = accountSecurity;
        _userManager = userManager;
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public async Task RecordFailedLoginAsync(
        User? user,
        string attemptedLogin,
        bool locked,
        CancellationToken cancellationToken = default)
    {
        var now = UtcNow;
        var network = GetCurrentNetwork();
        _context.SecurityEventLogs.Add(CreateEvent(
            locked ? SecurityEventTypes.AccountLocked : SecurityEventTypes.LoginFailed,
            locked ? SecurityEventSeverities.High : SecurityEventSeverities.Warning,
            locked ? "Учётная запись заблокирована" : "Неудачная попытка входа",
            locked
                ? "Превышено допустимое количество неудачных попыток входа."
                : user == null ? "Пользователь с указанным логином не найден." : "Указан неверный пароль.",
            now,
            user?.Id,
            user?.UserName ?? attemptedLogin,
            user?.FullName,
            network));
        await _context.SaveChangesAsync(cancellationToken);
        if (locked && user != null)
            await _accountSecurity.EvaluateAccumulatedRiskAsync(user.Id, cancellationToken);
    }

    public async Task<LoginSecurityResult> RecordSuccessfulLoginAsync(
        User user,
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        var now = UtcNow;
        var network = GetCurrentNetwork();

        _auditService.Record(
            user.Id,
            "User",
            user.Id,
            "LoginSucceeded",
            network == null
                ? "Успешный вход; IP-адрес не определён."
                : $"Успешный вход с IP {network.IpAddress}, сеть {network.Cidr}.",
            userLogin: user.UserName,
            userFullName: user.FullName,
            ipAddress: network?.IpAddress);

        if (network == null)
        {
            _context.SecurityEventLogs.Add(CreateEvent(
                SecurityEventTypes.LoginSucceeded,
                SecurityEventSeverities.Information,
                "Успешный вход",
                "Успешный вход; IP-адрес не определён.",
                now,
                user.Id,
                user.UserName,
                user.FullName,
                null));
            await _context.SaveChangesAsync(cancellationToken);
            return new LoginSecurityResult(false);
        }

        var hadHistory = await _context.UserLoginLocations
            .AnyAsync(location => location.UserId == user.Id, cancellationToken);
        var location = await _context.UserLoginLocations
            .SingleOrDefaultAsync(location =>
                location.UserId == user.Id &&
                location.NetworkAddress == network.NetworkAddress &&
                location.NetworkPrefixLength == network.PrefixLength,
                cancellationToken);
        var isNewNetwork = location == null || location.IsArchived;
        var previousLocation = await _context.UserLoginLocations
            .AsNoTracking()
            .Where(item => item.UserId == user.Id &&
                           !item.IsArchived &&
                           (location == null || item.Id != location.Id))
            .OrderByDescending(item => item.LastSeenAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        IpCountryLookup lookup;
        try
        {
            lookup = await _ipGeolocationService.LookupAsync(network.Address, cancellationToken);
        }
        catch when (!cancellationToken.IsCancellationRequested)
        {
            lookup = network.IsLocal ? IpCountryLookup.LocalOrReserved : IpCountryLookup.UnknownPublic;
        }

        var effectiveCountryCode = lookup.WasResolved ? lookup.CountryCode : location?.CountryCode;
        var effectiveCountryName = lookup.WasResolved ? lookup.CountryName : location?.CountryName;
        var effectiveLatitude = lookup.WasResolved ? lookup.Latitude : location?.Latitude;
        var effectiveLongitude = lookup.WasResolved ? lookup.Longitude : location?.Longitude;

        if (location == null)
        {
            location = new UserLoginLocation
            {
                UserId = user.Id,
                NetworkAddress = network.NetworkAddress,
                NetworkPrefixLength = network.PrefixLength,
                FirstSeenAtUtc = now
            };
            _context.UserLoginLocations.Add(location);
        }

        location.IpAddress = network.IpAddress;
        location.CountryCode = effectiveCountryCode;
        location.CountryName = effectiveCountryName;
        location.Latitude = effectiveLatitude;
        location.Longitude = effectiveLongitude;
        location.IsLocal = network.IsLocal;
        location.LastSeenAtUtc = now;
        location.SuccessfulLoginCount = Math.Max(0, location.SuccessfulLoginCount) + 1;
        location.IsArchived = false;

        _context.SecurityEventLogs.Add(CreateEvent(
            SecurityEventTypes.LoginSucceeded,
            SecurityEventSeverities.Information,
            "Успешный вход",
            BuildLoginDescription(
                user,
                network,
                network.IsLocal
                    ? "локальная сеть"
                    : FormatCountry(effectiveCountryCode, effectiveCountryName),
                now),
            now,
            user.Id,
            user.UserName,
            user.FullName,
            network,
            effectiveCountryCode,
            effectiveCountryName));

        var isAdministrator = await _userManager.IsInRoleAsync(user, AppRoles.Admin);
        var isForeign = !network.IsLocal && lookup.WasResolved &&
                        !string.Equals(
                            effectiveCountryCode,
                            _options.IpGeolocation.AllowedCountryCode,
                            StringComparison.OrdinalIgnoreCase);

        if (hadHistory)
        {
            if (isNewNetwork)
            {
                await RecordNewNetworkSignalsAsync(
                    user,
                    location,
                    network,
                    lookup,
                    isForeign,
                    isAdministrator,
                    now,
                    cancellationToken);
            }

            await RecordImpossibleTravelAsync(
                user,
                previousLocation,
                location,
                network,
                now,
                cancellationToken);
            await RecordConcurrentSessionAsync(
                user,
                network,
                effectiveCountryCode,
                now,
                cancellationToken);
        }

        await ArchiveInactiveSessionsAsync(user.Id, now, cancellationToken);
        _context.UserLoginSessions.Add(new UserLoginSession
        {
            SessionId = sessionId,
            UserId = user.Id,
            IpAddress = network.IpAddress,
            NetworkAddress = network.NetworkAddress,
            NetworkPrefixLength = network.PrefixLength,
            CountryCode = effectiveCountryCode,
            IsLocal = network.IsLocal,
            CreatedAtUtc = now,
            LastActivityAtUtc = now,
            IsActive = true
        });

        await _context.SaveChangesAsync(cancellationToken);

        await _accountSecurity.EvaluateAccumulatedRiskAsync(user.Id, cancellationToken);
        if (user.SecurityBlockedAtUtc.HasValue)
            return new LoginSecurityResult(true);

        if (!hadHistory || !isNewNetwork || !isForeign || !_options.BlockNewForeignLogin)
            return new LoginSecurityResult(false);

        if (!isAdministrator)
        {
            var activeSessions = await _context.UserLoginSessions
                .Where(item => item.UserId == user.Id && item.IsActive)
                .ToListAsync(cancellationToken);
            foreach (var activeSession in activeSessions)
            {
                activeSession.IsActive = false;
                activeSession.EndedAtUtc = now;
            }
        }

        var blocked = await _accountSecurity.BlockForLoginRiskAsync(
            user,
            $"Вход из новой иностранной сети {network.Cidr}, " +
            $"страна {FormatCountry(effectiveCountryCode, effectiveCountryName)}.",
            cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return new LoginSecurityResult(blocked);
    }

    public async Task EndSessionAsync(string? sessionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return;

        var session = await _context.UserLoginSessions
            .SingleOrDefaultAsync(item => item.SessionId == sessionId, cancellationToken);
        if (session == null || !session.IsActive)
            return;

        session.IsActive = false;
        session.EndedAtUtc = UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task RecordNewNetworkSignalsAsync(
        User user,
        UserLoginLocation location,
        IpNetworkInfo network,
        IpCountryLookup lookup,
        bool isForeign,
        bool isAdministrator,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var country = network.IsLocal
            ? "локальная сеть"
            : FormatCountry(lookup.CountryCode, lookup.CountryName);
        var description = BuildLoginDescription(user, network, country, now);

        if (!location.IsTrusted)
        {
            AddEvent(
                isForeign ? SecurityEventTypes.ForeignLogin : SecurityEventTypes.NewLoginNetwork,
                isForeign && _options.BlockNewForeignLogin && isAdministrator
                    ? SecurityEventSeverities.Critical
                    : isForeign ? SecurityEventSeverities.High : SecurityEventSeverities.Warning,
                isForeign ? "Вход из новой иностранной сети" : "Вход из новой сети",
                description,
                now,
                user,
                network,
                lookup.CountryCode,
                lookup.CountryName);
        }
        else if (isForeign)
        {
            AddEvent(
                SecurityEventTypes.ForeignLogin,
                SecurityEventSeverities.High,
                "Вход из доверенной иностранной сети",
                description,
                now,
                user,
                network,
                lookup.CountryCode,
                lookup.CountryName);
        }

        if (!network.IsLocal && !lookup.WasResolved)
        {
            AddEvent(
                SecurityEventTypes.LoginCountryUnknown,
                SecurityEventSeverities.Information,
                "Не удалось определить страну входа",
                BuildLoginDescription(user, network, "страна не определена", now),
                now,
                user,
                network);
        }

        var notificationCountry = network.IsLocal ? "локальная сеть" : country;
        _notifications.CreateSecurity(
            user.Id,
            "Вход из новой сети",
            $"Выполнен вход в вашу учётную запись из новой сети: {network.IpAddress}, " +
            $"{notificationCountry}, {now:dd.MM.yyyy HH:mm} UTC. " +
            "Если это были не вы, обратитесь к администратору.");

        if (isForeign)
        {
            await _notifications.CreateSecurityForAdministratorsAsync(
                "Вход из новой иностранной сети",
                $"Пользователь {user.FullName} ({user.UserName}, ID {user.Id}) вошёл с IP " +
                $"{network.IpAddress}, сеть {network.Cidr}, страна {country}, {now:dd.MM.yyyy HH:mm} UTC.",
                user.Id,
                cancellationToken);
        }

        var frequentWindow = now.AddHours(-Math.Max(1, _options.NewNetworksWindowHours));
        var recentNewNetworks = await _context.UserLoginLocations.CountAsync(item =>
            item.UserId == user.Id && item.FirstSeenAtUtc >= frequentWindow,
            cancellationToken);
        var baselineFirstSeen = await _context.UserLoginLocations
            .Where(item => item.UserId == user.Id)
            .OrderBy(item => item.FirstSeenAtUtc)
            .Select(item => (DateTime?)item.FirstSeenAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (baselineFirstSeen >= frequentWindow)
            recentNewNetworks = Math.Max(0, recentNewNetworks - 1);
        if (location.Id == 0)
            recentNewNetworks++;
        if (recentNewNetworks >= Math.Max(1, _options.NewNetworksWarningCount) &&
            !await HasRecentEventAsync(
                user.Id,
                SecurityEventTypes.FrequentNetworkChanges,
                frequentWindow,
                cancellationToken))
        {
            AddEvent(
                SecurityEventTypes.FrequentNetworkChanges,
                SecurityEventSeverities.High,
                "Частая смена сетей входа",
                $"Пользователь вошёл из {recentNewNetworks} новых сетей за " +
                $"{Math.Max(1, _options.NewNetworksWindowHours)} ч. Последняя сеть: {network.Cidr}.",
                now,
                user,
                network,
                lookup.CountryCode,
                lookup.CountryName);
        }

        var failedWindow = now.AddMinutes(-Math.Max(1, _options.FailedLoginCorrelationMinutes));
        var failedCount = await _context.SecurityEventLogs.CountAsync(item =>
            (item.EventType == SecurityEventTypes.LoginFailed || item.EventType == SecurityEventTypes.AccountLocked) &&
            item.LastOccurredAtUtc >= failedWindow &&
            item.IpAddress == network.IpAddress &&
            (item.UserId == user.Id || item.UserLogin == user.UserName),
            cancellationToken);
        if (failedCount >= Math.Max(1, _options.FailedLoginCorrelationCount))
        {
            AddEvent(
                SecurityEventTypes.NewNetworkAfterFailedLogins,
                SecurityEventSeverities.High,
                "Новая сеть после неудачных входов",
                $"Успешному входу из новой сети предшествовало {failedCount} неудачных попыток " +
                $"за {Math.Max(1, _options.FailedLoginCorrelationMinutes)} мин.",
                now,
                user,
                network,
                lookup.CountryCode,
                lookup.CountryName);
        }
    }

    private async Task RecordImpossibleTravelAsync(
        User user,
        UserLoginLocation? previous,
        UserLoginLocation current,
        IpNetworkInfo network,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (network.IsLocal || previous == null || previous.IsLocal ||
            !previous.Latitude.HasValue || !previous.Longitude.HasValue ||
            !current.Latitude.HasValue || !current.Longitude.HasValue)
        {
            return;
        }

        var elapsed = now - previous.LastSeenAtUtc;
        if (elapsed <= TimeSpan.Zero)
            return;

        var distance = CalculateDistanceKm(
            previous.Latitude.Value,
            previous.Longitude.Value,
            current.Latitude.Value,
            current.Longitude.Value);
        var speed = distance / elapsed.TotalHours;
        if (distance <= _options.ImpossibleTravelMinDistanceKm ||
            speed <= _options.ImpossibleTravelMaxSpeedKmh)
        {
            return;
        }

        AddEvent(
            SecurityEventTypes.ImpossibleTravel,
            SecurityEventSeverities.High,
            "Невозможное перемещение между входами",
            $"Предыдущий вход: {FormatCountry(previous.CountryCode, previous.CountryName)}, " +
            $"IP {previous.IpAddress}; текущий вход: " +
            $"{FormatCountry(current.CountryCode, current.CountryName)}, IP {network.IpAddress}; " +
            $"расстояние {distance:0} км, интервал {elapsed.TotalMinutes:0} мин., " +
            $"расчётная скорость {speed:0} км/ч.",
            now,
            user,
            network,
            current.CountryCode,
            current.CountryName);
        await Task.CompletedTask;
    }

    private async Task RecordConcurrentSessionAsync(
        User user,
        IpNetworkInfo network,
        string? countryCode,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (network.IsLocal)
            return;

        var windowStart = now.AddMinutes(-Math.Max(1, _options.ConcurrentSessionWindowMinutes));
        var other = await _context.UserLoginSessions
            .AsNoTracking()
            .Where(item => item.UserId == user.Id && item.IsActive && !item.IsLocal &&
                           item.LastActivityAtUtc >= windowStart &&
                           (item.NetworkAddress != network.NetworkAddress ||
                            item.NetworkPrefixLength != network.PrefixLength))
            .OrderByDescending(item => item.LastActivityAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (other == null || await HasRecentEventAsync(
                user.Id,
                SecurityEventTypes.ConcurrentForeignSessions,
                windowStart,
                cancellationToken))
        {
            return;
        }

        var differentKnownCountries = !string.IsNullOrWhiteSpace(countryCode) &&
                                      !string.IsNullOrWhiteSpace(other.CountryCode) &&
                                      !string.Equals(countryCode, other.CountryCode, StringComparison.OrdinalIgnoreCase);
        AddEvent(
            SecurityEventTypes.ConcurrentForeignSessions,
            differentKnownCountries ? SecurityEventSeverities.High : SecurityEventSeverities.Warning,
            differentKnownCountries
                ? "Одновременная активность из разных стран"
                : "Одновременная активность из разных сетей",
            $"За {Math.Max(1, _options.ConcurrentSessionWindowMinutes)} мин. обнаружены активные сети " +
            $"{other.NetworkAddress}/{other.NetworkPrefixLength} ({other.CountryCode ?? "страна не определена"}) " +
            $"и {network.Cidr} ({countryCode ?? "страна не определена"}).",
            now,
            user,
            network,
            countryCode);
    }

    private async Task ArchiveInactiveSessionsAsync(
        int userId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var cutoff = now.AddHours(-Math.Max(1, _options.SessionInactiveHours));
        var staleSessions = await _context.UserLoginSessions
            .Where(item => item.UserId == userId && item.IsActive && item.LastActivityAtUtc < cutoff)
            .ToListAsync(cancellationToken);
        foreach (var session in staleSessions)
        {
            session.IsActive = false;
            session.EndedAtUtc = now;
        }
    }

    private async Task<bool> HasRecentEventAsync(
        int userId,
        string eventType,
        DateTime since,
        CancellationToken cancellationToken)
    {
        if (_context.SecurityEventLogs.Local.Any(item =>
                item.UserId == userId && item.EventType == eventType && item.LastOccurredAtUtc >= since))
        {
            return true;
        }

        return await _context.SecurityEventLogs.AnyAsync(item =>
            item.UserId == userId && item.EventType == eventType && item.LastOccurredAtUtc >= since,
            cancellationToken);
    }

    private void AddEvent(
        string eventType,
        string severity,
        string title,
        string description,
        DateTime now,
        User user,
        IpNetworkInfo network,
        string? countryCode = null,
        string? countryName = null)
    {
        _context.SecurityEventLogs.Add(CreateEvent(
            eventType,
            severity,
            title,
            description,
            now,
            user.Id,
            user.UserName,
            user.FullName,
            network,
            countryCode,
            countryName));
    }

    private SecurityEventLog CreateEvent(
        string eventType,
        string severity,
        string title,
        string description,
        DateTime now,
        int? userId,
        string? userLogin,
        string? userFullName,
        IpNetworkInfo? network,
        string? countryCode = null,
        string? countryName = null)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var isInformation = severity == SecurityEventSeverities.Information;
        return new SecurityEventLog
        {
            FirstOccurredAtUtc = now,
            LastOccurredAtUtc = now,
            Severity = severity,
            EventType = eventType,
            Title = title,
            Description = description,
            UserId = userId,
            UserLogin = userLogin,
            UserFullName = userFullName,
            IpAddress = network?.IpAddress ??
                        httpContext?.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            NetworkAddress = network?.NetworkAddress,
            NetworkPrefixLength = network?.PrefixLength,
            CountryCode = countryCode,
            CountryName = countryName,
            HttpMethod = httpContext?.Request.Method,
            Path = httpContext?.Request.Path.Value,
            TraceId = httpContext?.TraceIdentifier,
            OccurrenceCount = 1,
            Status = isInformation ? SecurityEventStatuses.Resolved : SecurityEventStatuses.New,
            ReviewedAtUtc = isInformation ? now : null,
            ReviewNote = isInformation ? "Информационное событие обработано автоматически." : null
        };
    }

    private IpNetworkInfo? GetCurrentNetwork() =>
        _ipNetworkService.TryGetNetwork(
            _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress,
            out var network)
            ? network
            : null;

    private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

    private static string BuildLoginDescription(
        User user,
        IpNetworkInfo network,
        string country,
        DateTime now) =>
        $"Пользователь: {user.FullName}; логин: {user.UserName}; ID: {user.Id}; " +
        $"IP: {network.IpAddress}; сеть: {network.Cidr}; страна: {country}; " +
        $"время: {now:dd.MM.yyyy HH:mm:ss} UTC.";

    private static string FormatCountry(string? countryCode, string? countryName)
    {
        if (string.IsNullOrWhiteSpace(countryName))
            return string.IsNullOrWhiteSpace(countryCode) ? "страна не определена" : countryCode;
        return string.IsNullOrWhiteSpace(countryCode) ? countryName : $"{countryName} ({countryCode})";
    }

    public static double CalculateDistanceKm(
        double firstLatitude,
        double firstLongitude,
        double secondLatitude,
        double secondLongitude)
    {
        const double earthRadiusKm = 6371.0088;
        var latitudeDelta = DegreesToRadians(secondLatitude - firstLatitude);
        var longitudeDelta = DegreesToRadians(secondLongitude - firstLongitude);
        var firstLatitudeRadians = DegreesToRadians(firstLatitude);
        var secondLatitudeRadians = DegreesToRadians(secondLatitude);
        var haversine = Math.Pow(Math.Sin(latitudeDelta / 2), 2) +
                        Math.Cos(firstLatitudeRadians) * Math.Cos(secondLatitudeRadians) *
                        Math.Pow(Math.Sin(longitudeDelta / 2), 2);
        return 2 * earthRadiusKm * Math.Asin(Math.Sqrt(Math.Min(1, haversine)));
    }

    private static double DegreesToRadians(double value) => value * Math.PI / 180;
}
