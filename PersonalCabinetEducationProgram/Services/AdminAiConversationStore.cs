using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PersonalCabinetEducationProgram.Data;
using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Services;

/// <summary>Stores a bounded private history for the signed-in administrator.</summary>
public sealed class AdminAiConversationStore
{
    private readonly ApplicationDbContext _context;
    private readonly AiOptions _options;

    public AdminAiConversationStore(ApplicationDbContext context, IOptions<AiOptions> options)
    {
        _context = context;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<AdminAiConversationMessage>> GetRecentAsync(int userId, int limit, CancellationToken cancellationToken)
    {
        await DeleteExpiredAsync(userId, cancellationToken);
        return await _context.AdminAiConversationMessages.AsNoTracking()
            .Where(message => message.UserId == userId)
            .OrderByDescending(message => message.CreatedAtUtc)
            .Take(Math.Clamp(limit, 1, _options.MaxStoredConversationMessages))
            .OrderBy(message => message.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task AppendAsync(int userId, string role, string content, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(content)) return;

        _context.AdminAiConversationMessages.Add(new AdminAiConversationMessage
        {
            UserId = userId,
            Role = role is "user" or "assistant" ? role : "assistant",
            Content = content.Trim()[..Math.Min(content.Trim().Length, _options.MaxStoredConversationMessageLength)],
            CreatedAtUtc = DateTime.UtcNow
        });
        await DeleteExpiredAsync(userId, cancellationToken, saveChanges: false);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task ClearAsync(int userId, CancellationToken cancellationToken)
    {
        var messages = await _context.AdminAiConversationMessages.Where(message => message.UserId == userId)
            .ToListAsync(cancellationToken);
        if (messages.Count == 0) return;
        _context.AdminAiConversationMessages.RemoveRange(messages);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task DeleteExpiredAsync(int userId, CancellationToken cancellationToken, bool saveChanges = true)
    {
        var expiresAt = DateTime.UtcNow.AddDays(-Math.Max(1, _options.ConversationHistoryRetentionDays));
        var expired = await _context.AdminAiConversationMessages
            .Where(message => message.UserId == userId && message.CreatedAtUtc < expiresAt)
            .ToListAsync(cancellationToken);
        if (expired.Count == 0) return;
        _context.AdminAiConversationMessages.RemoveRange(expired);
        if (saveChanges) await _context.SaveChangesAsync(cancellationToken);
    }
}
