using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PersonalCabinetEducationProgram.Data;
using PersonalCabinetEducationProgram.Models;
using PersonalCabinetEducationProgram.Services;

namespace PersonalCabinetEducationProgram.Controllers
{
    [Authorize]
    public class ElementFilesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ElementAccessService _accessService;
        private readonly FileStorageSettings _settings;

        public ElementFilesController(
            ApplicationDbContext context,
            ElementAccessService accessService,
            IOptions<FileStorageSettings> settings)
        {
            _context = context;
            _accessService = accessService;
            _settings = settings.Value;
        }

        [AppRateLimit(AppRateLimitPolicies.FileDownload)]
        public Task<IActionResult> Preview(int id) => SendFile(id, false);

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AppRateLimit(AppRateLimitPolicies.FileDownload)]
        public Task<IActionResult> Download(int id) => SendFile(id, true);

        private async Task<IActionResult> SendFile(int id, bool download)
        {
            var file = await _context.EducationalProgramElementFiles
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == id);

            if (file == null)
                return NotFound();

            if (!await _accessService.CanViewElementAsync(User, file.EducationalProgramElementId))
                return Forbid();

            if (!SupportedDocumentFormats.IsSupported(file.OriginalFileName))
                return NotFound();

            var fullPath = StoredFilePath.Resolve(_settings.StoragePath, file.StoredFileName);
            if (fullPath == null)
                return NotFound();

            const string contentType = SupportedDocumentFormats.PdfContentType;

            if (download)
                return PhysicalFile(fullPath, contentType, file.OriginalFileName);

            FileContentDisposition.SetInline(Response, file.OriginalFileName);
            return PhysicalFile(fullPath, contentType);
        }
    }
}
