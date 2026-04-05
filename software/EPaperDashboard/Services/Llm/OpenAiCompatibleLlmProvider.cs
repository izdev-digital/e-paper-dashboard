using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CSharpFunctionalExtensions;
using EPaperDashboard.Models;

namespace EPaperDashboard.Services.Llm;

/// <summary>
/// LLM provider that uses an OpenAI-compatible API.
/// Works with OpenAI, LocalAI, LM Studio, vLLM, and other compatible servers.
/// Generates via POST {url}/v1/chat/completions, tests connectivity via GET {url}/v1/models.
/// </summary>
public sealed class OpenAiCompatibleLlmProvider(IHttpClientFactory httpClientFactory, UserLlmConfig config) : ILlmProvider
{
    public int TimeoutSeconds => config.TimeoutSeconds > 0 ? config.TimeoutSeconds : 60;

    public async Task<Result<string, string>> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds > 0 ? config.TimeoutSeconds : 60);

            if (!string.IsNullOrWhiteSpace(config.PlainApiKey))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config.PlainApiKey);
            }

            var baseUrl = config.BaseUrl.TrimEnd('/');
            var body = new
            {
                model = config.Model,
                messages = new[] { new { role = "user", content = prompt } },
                temperature = config.Temperature
            };

            var response = await client.PostAsJsonAsync($"{baseUrl}/v1/chat/completions", body, cancellationToken);
            response.EnsureSuccessStatusCode();

            using var doc = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: cancellationToken);
            var text = string.Empty;
            if (doc?.RootElement.TryGetProperty("choices", out var choices) == true
                && choices.ValueKind == JsonValueKind.Array
                && choices.GetArrayLength() > 0
                && choices[0].TryGetProperty("message", out var msg)
                && msg.TryGetProperty("content", out var content))
            {
                text = content.GetString() ?? string.Empty;
            }

            return Result.Success<string, string>(text);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result.Failure<string, string>(ex.Message);
        }
    }

    public async Task<Result<bool, string>> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);

            if (!string.IsNullOrWhiteSpace(config.PlainApiKey))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config.PlainApiKey);
            }

            var baseUrl = config.BaseUrl.TrimEnd('/');
            var response = await client.GetAsync($"{baseUrl}/v1/models", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result.Failure<bool, string>(ex.Message);
        }
    }
}
