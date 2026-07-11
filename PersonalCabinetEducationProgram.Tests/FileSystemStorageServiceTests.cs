using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using PersonalCabinetEducationProgram.Models;
using PersonalCabinetEducationProgram.Services;

namespace PersonalCabinetEducationProgram.Tests;

public class FileSystemStorageServiceTests
{
    [Fact]
    public async Task SaveFileAsync_RejectsFileLargerThan50Mb()
    {
        var settings = Options.Create(new FileStorageSettings
        {
            StoragePath = Path.Combine(Path.GetTempPath(), "pcep-file-tests")
        });
        var service = new FileSystemStorageService(settings);
        var file = new FormFile(
            Stream.Null,
            0,
            FileUploadLimits.MaxFileSizeBytes + 1,
            "file",
            "large.pdf");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SaveFileAsync(file));

        Assert.Contains(FileUploadLimits.MaxFileSizeDisplay, exception.Message);
    }

    [Fact]
    public async Task ValidateFileAsync_AcceptsRealPdfSignature()
    {
        var service = CreateService();
        var bytes = "%PDF-1.7\n%%EOF"u8.ToArray();
        var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "document.pdf");

        await service.ValidateFileAsync(file);
    }

    [Fact]
    public async Task ValidateFileAsync_RejectsRenamedExecutable()
    {
        var service = CreateService();
        var bytes = "MZ-not-a-pdf"u8.ToArray();
        var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "document.pdf");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ValidateFileAsync(file));

        Assert.Contains("не соответствует", exception.Message);
    }

    private static FileSystemStorageService CreateService()
    {
        return new FileSystemStorageService(Options.Create(new FileStorageSettings
        {
            StoragePath = Path.Combine(Path.GetTempPath(), "pcep-file-tests")
        }));
    }
}
