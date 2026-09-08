using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PersonalCabinetEducationProgram.Data;
using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Services.Documents;

public sealed class DocumentAnalysisService(ApplicationDbContext db, DocumentAnalysisAccessService access,
    IOptions<DocumentAnalysisOptions> options, IOptions<FileStorageSettings> storage,
    IPdfTextExtractor extractor, IDocumentSemanticAnalyzer semantic)
{
    public async Task<DocumentAnalysis> EnqueueAsync(ClaimsPrincipal user, int fileId, int? previousFileId, CancellationToken ct)
    {
        var file = await access.GetFileAsync(user, fileId) ?? throw new UnauthorizedAccessException();
        if (!int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) throw new UnauthorizedAccessException();
        if (!CanStart(file)) throw new DocumentAnalysisException("Проверка перед согласованием доступна для текущего незавершённого PDF.");
        EducationalProgramElementFile? previous = null;
        if (previousFileId.HasValue)
        {
            previous = await access.GetFileAsync(user, previousFileId.Value);
            if (previous == null || previous.EducationalProgramElementId != file.EducationalProgramElementId ||
                previous.Id == file.Id || previous.UploadedAt > file.UploadedAt || previous.IsCurrent)
                throw new UnauthorizedAccessException();
        }
        var existing = await db.DocumentAnalyses.AsNoTracking().SingleOrDefaultAsync(a => a.ActiveKey == $"manager:{fileId}", ct);
        if (existing != null) return existing;
        var config = options.Value;
        if (await db.DocumentAnalyses.CountAsync(a => a.RequestedByUserId == userId && a.QueueSlot != null, ct) >= config.MaxQueuedPerUser)
            throw new DocumentAnalysisException("У вас уже есть проверки в очереди. Дождитесь их завершения.");
        var template = config.Templates.Where(t => t.OrganizationKey == config.OrganizationKey &&
                (t.ElementType == "*" || t.ElementType == file.Element.TypeElement) &&
                (t.EducationalLevel == "*" || t.EducationalLevel == file.Element.EducationalProgram.EducationalLevel))
            .OrderByDescending(t => (t.ElementType != "*" ? 2 : 0) + (t.EducationalLevel != "*" ? 1 : 0)).FirstOrDefault();
        var templateJson = JsonSerializer.Serialize(template);
        if (templateJson.Length > 16000) throw new DocumentAnalysisException("Шаблон превышает допустимый размер. Обратитесь к администратору.");
        var hash = await HashAsync(file, ct);
        var previousHash = previous == null ? null : await HashAsync(previous, ct);
        var occupied = await db.DocumentAnalyses.Where(a => a.QueueSlot != null).Select(a => a.QueueSlot!.Value).ToListAsync(ct);
        foreach (var slot in Enumerable.Range(1, config.QueueCapacity).Except(occupied))
        {
            var userKeys = await db.DocumentAnalyses.Where(a => a.RequestedByUserId == userId && a.UserQueueKey != null)
                .Select(a => a.UserQueueKey).ToListAsync(ct);
            var userKey = Enumerable.Range(1, config.MaxQueuedPerUser).Select(i => $"{userId}:{i}").FirstOrDefault(key => !userKeys.Contains(key));
            if (userKey == null) throw new DocumentAnalysisException("У вас уже есть проверки в очереди. Дождитесь их завершения.");
            var analysis = new DocumentAnalysis
            {
                FileId = fileId, FileRevision = file.RevisionNumber, FileHash = hash,
                RequestedByUserId = userId, RequestedByName = Limit(user.Identity?.Name ?? userId.ToString(), 300),
                PreviousFileId = previous?.Id, PreviousFileHash = previousHash,
                QueueSlot = slot, ActiveKey = $"manager:{fileId}", UserQueueKey = userKey, Status = DocumentAnalysisStatus.Queued,
                TemplateSnapshot = templateJson, DeadlineUtc = DateTime.UtcNow.AddSeconds(config.MaxQueueWaitSeconds),
                ProviderModel = config.SemanticAnalysisEnabled ? Limit(config.Model, 200) : "Отключён",
                Message = "Ожидание в очереди. Можно закрыть страницу и вернуться позже."
            };
            db.DocumentAnalyses.Add(analysis);
            try { await db.SaveChangesAsync(ct); return analysis; }
            catch (DbUpdateException)
            {
                db.Entry(analysis).State = EntityState.Detached;
                existing = await db.DocumentAnalyses.AsNoTracking().SingleOrDefaultAsync(a => a.ActiveKey == $"manager:{fileId}", ct);
                if (existing != null) return existing;
                // Competing enqueue can take a unique slot; try another free slot.
                if (!await db.DocumentAnalyses.AnyAsync(a => a.QueueSlot == slot || a.UserQueueKey == userKey, ct)) throw;
            }
        }
        throw new DocumentAnalysisException("Очередь проверок заполнена. Попробуйте позже.");
    }

    public async Task ProcessAsync(DocumentAnalysis analysis, CancellationToken ct)
    {
        var file = await ReauthorizeAsync(analysis, ct);
        if (!CanStart(file)) throw new DocumentAnalysisException("Файл уже заменён, удалён или отправлен на согласование. Запустите проверку актуальной версии.");
        var path = Resolve(file);
        var hash = await HashAsync(file, ct);
        if (hash != analysis.FileHash || file.RevisionNumber != analysis.FileRevision)
            throw new DocumentAnalysisException("Содержимое или версия PDF изменились после постановки в очередь. Запустите новую проверку.");
        var pdf = await extractor.ExtractAsync(path, ct);
        // Also detect external changes while parsing, before transmitting any text.
        if (await HashAsync(file, ct) != hash) throw new DocumentAnalysisException("PDF изменился во время извлечения текста. Повторите проверку.");
        foreach (var page in pdf.Pages)
        {
            analysis.Pages.Add(new()
            {
                PageNumber = page.Number, HasTextLayer = page.HasTextLayer, IsBlank = page.IsBlank,
                LowReadability = page.LowReadability, Rotation = page.Rotation, FromOcr = page.FromOcr,
                Message = page.IsBlank ? "Пустая страница (не найден текст, изображения или векторная графика)." :
                    page.LowReadability ? "Низкая читаемость текста или низкое разрешение скана: требуется ручная проверка." :
                    string.IsNullOrWhiteSpace(page.Text) ? "Нет извлекаемого текста: скан, графика или пустое изображение. Требуется OCR/ручная проверка." :
                    page.FromOcr ? "Текст получен OCR; проверьте цитаты по изображению." :
                    page.Rotation != 0 ? $"Поворот страницы: {page.Rotation}°. Проверьте ориентацию." : "Текстовый слой доступен."
            });
        }
        var template = JsonSerializer.Deserialize<DocumentRequirementTemplate>(analysis.TemplateSnapshot);
        var findings = DocumentRules.Check(pdf.Pages, file.Element.EducationalProgram, template);
        var manual = pdf.Pages.Any(p => p.IsBlank || p.LowReadability || p.FromOcr || p.Rotation != 0 || string.IsNullOrWhiteSpace(p.Text));
        var messages = new List<string> { pdf.OcrStatus };
        if (template == null) { manual = true; messages.Add("Шаблон для типа элемента/уровня образования не настроен; обязательные разделы и реквизиты не проверены."); }
        var comparisonComplete = false;
        if (analysis.PreviousFileId.HasValue)
        {
            var principal = await access.CurrentPrincipalAsync(analysis.RequestedByUserId, ct) ?? throw new UnauthorizedAccessException();
            var previous = await access.GetFileAsync(principal, analysis.PreviousFileId.Value, analysis.Purpose);
            if (previous == null || previous.EducationalProgramElementId != file.EducationalProgramElementId) throw new UnauthorizedAccessException();
            if (await HashAsync(previous, ct) != analysis.PreviousFileHash) throw new DocumentAnalysisException("Предыдущий PDF изменился. Выберите версию заново.");
            var before = await extractor.ExtractAsync(Resolve(previous), ct);
            if (await HashAsync(previous, ct) != analysis.PreviousFileHash) throw new DocumentAnalysisException("Предыдущий PDF изменился при сравнении.");
            if (before.Pages.Concat(pdf.Pages).Any(p => p.LowReadability || (!p.IsBlank && string.IsNullOrWhiteSpace(p.Text))))
            { manual = true; messages.Add("Сравнение версий неполное: в одном из PDF есть нераспознанные страницы. Требуется ручное сравнение."); }
            else
            {
                foreach (var change in DocumentVersionComparer.Compare(before.Pages, pdf.Pages)) analysis.Changes.Add(change);
                comparisonComplete = true;
            }
        }
        var readablePages = pdf.Pages.Where(p => !p.LowReadability && !string.IsNullOrWhiteSpace(p.Text)).ToList();
        await ReauthorizeAsync(analysis, ct);
        if (await HashAsync(file, ct) != hash) throw new DocumentAnalysisException("PDF изменился перед смысловой проверкой. Запустите анализ заново.");
        var meaning = readablePages.Count == 0
            ? new SemanticAnalysisResult([], "Смысловая проверка не выполнена: нет читаемого текста.", false)
            : await semantic.AnalyzeAsync(readablePages, ct);
        messages.Add(meaning.Message);
        manual |= !meaning.Complete;
        findings.AddRange(meaning.Findings);
        var valid = findings.Where(f => DocumentRules.HasValidEvidence(f, pdf.Pages))
            .DistinctBy(f => (f.Type, f.PageNumber, f.Quote)).ToList();
        if (valid.Count > options.Value.MaxFindings) { manual = true; messages.Add("Достигнут лимит замечаний. Остальные места проверьте вручную."); }
        foreach (var finding in valid.Take(options.Value.MaxFindings)) analysis.Findings.Add(finding);
        // A revoked assignment must never leave newly generated results visible.
        await ReauthorizeAsync(analysis, ct);
        if (await HashAsync(file, ct) != hash) throw new DocumentAnalysisException("PDF изменился во время анализа. Результат не сохранён; повторите проверку.");
        analysis.Status = manual ? DocumentAnalysisStatus.NeedsManualReview : DocumentAnalysisStatus.Completed;
        analysis.Message = Limit(string.Join(" ", messages), 1500);
        analysis.Summary = $"Проверено страниц: {pdf.Pages.Count}; замечаний: {analysis.Findings.Count}. " +
            (comparisonComplete ? $"Текстовое сравнение: добавлено страниц {analysis.Changes.Count(c => c.Kind == "Added")}, удалено {analysis.Changes.Count(c => c.Kind == "Removed")}, изменено {analysis.Changes.Count(c => c.Kind == "Changed")}. " :
                analysis.PreviousFileId.HasValue ? "Не удалось достоверно сравнить версии. Требуется ручное сравнение. " : "Предыдущая версия для сравнения не выбрана. ") +
            "Результат носит рекомендательный характер; решение об отправке принимает руководитель.";
        Finish(analysis);
        await db.SaveChangesAsync(ct);
    }

    private async Task<EducationalProgramElementFile> ReauthorizeAsync(DocumentAnalysis analysis, CancellationToken ct)
    {
        var principal = await access.CurrentPrincipalAsync(analysis.RequestedByUserId, ct) ?? throw new UnauthorizedAccessException();
        var file = await access.GetFileAsync(principal, analysis.FileId, analysis.Purpose) ?? throw new UnauthorizedAccessException();
        if (analysis.Purpose == DocumentAnalysisPurpose.ManagerPreflight && !CanStart(file))
            throw new DocumentAnalysisException("Файл уже заменён, удалён или отправлен на согласование. Запустите проверку актуальной версии.");
        return file;
    }

    public static bool CanStart(EducationalProgramElementFile file) => file.IsCurrent && !file.IsRemoved && !file.IsSubmitted &&
        ElementApprovalStatus.Normalize(file.Element.StatusApprovals) is ElementApprovalStatus.Uploaded or ElementApprovalStatus.RevisionRequired;

    public static void Finish(DocumentAnalysis analysis)
    {
        analysis.QueueSlot = null; analysis.ActiveKey = null; analysis.UserQueueKey = null; analysis.CompletedAtUtc = DateTime.UtcNow;
        analysis.ConcurrencyToken = Guid.NewGuid();
    }

    private string Resolve(EducationalProgramElementFile file)
    {
        if (!string.Equals(Path.GetExtension(file.OriginalFileName), ".pdf", StringComparison.OrdinalIgnoreCase))
            throw new DocumentAnalysisException("Помощник работает только с PDF.");
        return StoredFilePath.Resolve(storage.Value.StoragePath, file.StoredFileName)
            ?? throw new DocumentAnalysisException("PDF отсутствует в хранилище или временно недоступен.");
    }

    private async Task<string> HashAsync(EducationalProgramElementFile file, CancellationToken ct)
    {
        await using var stream = new FileStream(Resolve(file), FileMode.Open, FileAccess.Read, FileShare.Read, 65536, true);
        if (stream.Length == 0 || stream.Length > options.Value.MaxFileBytes)
            throw new DocumentAnalysisException($"Допустимый размер PDF для анализа: до {options.Value.MaxFileBytes / 1024 / 1024} МБ.");
        var header = new byte[5];
        if (await stream.ReadAsync(header, ct) != 5 || !header.AsSpan().SequenceEqual("%PDF-"u8))
            throw new DocumentAnalysisException("Содержимое файла не соответствует PDF.");
        stream.Position = 0;
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, ct));
    }

    private static string Limit(string text, int length) => text[..Math.Min(length, text.Length)];
}
