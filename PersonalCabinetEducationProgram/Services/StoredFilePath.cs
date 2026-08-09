namespace PersonalCabinetEducationProgram.Services
{
    public static class StoredFilePath
    {
        public static string? Resolve(string storagePath, string storedFileName)
        {
            var storageRoot = Path.GetFullPath(storagePath);
            var fullPath = Path.GetFullPath(Path.Combine(storageRoot, Path.GetFileName(storedFileName)));
            var rootPrefix = storageRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase) && File.Exists(fullPath)
                ? fullPath
                : null;
        }
    }
}
