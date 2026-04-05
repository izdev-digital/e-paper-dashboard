using System.Net.Http.Json;
using System.Text.Json;
using CSharpFunctionalExtensions;
using EPaperDashboard.Models;

namespace EPaperDashboard.Services.Llm;

/// <summary>
/// LLM provider that uses the Ollama native API.
/// Generates via POST {url}/api/generate, tests connectivity via GET {url}/api/tags.
/// </summary>
public sealed class OllamaLlmProvider(IHttpClientFactory httpClientFactory, UserLlmConfig config) : ILlmProvider
{
    public int TimeoutSeconds => config.TimeoutSeconds > 0 ? config.TimeoutSeconds : 60;

    public async Task<Result<string, string>> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds > 0 ? config.TimeoutSeconds : 60);

            var baseUrl = config.BaseUrl.TrimEnd('/');
            var body = new
            {
                model = config.Model,
                prompt,
                stream = false,
                options = new { temperature = config.Temperature }
            };

            var response = await client.PostAsJsonAsync($"{baseUrl}/api/generate", body, cancellationToken);
            response.EnsureSuccessStatusCode();

            using var doc = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: cancellationToken);
            var text = doc?.RootElement.TryGetProperty("response", out var r) == true
                ? r.GetString() ?? string.Empty
                : string.Empty;

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

            var baseUrl = config.BaseUrl.TrimEnd('/');
            var response = await client.GetAsync($"{baseUrl}/api/tags", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result.Failure<bool, string>(ex.Message);
        }
    }
}
