using System.Net.WebSockets;
using System.Text.Json;
using CSharpFunctionalExtensions;

namespace EPaperDashboard.Services.Llm;

/// <summary>
/// LLM provider that uses the Home Assistant conversation API via WebSocket.
/// Only meaningful in Addon/Host deployment mode.
/// </summary>
public sealed class HomeAssistantLlmProvider(
    HomeAssistantConnectionService connectionService,
    string dashboardId) : ILlmProvider
{
    public int TimeoutSeconds => 60;

    public async Task<Result<string, string>> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
    {
        try
        {
            using var ws = await connectionService.ConnectAsync(dashboardId);
            var msgId = connectionService.NextMessageId();

            await HomeAssistantConnectionService.SendMessageAsync(ws, new
            {
                id = msgId,
                type = "conversation/process",
                text = prompt,
                language = "en"
            });

            while (true)
            {
                var json = await HomeAssistantConnectionService.ReceiveMessageAsync(ws);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("id", out var idEl) && idEl.GetInt32() == msgId
                    && root.TryGetProperty("type", out var typeEl) && typeEl.GetString() == "result")
                {
                    if (root.TryGetProperty("success", out var successEl) && successEl.GetBoolean())
                    {
                        var speech = root.TryGetProperty("result", out var resultEl)
                            && resultEl.TryGetProperty("speech", out var speechEl)
                            && speechEl.TryGetProperty("plain", out var plainEl)
                            && plainEl.TryGetProperty("speech", out var speechTextEl)
                            ? speechTextEl.GetString() ?? string.Empty
                            : string.Empty;

                        return Result.Success<string, string>(speech);
                    }
                    else
                    {
                        return Result.Failure<string, string>("Home Assistant conversation failed.");
                    }
                }
            }
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
            using var ws = await connectionService.ConnectAsync(dashboardId);
            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "test", cancellationToken);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result.Failure<bool, string>(ex.Message);
        }
    }
}
