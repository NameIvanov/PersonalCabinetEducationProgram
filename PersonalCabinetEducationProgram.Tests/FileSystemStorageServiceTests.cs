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
}
