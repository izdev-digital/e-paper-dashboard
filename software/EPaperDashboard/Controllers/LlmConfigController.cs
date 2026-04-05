using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using EPaperDashboard.Data.Repositories;
using EPaperDashboard.Models;
using EPaperDashboard.Services.Llm;

namespace EPaperDashboard.Controllers;

[ApiController]
[Route("api/llm")]
[Authorize]
public class LlmConfigController(
    IUserLlmConfigRepository configRepository,
    ILlmProviderFactory providerFactory,
    IDataProtectionProvider dataProtectionProvider) : BaseApiController
{
    private readonly IDataProtector _dataProtector =
        dataProtectionProvider.CreateProtector("EPaperDashboard.LlmApiKey");

    // ─── GET /api/llm/config ─────────────────────────────────────────────
    [HttpGet("config")]
    public IActionResult GetConfig()
    {
        var config = configRepository.FindByUserId(CurrentUserId);

        if (config.HasNoValue)
        {
            return Ok(new LlmConfigResponse
            {
                Enabled = false,
                ProviderType = "none",
                BaseUrl = string.Empty,
                Model = string.Empty,
                HasApiKey = false,
                Temperature = 0.1,
                TimeoutSeconds = 60
            });
        }

        return Ok(new LlmConfigResponse
        {
            Enabled = config.Value.Enabled,
            ProviderType = config.Value.ProviderType,
            BaseUrl = config.Value.BaseUrl,
            Model = config.Value.Model,
            HasApiKey = !string.IsNullOrWhiteSpace(config.Value.EncryptedApiKey),
            Temperature = config.Value.Temperature,
            TimeoutSeconds = config.Value.TimeoutSeconds
        });
    }

    // ─── PUT /api/llm/config ─────────────────────────────────────────────
    [HttpPut("config")]
    public IActionResult SaveConfig([FromBody] UpdateLlmConfigRequest request)
    {
        var existing = configRepository.FindByUserId(CurrentUserId);

        var entry = existing.HasValue ? existing.Value : new UserLlmConfig { UserId = CurrentUserId };

        entry.Enabled = request.Enabled;
        entry.ProviderType = request.ProviderType ?? "none";
        entry.BaseUrl = request.BaseUrl ?? string.Empty;
        entry.Model = request.Model ?? string.Empty;
        entry.Temperature = Math.Clamp(request.Temperature, 0.0, 2.0);
        entry.TimeoutSeconds = Math.Clamp(request.TimeoutSeconds, 1, 300);

        // Only update the stored API key when a new non-empty value is provided
        if (!string.IsNullOrWhiteSpace(request.ApiKey))
        {
            entry.EncryptedApiKey = _dataProtector.Protect(request.ApiKey);
        }
        else if (request.ClearApiKey)
        {
            entry.EncryptedApiKey = null;
        }

        configRepository.Upsert(entry);

        return Ok(new LlmConfigResponse
        {
            Enabled = entry.Enabled,
            ProviderType = entry.ProviderType,
            BaseUrl = entry.BaseUrl,
            Model = entry.Model,
            HasApiKey = !string.IsNullOrWhiteSpace(entry.EncryptedApiKey),
            Temperature = entry.Temperature,
            TimeoutSeconds = entry.TimeoutSeconds
        });
    }

    // ─── POST /api/llm/test-connection ───────────────────────────────────
    [HttpPost("test-connection")]
    public async Task<IActionResult> TestConnection()
    {
        var provider = providerFactory.GetProvider(CurrentUserId);

        if (provider is NoOpLlmProvider)
        {
            return BadRequest(new { message = "AI provider is not configured or not enabled." });
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var result = await provider.TestConnectionAsync(cts.Token);

        if (result.IsSuccess && result.Value)
        {
            return Ok(new { success = true, message = "Connection successful." });
        }

        return BadRequest(new { success = false, message = result.IsFailure ? result.Error : "Connection failed." });
    }
}

// ─── DTOs ────────────────────────────────────────────────────────────────────

public sealed class LlmConfigResponse
{
    public bool Enabled { get; set; }
    public string ProviderType { get; set; } = "none";
    public string BaseUrl { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public bool HasApiKey { get; set; }
    public double Temperature { get; set; }
    public int TimeoutSeconds { get; set; }
}

public sealed class UpdateLlmConfigRequest
{
    public bool Enabled { get; set; }
    public string? ProviderType { get; set; }
    public string? BaseUrl { get; set; }
    public string? Model { get; set; }
    /// <summary>Provide to set a new API key. Leave null/empty to keep the existing key.</summary>
    public string? ApiKey { get; set; }
    /// <summary>When true, removes the stored API key.</summary>
    public bool ClearApiKey { get; set; }
    public double Temperature { get; set; } = 0.1;
    public int TimeoutSeconds { get; set; } = 60;
}
