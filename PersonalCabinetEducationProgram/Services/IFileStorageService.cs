using Microsoft.AspNetCore.Http;

namespace PersonalCabinetEducationProgram.Services
{
    public interface IFileStorageService
    {
        Task<string> SaveFileAsync(IFormFile file);
    }
}
