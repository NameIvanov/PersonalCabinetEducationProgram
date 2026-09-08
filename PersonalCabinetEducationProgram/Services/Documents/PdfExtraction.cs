using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace PersonalCabinetEducationProgram.Services.Documents;

public sealed record PdfPageText(int Number, string Text, bool HasTextLayer, bool IsBlank,
    bool LowReadability, int Rotation, bool FromOcr = false);
public sealed record PdfTextResult(List<PdfPageText> Pages, string OcrStatus = "Не требуется");

public interface IPdfTextExtractor
{
    Task<PdfTextResult> ExtractAsync(string path, CancellationToken cancellationToken);
}

// Run before web host/configuration initialization. Parsing hostile PDFs is killable,
// and the child never receives provider keys or database configuration.
public static class PdfExtractionWorker
{
    public static bool TryRun(string[] args)
    {
        if (args.FirstOrDefault() != "--extract-document") return false;
        Console.OutputEncoding = Encoding.UTF8;
        try
        {
            if (args.Length != 5) throw new InvalidDataException();
            var result = Extract(args[1], int.Parse(args[2]), int.Parse(args[3]), long.Parse(args[4]));
            Console.Write(JsonSerializer.Serialize(result));
        }
        catch
        {
            // No PDF text, paths or parser exception messages in stdout/stderr logs.
            Environment.ExitCode = 2;
        }
        return true;
    }

    public static PdfTextResult Extract(string path, int maxPages, int maxCharacters, long maxBytes)
    {
        if (new FileInfo(path).Length > maxBytes) throw new InvalidDataException();
        using var document = PdfDocument.Open(path);
        if (document.NumberOfPages == 0 || document.NumberOfPages > maxPages) throw new InvalidDataException();
        var pages = new List<PdfPageText>();
        var total = 0;
        foreach (var page in document.GetPages())
        {
            var text = ContentOrderTextExtractor.GetText(page);
            total += text.Length;
            if (total > maxCharacters) throw new InvalidDataException();
            var hasText = !string.IsNullOrWhiteSpace(text);
            var visible = text.Count(c => !char.IsWhiteSpace(c));
            var readable = text.Count(char.IsLetterOrDigit);
            var low = hasText && (text.Contains('\uFFFD') || readable < visible * .4);
            // A large image below 100 DPI is a concrete indication of poor scan resolution.
            // Small illustrations/logos do not determine the readability of the whole page.
            if (!hasText && page.NumberOfImages > 0)
                low |= page.GetImages().Any(image => image.Bounds.Width > page.Width * .5 && image.Bounds.Height > page.Height * .5 &&
                    (image.WidthInSamples * 72.0 / image.Bounds.Width < 100 || image.HeightInSamples * 72.0 / image.Bounds.Height < 100));
            // Conservative: image-only/vector-only pages must be inspected or OCR'd.
            var blank = !hasText && page.NumberOfImages == 0 && page.Paths.Count == 0;
            pages.Add(new(page.Number, text, hasText, blank, low, page.Rotation.Value));
        }
        return new(pages);
    }
}

public sealed class PdfTextExtractor(IOptions<DocumentAnalysisOptions> options) : IPdfTextExtractor
{
    public async Task<PdfTextResult> ExtractAsync(string path, CancellationToken cancellationToken)
    {
        var original = await ExtractLayerAsync(path, cancellationToken);
        if (!original.Pages.Any(p => !p.IsBlank && (!p.HasTextLayer || p.LowReadability))) return original;
        if (string.IsNullOrWhiteSpace(options.Value.OcrExecutable))
            return original with { OcrStatus = "Требуется OCR или ручная проверка: локальный OCR не настроен." };

        var directory = Path.Combine(Path.GetTempPath(), "opop-ocr-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var output = Path.Combine(directory, "recognized.pdf");
        try
        {
            var start = NewProcess(options.Value.OcrExecutable!);
            start.Environment["TEMP"] = directory;
            start.Environment["TMP"] = directory;
            start.Environment["TMPDIR"] = directory;
            foreach (var argument in new[] { "--skip-text", "--rotate-pages", "--deskew", "--jobs", "1",
                         "--tesseract-timeout", "30", "-l", options.Value.OcrLanguages, path, output })
                start.ArgumentList.Add(argument);
            await RunAsync(start, 16000, cancellationToken);
            var recognized = await ExtractLayerAsync(output, cancellationToken);
            if (recognized.Pages.Count != original.Pages.Count) throw new InvalidDataException();
            var merged = original.Pages.Select((page, i) => page.HasTextLayer ? page :
                recognized.Pages[i] with { HasTextLayer = false, FromOcr = true }).ToList();
            return new(merged, "Выполнен локальный OCR. Цитаты OCR требуют сверки с изображением страницы.");
        }
        catch (OperationCanceledException) { throw; }
        catch
        {
            return original with { OcrStatus = "OCR недоступен или не смог распознать PDF. Требуется ручная проверка." };
        }
        finally
        {
            // Remove OCR scratch files as well, including when the process was killed.
            // Validate the absolute parent before recursively deleting this generated directory.
            var temporaryRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var target = Path.GetFullPath(directory);
            if (string.Equals(Path.GetDirectoryName(target), temporaryRoot, StringComparison.OrdinalIgnoreCase) &&
                Path.GetFileName(target).StartsWith("opop-ocr-", StringComparison.Ordinal) && Directory.Exists(target))
                Directory.Delete(target, recursive: true);
        }
    }

    private async Task<PdfTextResult> ExtractLayerAsync(string path, CancellationToken ct)
    {
        var settings = options.Value;
        var start = NewProcess(settings.DotnetExecutable);
        foreach (var argument in new[] { typeof(Program).Assembly.Location, "--extract-document", path,
                     settings.MaxPages.ToString(), settings.MaxTextCharacters.ToString(), settings.MaxFileBytes.ToString() })
            start.ArgumentList.Add(argument);
        try
        {
            var json = await RunAsync(start, settings.MaxTextCharacters * 8 + settings.MaxPages * 1000, ct);
            return JsonSerializer.Deserialize<PdfTextResult>(json) ?? throw new InvalidDataException();
        }
        catch (OperationCanceledException) { throw; }
        catch
        {
            throw new DocumentAnalysisException("Не удалось прочитать PDF: файл повреждён, защищён паролем или превышает лимит страниц/текста. Требуется ручная проверка.");
        }
    }

    internal static ProcessStartInfo NewProcess(string executable)
    {
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true,
            RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8
        };
        // Child parser/OCR doesn't need application secrets from the environment.
        var allowed = new[] { "PATH", "SystemRoot", "WINDIR", "TEMP", "TMP", "TMPDIR", "HOME",
            "DOTNET_ROOT", "DOTNET_ROOT_X64", "TESSDATA_PREFIX", "LANG" };
        foreach (var key in start.Environment.Keys.ToArray())
            if (!allowed.Contains(key, StringComparer.OrdinalIgnoreCase)) start.Environment.Remove(key);
        return start;
    }

    internal static async Task<string> RunAsync(ProcessStartInfo start, int maxCharacters, CancellationToken ct)
    {
        using var process = Process.Start(start) ?? throw new InvalidOperationException();
        using var registration = ct.Register(() => { try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { } });
        try
        {
            var stdout = ReadBoundedAsync(process.StandardOutput, maxCharacters, ct);
            var stderr = ReadBoundedAsync(process.StandardError, 16000, ct);
            // A failed bounded reader kills the child immediately, preventing pipe deadlocks.
            var readers = new[] { stdout, stderr };
            foreach (var reader in readers)
                _ = reader.ContinueWith(_ => { try { process.Kill(true); } catch (InvalidOperationException) { } },
                    CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            await Task.WhenAll(readers);
            await process.WaitForExitAsync(ct);
            ct.ThrowIfCancellationRequested();
            if (process.ExitCode != 0) throw new InvalidDataException();
            return await stdout;
        }
        finally
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
        }
    }

    internal static async Task<string> ReadBoundedAsync(TextReader reader, int maxCharacters, CancellationToken ct)
    {
        var result = new StringBuilder();
        var buffer = new char[4096];
        int count;
        while ((count = await reader.ReadAsync(buffer.AsMemory(), ct)) != 0)
        {
            if (result.Length + count > maxCharacters) throw new InvalidDataException();
            result.Append(buffer, 0, count);
        }
        return result.ToString();
    }
}
