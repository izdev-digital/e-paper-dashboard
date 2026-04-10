using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CSharpFunctionalExtensions;

namespace EPaperDashboard.Services.Ai;

/// <summary>
/// AI service implementation using OpenAI-compatible chat completion APIs.
/// Works with OpenAI, Azure OpenAI, Ollama, LM Studio, and other compatible endpoints.
/// </summary>
public sealed class DirectAiService(
    IHttpClientFactory httpClientFactory,
    string endpoint,
    string apiKey,
    string model,
    ILogger<DirectAiService> logger) : IAiService
{
    public async Task<Result<string, string>> GenerateCompletionAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default)
    {
        var firstAttempt = await TrySendRequestAsync(systemPrompt, userPrompt, cancellationToken);
        if (firstAttempt.IsSuccess)
            return firstAttempt;

        // Retry once for transient / rate-limit failures
        if (firstAttempt.Error.Contains("status 429") || firstAttempt.Error.Contains("status 5"))
        {
            logger.LogWarning("Retrying AI request after transient failure: {Error}", firstAttempt.Error);
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            return await TrySendRequestAsync(systemPrompt, userPrompt, cancellationToken);
        }

        return firstAttempt;
    }

    private async Task<Result<string, string>> TrySendRequestAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(120);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var requestBody = new
        {
            model,
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            temperature = 0.7,
            response_format = new { type = "json_object" }
        };

        var requestJson = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

        // Normalize endpoint to end with /chat/completions
        var url = endpoint.TrimEnd('/');
        if (!url.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            if (!url.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
            {
                url += "/v1";
            }
            url += "/chat/completions";
        }

        try
        {
            logger.LogInformation("Calling Direct AI endpoint: {Url} with model: {Model}", url, model);

            var response = await client.PostAsync(url, content, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Direct AI request failed with status {StatusCode}: {Body}",
                    response.StatusCode, responseBody);
                return Result.Failure<string, string>($"AI request failed with status {response.StatusCode}");
            }

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            if (root.TryGetProperty("choices", out var choices)
                && choices.GetArrayLength() > 0)
            {
                var firstChoice = choices[0];
                if (firstChoice.TryGetProperty("message", out var message)
                    && message.TryGetProperty("content", out var msgContent))
                {
                    var result = msgContent.GetString();
                    if (!string.IsNullOrEmpty(result))
                    {
                        logger.LogInformation("Direct AI returned response ({Length} chars)", result.Length);
                        return Result.Success<string, string>(result);
                    }
                }
            }

            return Result.Failure<string, string>("AI returned an empty or unexpected response");
        }
        catch (OperationCanceledException)
        {
            return Result.Failure<string, string>("AI request was cancelled");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Direct AI request failed");
            return Result.Failure<string, string>($"AI request failed: {ex.Message}");
        }
    }
}
