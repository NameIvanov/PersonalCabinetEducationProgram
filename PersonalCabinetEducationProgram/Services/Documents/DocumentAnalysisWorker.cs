using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PersonalCabinetEducationProgram.Data;
using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Services.Documents;

public sealed class DocumentAnalysisWorker(IServiceScopeFactory scopes, IOptions<DocumentAnalysisOptions> options,
    ILogger<DocumentAnalysisWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (await RunOneAsync(stoppingToken)) continue;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch
            {
                // Neither exception details nor PDF/provider content belong in application logs.
                logger.LogWarning("Document analysis queue temporarily unavailable.");
            }
            try { await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    public async Task<bool> RunOneAsync(CancellationToken stoppingToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = DateTime.UtcNow;
        var expired = await db.DocumentAnalyses.Where(a => a.QueueSlot != null && a.DeadlineUtc < now).Take(50).ToListAsync(stoppingToken);
        foreach (var item in expired)
        {
            item.Status = DocumentAnalysisStatus.Failed;
            item.Message = "Время ожидания/обработки истекло либо сервер был перезапущен. Запустите проверку повторно.";
            DocumentAnalysisService.Finish(item);
        }
        try { await db.SaveChangesAsync(stoppingToken); }
        catch (DbUpdateConcurrencyException) { return true; }

        var analysis = await db.DocumentAnalyses.Where(a => a.Status == DocumentAnalysisStatus.Queued)
            .OrderBy(a => a.CreatedAtUtc).FirstOrDefaultAsync(stoppingToken);
        if (analysis == null) return false;
        analysis.Status = DocumentAnalysisStatus.Running;
        analysis.StartedAtUtc = DateTime.UtcNow;
        analysis.DeadlineUtc = DateTime.UtcNow.AddSeconds(options.Value.AnalysisTimeoutSeconds + 30);
        analysis.ConcurrencyToken = Guid.NewGuid();
        analysis.Message = "Извлечение PDF и проверка выбранной версии…";
        try { await db.SaveChangesAsync(stoppingToken); }
        catch (DbUpdateConcurrencyException) { return true; }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.AnalysisTimeoutSeconds));
        try
        {
            await scope.ServiceProvider.GetRequiredService<DocumentAnalysisService>().ProcessAsync(analysis, timeout.Token);
        }
        catch (Exception ex)
        {
            // Discard pending navigation rows before persisting a safe terminal status.
            db.ChangeTracker.Clear();
            var failed = await db.DocumentAnalyses.SingleOrDefaultAsync(a => a.Id == analysis.Id, CancellationToken.None);
            if (failed == null || failed.Status != DocumentAnalysisStatus.Running) return true;
            failed.Status = ex is UnauthorizedAccessException ? DocumentAnalysisStatus.Cancelled : DocumentAnalysisStatus.Failed;
            failed.Message = ex switch
            {
                UnauthorizedAccessException => "Проверка отменена: права доступа или состояние учётной записи изменились.",
                OperationCanceledException => "Анализ остановлен по таймауту или при остановке сервера. Повторите проверку; для скана может требоваться ручная обработка.",
                DocumentAnalysisException known => known.Message,
                _ => "Не удалось выполнить анализ PDF. Проверьте доступность хранилища и повторите позже."
            };
            DocumentAnalysisService.Finish(failed);
            try { await db.SaveChangesAsync(CancellationToken.None); }
            catch (DbUpdateConcurrencyException) { }
        }
        return true;
    }
}
