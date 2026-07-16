using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using PersonalCabinetEducationProgram.Models;
using System.IO.Compression;

namespace PersonalCabinetEducationProgram.Services
{
    public class FileSystemStorageService : IFileStorageService
    {
        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf",
            ".doc",
            ".docx"
        };

        private readonly FileStorageSettings _settings;

        public FileSystemStorageService(IOptions<FileStorageSettings> settings)
        {
            _settings = settings.Value;
        }

        public async Task<string> SaveFileAsync(IFormFile file)
        {
            await ValidateFileAsync(file);

            string uploadsFolder = _settings.StoragePath;

            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            string uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(file.FileName);
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

            try
            {
                await using var fileStream = new FileStream(filePath, FileMode.CreateNew);
                await file.CopyToAsync(fileStream);
            }
            catch
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
                throw;
            }

            // Return either the full URL or a relative path that can be combined with BaseUrl
            return uniqueFileName;
        }

        public Task DeleteFileAsync(string storedFileName)
        {
            if (string.IsNullOrWhiteSpace(storedFileName))
                return Task.CompletedTask;

            var storageRoot = Path.GetFullPath(_settings.StoragePath);
            var filePath = Path.GetFullPath(Path.Combine(storageRoot, Path.GetFileName(storedFileName)));
            if (!filePath.StartsWith(storageRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Недопустимый путь к файлу.");

            if (File.Exists(filePath))
                File.Delete(filePath);

            return Task.CompletedTask;
        }

        public async Task ValidateFileAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("Файл не выбран или пуст.");

            if (file.Length > FileUploadLimits.MaxFileSizeBytes)
                throw new InvalidOperationException($"Размер каждого файла не должен превышать {FileUploadLimits.MaxFileSizeDisplay}.");

            if (file.FileName.Length > 255)
                throw new InvalidOperationException("Имя файла не должно превышать 255 символов.");

            var extension = Path.GetExtension(file.FileName);
            if (!AllowedExtensions.Contains(extension))
                throw new InvalidOperationException("Можно загружать только PDF, DOC и DOCX файлы.");

            await using var stream = file.OpenReadStream();
            var header = new byte[8];
            var bytesRead = await stream.ReadAsync(header);
            stream.Position = 0;

            var isValid = extension.ToLowerInvariant() switch
            {
                ".pdf" => bytesRead >= 5 && header.AsSpan(0, 5).SequenceEqual("%PDF-"u8),
                ".doc" => bytesRead == 8 && header.SequenceEqual(new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }),
                ".docx" => IsWordDocument(stream),
                _ => false
            };

            if (!isValid)
                throw new InvalidOperationException($"Содержимое файла «{Path.GetFileName(file.FileName)}» не соответствует его расширению.");
        }

        private static bool IsWordDocument(Stream stream)
        {
            try
            {
                using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
                return archive.GetEntry("[Content_Types].xml") != null &&
                       archive.Entries.Any(e => e.FullName.StartsWith("word/", StringComparison.OrdinalIgnoreCase));
            }
            catch (InvalidDataException)
            {
                return false;
            }
        }
    }
}
