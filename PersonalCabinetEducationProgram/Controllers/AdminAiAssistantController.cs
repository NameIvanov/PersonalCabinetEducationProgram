using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PersonalCabinetEducationProgram.Models;
using PersonalCabinetEducationProgram.Services;
using PersonalCabinetEducationProgram.ViewModels;

namespace PersonalCabinetEducationProgram.Controllers;

[Authorize(Roles = AppRoles.Admin)]
[Route("Admin/AiAssistant")]
public sealed class AdminAiAssistantController : Controller
{
    private readonly IAiAssistantService _assistant;
    private readonly AdminAiContextService _contextService;
    private readonly AiOptions _options;
    private readonly ILogger<AdminAiAssistantController> _logger;

    public AdminAiAssistantController(
        IAiAssistantService assistant,
        AdminAiContextService contextService,
        IOptions<AiOptions> options,
        ILogger<AdminAiAssistantController> logger)
    {
        _assistant = assistant;
        _contextService = contextService;
        _options = options.Value;
        _logger = logger;
    }

    [HttpGet("")]
    [AppRateLimit(AppRateLimitPolicies.Search)]
    public IActionResult Index() => View(new AdminAiAssistantViewModel
    {
        IsConfigured = _assistant.IsConfigured,
        MaxQuestionLength = _options.MaxQuestionLength
    });

    [HttpPost("Ask")]
    [ValidateAntiForgeryToken]
    [AppRateLimit(AppRateLimitPolicies.AiAssistant)]
    public async Task<IActionResult> Ask([FromBody] AdminAiQuestionRequest? request, CancellationToken cancellationToken)
    {
        var question = request?.Question?.Trim();
        if (string.IsNullOrWhiteSpace(question))
            return BadRequest(new { message = "Введите вопрос для помощника." });
        if (question.Length > _options.MaxQuestionLength)
            return BadRequest(new { message = $"Вопрос не должен превышать {_options.MaxQuestionLength} символов." });
        if (!_assistant.IsConfigured)
            return Ok(AiAssistantResult.NotConfigured());

        try
        {
            var pageArea = AdminAiContextService.ResolvePageArea(request?.CurrentPage);
            var adminUserId = int.TryParse(
                User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                out var parsedAdminUserId)
                ? parsedAdminUserId
                : (int?)null;
            var safeContext = await _contextService.BuildSummaryAsync(pageArea, adminUserId, cancellationToken);
            var result = await _assistant.AskAsync(question, safeContext, cancellationToken);
            _logger.LogInformation("AI assistant request processed. Admin {AdminId}; success {Success}; provider {Provider}; model {Model}",
                User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "unknown",
                result.Succeeded,
                _options.Provider,
                _options.Model);
            return Ok(result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return StatusCode(499, new { message = "Запрос к помощнику отменён." });
        }
        catch (Exception)
        {
            // A provider/context failure is intentionally not raised as a SecurityEvent/ServerError.
            _logger.LogWarning("AI assistant request could not be completed. Admin {AdminId}; provider {Provider}; model {Model}",
                User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "unknown", _options.Provider, _options.Model);
            return Ok(new AiAssistantResult(false, true, "Помощник временно недоступен. Попробуйте позже."));
        }
    }
}
