using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PersonalCabinetEducationProgram.Data;
using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Services
{
    public sealed class StorageHealthService
    {
        private static readonly TimeSpan StagingLifetime = TimeSpan.FromHours(24);
        private readonly ApplicationDbContext _context;
        private readonly FileStorageSettings _settings;

        public StorageHealthService(
            ApplicationDbContext context,
            IOptions<FileStorageSettings> settings)
        {
            _context = context;
            _settings = settings.Value;
        }

        public async Task<StorageHealthSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
        {
            var root = Path.GetFullPath(_settings.StoragePath);
            var snapshot = new StorageHealthSnapshot
            {
                CheckedAtUtc = DateTime.UtcNow,
                StoragePath = root,
                Exists = Directory.Exists(root)
            };

            if (!snapshot.Exists)
            {
                snapshot.Error = "Каталог хранилища не существует.";
                return snapshot;
            }

            try
            {
                var drive = GetDrive(root);
                snapshot.TotalSpaceBytes = drive?.TotalSize;
                snapshot.FreeSpaceBytes = drive?.AvailableFreeSpace;
                snapshot.WriteAvailable = await TestWriteAsync(root, cancellationToken);

                var allFiles = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).ToList();
                snapshot.FileCount = allFiles.Count;
                snapshot.UsedByApplicationBytes = allFiles.Sum(path => TryGetLength(path));

                var stagingRoot = Path.Combine(root, "plx", "staging");
                var archiveRoot = Path.Combine(root, "plx", "imports");
                var stagingFiles = Directory.Exists(stagingRoot)
                    ? Directory.EnumerateFiles(stagingRoot).ToList()
                    : [];
                var archiveFiles = Directory.Exists(archiveRoot)
                    ? Directory.EnumerateFiles(archiveRoot, "*", SearchOption.AllDirectories).ToList()
                    : [];
                snapshot.PlxStagingFileCount = stagingFiles.Count;
                snapshot.ExpiredStagingFileCount = stagingFiles.Count(path =>
                    DateTime.UtcNow - File.GetLastWriteTimeUtc(path) > StagingLifetime);
                snapshot.PlxArchiveFileCount = archiveFiles.Count;
                snapshot.PlxArchiveBytes = archiveFiles.Sum(path => TryGetLength(path));

                var expectedPaths = await GetExpectedPathsAsync(root, cancellationToken);
                snapshot.DatabaseFileReferenceCount = expectedPaths.Count;
                snapshot.MissingFileCount = expectedPaths.Count(path => !File.Exists(path));

                var stagingPrefix = EnsureTrailingSeparator(stagingRoot);
                snapshot.OrphanFileCount = allFiles.Count(path =>
                    !path.StartsWith(stagingPrefix, StringComparison.OrdinalIgnoreCase) &&
                    !expectedPaths.Contains(Path.GetFullPath(path)));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                snapshot.Error = exception.Message;
            }

            return snapshot;
        }

        public StorageSidebarSnapshot GetSidebarSnapshot()
        {
            var root = Path.GetFullPath(_settings.StoragePath);
            if (!Directory.Exists(root))
                return new StorageSidebarSnapshot(false, null);

            try
            {
                var drive = GetDrive(root);
                if (drive == null || drive.TotalSize == 0)
                    return new StorageSidebarSnapshot(true, null);
                var usedPercent = (int)Math.Round((drive.TotalSize - drive.AvailableFreeSpace) * 100d / drive.TotalSize);
                return new StorageSidebarSnapshot(true, Math.Clamp(usedPercent, 0, 100));
            }
            catch
            {
                return new StorageSidebarSnapshot(false, null);
            }
        }

        private async Task<HashSet<string>> GetExpectedPathsAsync(string root, CancellationToken cancellationToken)
        {
            var elementFiles = await _context.EducationalProgramElementFiles
                .AsNoTracking()
                .Select(file => file.StoredFileName)
                .ToListAsync(cancellationToken);
            var legacyFiles = await _context.EducationalProgramElements
                .AsNoTracking()
                .Where(element => element.FilePath != null && element.FilePath != string.Empty)
                .Select(element => element.FilePath!)
                .ToListAsync(cancellationToken);
            var imports = await _context.CurriculumImports
                .AsNoTracking()
                .Select(import => import.StoredFilePath)
                .ToListAsync(cancellationToken);

            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var storedPath in elementFiles.Concat(legacyFiles).Concat(imports))
            {
                var resolved = ResolveExpectedPath(root, storedPath);
                if (resolved != null)
                    paths.Add(resolved);
            }
            return paths;
        }

        private static string? ResolveExpectedPath(string root, string storedPath)
        {
            if (string.IsNullOrWhiteSpace(storedPath))
                return null;

            var normalized = storedPath.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
            var candidate = Path.GetFullPath(Path.Combine(root, normalized));
            var rootPrefix = EnsureTrailingSeparator(root);
            if (candidate.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                return candidate;

            var fileNameCandidate = Path.GetFullPath(Path.Combine(root, Path.GetFileName(storedPath)));
            return fileNameCandidate.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase)
                ? fileNameCandidate
                : null;
        }

        private static DriveInfo? GetDrive(string fullPath) => DriveInfo.GetDrives()
            .Where(drive => drive.IsReady && fullPath.StartsWith(drive.RootDirectory.FullName, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(drive => drive.RootDirectory.FullName.Length)
            .FirstOrDefault();

        private static async Task<bool> TestWriteAsync(string root, CancellationToken cancellationToken)
        {
            var path = Path.Combine(root, $".storage-health-{Guid.NewGuid():N}.tmp");
            try
            {
                await File.WriteAllTextAsync(path, "storage health check", cancellationToken);
                return true;
            }
            finally
            {
                try
                {
                    if (File.Exists(path))
                        File.Delete(path);
                }
                catch
                {
                }
            }
        }

        private static long TryGetLength(string path)
        {
            try
            {
                return new FileInfo(path).Length;
            }
            catch
            {
                return 0;
            }
        }

        private static string EnsureTrailingSeparator(string path) =>
            Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
    }

    public sealed class StorageHealthSnapshot
    {
        public DateTime CheckedAtUtc { get; init; }
        public string StoragePath { get; init; } = string.Empty;
        public bool Exists { get; set; }
        public bool WriteAvailable { get; set; }
        public long? TotalSpaceBytes { get; set; }
        public long? FreeSpaceBytes { get; set; }
        public long UsedByApplicationBytes { get; set; }
        public int FileCount { get; set; }
        public int DatabaseFileReferenceCount { get; set; }
        public int MissingFileCount { get; set; }
        public int OrphanFileCount { get; set; }
        public int PlxArchiveFileCount { get; set; }
        public long PlxArchiveBytes { get; set; }
        public int PlxStagingFileCount { get; set; }
        public int ExpiredStagingFileCount { get; set; }
        public string? Error { get; set; }

        public int? UsedSpacePercent => TotalSpaceBytes is > 0 && FreeSpaceBytes.HasValue
            ? (int)Math.Round((TotalSpaceBytes.Value - FreeSpaceBytes.Value) * 100d / TotalSpaceBytes.Value)
            : null;
    }

    public sealed record StorageSidebarSnapshot(bool Available, int? UsedSpacePercent);
}
