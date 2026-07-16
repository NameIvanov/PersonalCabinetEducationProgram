using Microsoft.AspNetCore.Http;

namespace PersonalCabinetEducationProgram.Services
{
    public interface IFileStorageService
    {
        Task ValidateFileAsync(IFormFile file);
        Task<string> SaveFileAsync(IFormFile file);
        Task DeleteFileAsync(string storedFileName);
    }
}
