using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Services
{
    public sealed class PlxImportStorageService
    {
        private static readonly TimeSpan StagingLifetime = TimeSpan.FromHours(24);
        private readonly FileStorageSettings _settings;

        public PlxImportStorageService(IOptions<FileStorageSettings> settings)
        {
            _settings = settings.Value;
        }

        public async Task<StagedPlxFile> StageAsync(
            IFormFile file,
            int userId,
            int programId,
            CancellationToken cancellationToken = default)
        {
            ValidateUpload(file);
            CleanupExpiredStagingFiles();

            var token = $"{Guid.NewGuid():N}-{userId}-{programId}";
            var stagingDirectory = GetStagingDirectory();
            Directory.CreateDirectory(stagingDirectory);
            var filePath = Path.Combine(stagingDirectory, $"{token}.plx");
            var metadataPath = Path.Combine(stagingDirectory, $"{token}.json");
            var metadata = new StagedPlxFile
            {
                Token = token,
                OriginalFileName = Path.GetFileName(file.FileName),
                FilePath = filePath,
                UserId = userId,
                ProgramId = programId,
                CreatedAtUtc = DateTime.UtcNow
            };

            try
            {
                await using (var output = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    await file.CopyToAsync(output, cancellationToken);

                await File.WriteAllTextAsync(metadataPath, JsonSerializer.Serialize(metadata), cancellationToken);
                return metadata;
            }
            catch
            {
                TryDelete(filePath);
                TryDelete(metadataPath);
                throw;
            }
        }

        public async Task<StagedPlxFile> GetStagedAsync(
            string token,
            int userId,
            int programId,
            CancellationToken cancellationToken = default)
        {
            ValidateToken(token, userId, programId);
            var stagingDirectory = GetStagingDirectory();
            var metadataPath = Path.Combine(stagingDirectory, $"{token}.json");
            var filePath = Path.Combine(stagingDirectory, $"{token}.plx");
            if (!File.Exists(metadataPath) || !File.Exists(filePath))
                throw new InvalidOperationException("Временный файл импорта не найден. Загрузите PLX повторно.");

            var metadataJson = await File.ReadAllTextAsync(metadataPath, cancellationToken);
            var metadata = JsonSerializer.Deserialize<StagedPlxFile>(metadataJson)
                ?? throw new InvalidDataException("Не удалось прочитать сведения о временном файле PLX.");
            if (metadata.UserId != userId || metadata.ProgramId != programId || metadata.Token != token)
                throw new InvalidOperationException("Файл импорта не принадлежит выбранной ОПОП.");
            if (DateTime.UtcNow - metadata.CreatedAtUtc > StagingLifetime)
            {
                DeleteStaged(metadata);
                throw new InvalidOperationException("Срок предварительного просмотра истёк. Загрузите PLX повторно.");
            }

            return new StagedPlxFile
            {
                Token = metadata.Token,
                OriginalFileName = metadata.OriginalFileName,
                FilePath = filePath,
                UserId = metadata.UserId,
                ProgramId = metadata.ProgramId,
                CreatedAtUtc = metadata.CreatedAtUtc
            };
        }

        public async Task<string> CopyToArchiveAsync(StagedPlxFile staged, CancellationToken cancellationToken = default)
        {
            var storageRoot = GetStorageRoot();
            var archiveDirectory = Path.Combine(storageRoot, "plx", "imports", DateTime.UtcNow.ToString("yyyy"));
            Directory.CreateDirectory(archiveDirectory);
            var safeName = SanitizeFileName(staged.OriginalFileName);
            var targetPath = Path.Combine(archiveDirectory, $"{Guid.NewGuid():N}_{safeName}");

            await using var source = new FileStream(staged.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            await using var target = new FileStream(targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await source.CopyToAsync(target, cancellationToken);
            return Path.GetRelativePath(storageRoot, targetPath);
        }

        public string ResolveStoredPath(string storedPath)
        {
            var storageRoot = GetStorageRoot();
            var fullPath = Path.GetFullPath(Path.Combine(storageRoot, storedPath));
            if (!fullPath.StartsWith(storageRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Недопустимый путь к файлу импорта.");
            return fullPath;
        }

        public void DeleteStored(string storedPath)
        {
            TryDelete(ResolveStoredPath(storedPath));
        }

        public void DeleteStaged(StagedPlxFile staged)
        {
            TryDelete(staged.FilePath);
            TryDelete(Path.Combine(GetStagingDirectory(), $"{staged.Token}.json"));
        }

        private static void ValidateUpload(IFormFile file)
        {
            if (file == null || file.Length == 0)
                throw new InvalidOperationException("Выберите непустой файл PLX.");
            if (file.Length > PlxParserService.MaxPlxFileSizeBytes)
                throw new InvalidOperationException("Размер файла PLX не должен превышать 20 МБ.");
            if (file.FileName.Length > 255)
                throw new InvalidOperationException("Имя файла PLX не должно превышать 255 символов.");
            if (!Path.GetExtension(file.FileName).Equals(".plx", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Для импорта требуется файл с расширением .plx.");
        }

        private static void ValidateToken(string token, int userId, int programId)
        {
            var parts = token?.Split('-', StringSplitOptions.RemoveEmptyEntries) ?? [];
            if (parts.Length != 3 ||
                !Guid.TryParseExact(parts[0], "N", out _) ||
                !int.TryParse(parts[1], out var tokenUserId) ||
                !int.TryParse(parts[2], out var tokenProgramId) ||
                tokenUserId != userId || tokenProgramId != programId)
                throw new InvalidOperationException("Некорректный идентификатор предварительного просмотра.");
        }

        private string GetStagingDirectory() => Path.Combine(GetStorageRoot(), "plx", "staging");

        private string GetStorageRoot() => Path.GetFullPath(_settings.StoragePath);

        private void CleanupExpiredStagingFiles()
        {
            var directory = GetStagingDirectory();
            if (!Directory.Exists(directory))
                return;

            foreach (var filePath in Directory.EnumerateFiles(directory))
            {
                try
                {
                    if (DateTime.UtcNow - File.GetLastWriteTimeUtc(filePath) > StagingLifetime)
                        File.Delete(filePath);
                }
                catch (IOException)
                {
                    // Another request may still be reading this staging file.
                }
                catch (UnauthorizedAccessException)
                {
                    // Cleanup is best-effort and must not block a new import.
                }
            }
        }

        private static string SanitizeFileName(string originalFileName)
        {
            var name = Path.GetFileName(originalFileName);
            foreach (var invalidCharacter in Path.GetInvalidFileNameChars())
                name = name.Replace(invalidCharacter, '_');
            if (!name.EndsWith(".plx", StringComparison.OrdinalIgnoreCase))
                name += ".plx";
            return name.Length <= 180 ? name : $"{Path.GetFileNameWithoutExtension(name)[..170]}.plx";
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (IOException)
            {
                // Cleanup is best-effort; import data has already been handled.
            }
            catch (UnauthorizedAccessException)
            {
                // Cleanup is best-effort; import data has already been handled.
            }
        }
    }
}
