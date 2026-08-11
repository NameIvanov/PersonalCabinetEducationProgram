namespace PersonalCabinetEducationProgram.Services
{
    public sealed class SecurityMonitoringOptions
    {
        public const string SectionName = "SecurityMonitoring";

        public int InvalidFileBlockThreshold { get; set; } = 5;
        public int DownloadWarningThresholdPerMinute { get; set; } = 10;
        public int DownloadBlockThresholdPerMinute { get; set; } = 20;

        public int UserRequestWarningPerMinute { get; set; } = 100;
        public int UserRequestWarningPerHour { get; set; } = 1000;
        public int AnonymousIpRequestWarningPerMinute { get; set; } = 25;
        public int AnonymousIpRequestWarningPerHour { get; set; } = 200;
        public int AuthenticatedIpRequestWarningPerMinute { get; set; } = 300;
        public int AuthenticatedIpRequestWarningPerHour { get; set; } = 5000;

        public long LargeDocumentWarningBytes { get; set; } = 40L * 1024 * 1024;
        public long LargeDocumentHighRiskBytes { get; set; } = 49_807_360L;
        public long LargeDocumentGroupWarningBytes { get; set; } = 150L * 1024 * 1024;
        public long LargePlxWarningBytes { get; set; } = 5L * 1024 * 1024;
        public long LargePlxHighRiskBytes { get; set; } = 10L * 1024 * 1024;

        public IpGeolocationOptions IpGeolocation { get; set; } = new();
    }

    public sealed class IpGeolocationOptions
    {
        public bool Enabled { get; set; } = true;
        public string AllowedCountryCode { get; set; } = "RU";
        public string EndpointTemplate { get; set; } = "https://ipwho.is/{0}";
        public int TimeoutMilliseconds { get; set; } = 2000;
        public int CacheHours { get; set; } = 24;
        public int FailureCacheMinutes { get; set; } = 15;
    }
}
