namespace PersonalCabinetEducationProgram.Services
{
    public static class FileUploadLimits
    {
        public const long MaxFileSizeBytes = 50L * 1024 * 1024;
        public const long MaxRequestSizeBytes = MaxFileSizeBytes + 1024 * 1024;
        public const string MaxFileSizeDisplay = "50 МБ";
    }
}
