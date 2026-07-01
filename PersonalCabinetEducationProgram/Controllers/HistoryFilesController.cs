using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PersonalCabinetEducationProgram.Data;
using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Controllers
{
    [Authorize(Roles = "Manager,Approver,Moderator,Admin")]
    public class HistoryFilesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly FileStorageSettings _storageSettings;

        public HistoryFilesController(
            ApplicationDbContext context,
            IOptions<FileStorageSettings> storageSettings)
        {
            _context = context;
            _storageSettings = storageSettings.Value;
        }

        public async Task<IActionResult> Preview(int historyId)
        {
            var result = await GetHistoryFile(historyId);
            if (result == null)
                return NotFound();

            Response.Headers.Append("Content-Disposition", $"inline; filename=\"{result.Value.FileName}\"");
            return PhysicalFile(result.Value.FullPath, GetContentType(result.Value.FileName));
        }

        public async Task<IActionResult> Download(int historyId)
        {
            var result = await GetHistoryFile(historyId);
            if (result == null)
                return NotFound();

            return PhysicalFile(result.Value.FullPath, GetContentType(result.Value.FileName), result.Value.FileName);
        }

        private async Task<(string FullPath, string FileName)?> GetHistoryFile(int historyId)
        {
            var history = await _context.ElementStatusHistory
                .AsNoTracking()
                .FirstOrDefaultAsync(h => h.Id == historyId);

            if (history == null || string.IsNullOrWhiteSpace(history.FilePath))
                return null;

            var storageRoot = Path.GetFullPath(_storageSettings.StoragePath);
            var fullPath = Path.GetFullPath(Path.Combine(storageRoot, history.FilePath));
            var rootPrefix = storageRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

            if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase) ||
                !System.IO.File.Exists(fullPath))
                return null;

            return (fullPath, history.FileName ?? Path.GetFileName(history.FilePath));
        }

        private static string GetContentType(string fileName)
        {
            return Path.GetExtension(fileName).ToLowerInvariant() switch
            {
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                _ => "application/octet-stream"
            };
        }
    }
}
