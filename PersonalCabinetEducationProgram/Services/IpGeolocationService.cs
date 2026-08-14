using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace PersonalCabinetEducationProgram.Services
{
    public sealed record IpCountryLookup(
        bool IsPublicAddress,
        bool WasResolved,
        string? CountryCode,
        string? CountryName,
        double? Latitude = null,
        double? Longitude = null)
    {
        public static IpCountryLookup LocalOrReserved { get; } = new(false, false, null, null);
        public static IpCountryLookup UnknownPublic { get; } = new(true, false, null, null);
    }

    public interface IIpGeolocationService
    {
        Task<IpCountryLookup> LookupAsync(IPAddress? ipAddress, CancellationToken cancellationToken = default);
    }

    public sealed class IpGeolocationService : IIpGeolocationService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly IpGeolocationOptions _options;
        private readonly ILogger<IpGeolocationService> _logger;

        public IpGeolocationService(
            HttpClient httpClient,
            IMemoryCache cache,
            IOptions<SecurityMonitoringOptions> options,
            ILogger<IpGeolocationService> logger)
        {
            _httpClient = httpClient;
            _cache = cache;
            _options = options.Value.IpGeolocation;
            _logger = logger;
        }

        public async Task<IpCountryLookup> LookupAsync(
            IPAddress? ipAddress,
            CancellationToken cancellationToken = default)
        {
            if (!_options.Enabled || ipAddress == null || !IsPublicAddress(ipAddress))
                return IpCountryLookup.LocalOrReserved;

            var normalizedAddress = ipAddress.IsIPv4MappedToIPv6 ? ipAddress.MapToIPv4() : ipAddress;
            var cacheKey = $"ip-country:{normalizedAddress}";
            if (_cache.TryGetValue(cacheKey, out IpCountryLookup? cached) && cached != null)
                return cached;

            IpCountryLookup result;
            try
            {
                var endpoint = string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    _options.EndpointTemplate,
                    Uri.EscapeDataString(normalizedAddress.ToString()));
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromMilliseconds(Math.Clamp(_options.TimeoutMilliseconds, 250, 10000)));
                using var response = await _httpClient.GetAsync(endpoint, timeout.Token);
                if (!response.IsSuccessStatusCode)
                {
                    result = IpCountryLookup.UnknownPublic;
                }
                else
                {
                    await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
                    using var document = await JsonDocument.ParseAsync(stream, cancellationToken: timeout.Token);
                    var root = document.RootElement;
                    var success = !root.TryGetProperty("success", out var successValue) || successValue.GetBoolean();
                    var countryCode = ReadString(root, "country_code")?.ToUpperInvariant();
                    var countryName = ReadString(root, "country");
                    var latitude = ReadDouble(root, "latitude");
                    var longitude = ReadDouble(root, "longitude");
                    result = success && !string.IsNullOrWhiteSpace(countryCode)
                        ? new IpCountryLookup(true, true, countryCode, countryName, latitude, longitude)
                        : IpCountryLookup.UnknownPublic;
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                result = IpCountryLookup.UnknownPublic;
                _logger.LogWarning("IP geolocation timed out for {IpAddress}.", normalizedAddress);
            }
            catch (Exception exception) when (exception is HttpRequestException or JsonException or FormatException)
            {
                result = IpCountryLookup.UnknownPublic;
                _logger.LogWarning(exception, "IP geolocation failed for {IpAddress}.", normalizedAddress);
            }

            var cacheDuration = result.WasResolved
                ? TimeSpan.FromHours(Math.Max(1, _options.CacheHours))
                : TimeSpan.FromMinutes(Math.Max(1, _options.FailureCacheMinutes));
            _cache.Set(cacheKey, result, cacheDuration);
            return result;
        }

        internal static bool IsPublicAddress(IPAddress address)
        {
            if (address.IsIPv4MappedToIPv6)
                address = address.MapToIPv4();
            if (IPAddress.IsLoopback(address))
                return false;
            if (address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any) ||
                address.Equals(IPAddress.None) || address.Equals(IPAddress.IPv6None))
                return false;

            var bytes = address.GetAddressBytes();
            if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                return bytes[0] switch
                {
                    0 or 10 or 127 => false,
                    100 when bytes[1] is >= 64 and <= 127 => false,
                    169 when bytes[1] == 254 => false,
                    172 when bytes[1] is >= 16 and <= 31 => false,
                    192 when bytes[1] == 168 => false,
                    192 when bytes[1] == 0 => false,
                    198 when bytes[1] is 18 or 19 => false,
                    198 when bytes[1] == 51 && bytes[2] == 100 => false,
                    203 when bytes[1] == 0 && bytes[2] == 113 => false,
                    >= 224 => false,
                    _ => true
                };
            }

            return !address.IsIPv6LinkLocal &&
                   !address.IsIPv6Multicast &&
                   !address.IsIPv6SiteLocal &&
                   (bytes[0] & 0xFE) != 0xFC;
        }

        private static string? ReadString(JsonElement element, string propertyName) =>
            element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

        private static double? ReadDouble(JsonElement element, string propertyName) =>
            element.TryGetProperty(propertyName, out var value) && value.TryGetDouble(out var result)
                ? result
                : null;
    }
}
