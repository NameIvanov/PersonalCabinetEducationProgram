namespace PersonalCabinetEducationProgram.Services
{
    public static class FileUploadLimits
    {
        public const long MaxFileSizeBytes = 50L * 1024 * 1024;
        public const int MaxFilesPerGroup = 20;
        public const long MaxGroupSizeBytes = 200L * 1024 * 1024;
        public const long MaxRequestSizeBytes = MaxGroupSizeBytes + 1024 * 1024;
        public const string MaxFileSizeDisplay = "50 МБ";
        public const string MaxGroupSizeDisplay = "200 МБ";
    }
}
