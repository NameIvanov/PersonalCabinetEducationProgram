using static System.Net.WebRequestMethods;

namespace PersonalCabinetEducationProgram.Models
{
    public class FileStorageSettings
    {
        public string StoragePath { get; set; } = "C:\\2026";
        public string BaseUrl { get; set; } = "/uploads/";
    }
}
