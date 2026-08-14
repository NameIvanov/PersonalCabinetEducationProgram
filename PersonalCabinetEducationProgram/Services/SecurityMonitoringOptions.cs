namespace PersonalCabinetEducationProgram.Services
{
    public sealed class SecurityMonitoringOptions
    {
        public const string SectionName = "SecurityMonitoring";

        public int InvalidFileBlockThreshold { get; set; } = 5;
        public int DownloadWarningThresholdPerMinute { get; set; } = 10;
        public int DownloadBlockThresholdPerMinute { get; set; } = 20;

        public int AccountRiskBlockScore { get; set; } = 6;
        public int AccountRiskWindowHours { get; set; } = 24;
        public int HighSeverityRiskPoints { get; set; } = 1;
        public int CriticalSeverityRiskPoints { get; set; } = 2;

        public int IpRiskSuspiciousScore { get; set; } = 6;
        public int IpRiskBlockScore { get; set; } = 15;
        public int IpRiskWindowHours { get; set; } = 24;
        public int IpRiskFirstBlockHours { get; set; } = 1;
        public int IpRiskRepeatWindowDays { get; set; } = 7;
        public int IpRiskSecondBlockHours { get; set; } = 24;

        public int AnonymousProbeInitialThreshold { get; set; } = 3;
        public int AnonymousProbeInitialWindowMinutes { get; set; } = 10;
        public int AnonymousProbeFirstBlockMinutes { get; set; } = 30;
        public int AnonymousProbeRepeatThreshold { get; set; } = 3;
        public int AnonymousProbeEscalationWindowDays { get; set; } = 7;
        public int AnonymousProbeSecondBlockHours { get; set; } = 24;

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

        public bool BlockNewForeignLogin { get; set; }
        public double ImpossibleTravelMinDistanceKm { get; set; } = 500;
        public double ImpossibleTravelMaxSpeedKmh { get; set; } = 900;
        public int NewNetworksWarningCount { get; set; } = 3;
        public int NewNetworksWindowHours { get; set; } = 24;
        public int FailedLoginCorrelationCount { get; set; } = 3;
        public int FailedLoginCorrelationMinutes { get; set; } = 15;
        public int ConcurrentSessionWindowMinutes { get; set; } = 30;
        public int SessionActivityUpdateMinutes { get; set; } = 5;
        public int SessionInactiveHours { get; set; } = 24;

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
