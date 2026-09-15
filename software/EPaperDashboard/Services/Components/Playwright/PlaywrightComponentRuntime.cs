using System.Diagnostics;
using System.Text;
using System.Text.Json;
using EPaperDashboard.RenderingComponent;

namespace EPaperDashboard.Services.Components.Playwright;

// One shared runtime coordinates renders with activation and removal of their files.
public sealed class PlaywrightComponentRuntime
{
    private readonly int _concurrency;
    private readonly int _maximumQueuedRequests;
    private readonly SemaphoreSlim _slots;
    private int _queuedRequests;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public PlaywrightComponentRuntime(IConfiguration? configuration = null)
    {
        _concurrency = ReadLimit(configuration, "RENDERING_MAX_CONCURRENT_RENDERS", 2, 1, 16);
        _maximumQueuedRequests = ReadLimit(configuration, "RENDERING_MAX_QUEUED_RENDERS", 100, 0, 1000);
        _slots = new SemaphoreSlim(_concurrency, _concurrency);
    }

    private static int ReadLimit(IConfiguration? configuration, string key, int fallback, int minimum, int maximum)
    {
        var value = configuration?[key];
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        if (!int.TryParse(value, out var limit) || limit < minimum || limit > maximum)
            throw new InvalidOperationException($"{key} must be between {minimum} and {maximum}.");
        return limit;
    }

    internal async Task<IDisposable> AcquireRenderAsync(CancellationToken cancellationToken)
    {
        if (_slots.Wait(0, cancellationToken)) return new Lease(() => _slots.Release());
        if (Interlocked.Increment(ref _queuedRequests) > _maximumQueuedRequests)
        {
            Interlocked.Decrement(ref _queuedRequests);
            throw new InvalidOperationException("The rendering queue is full. Please try again later.");
        }
        try
        {
            await _slots.WaitAsync(cancellationToken);
            return new Lease(() => _slots.Release());
        }
        finally
        {
            Interlocked.Decrement(ref _queuedRequests);
        }
    }

    internal async Task<IDisposable> AcquireExclusiveAsync(CancellationToken cancellationToken)
    {
        var acquired = 0;
        try
        {
            for (; acquired < _concurrency; acquired++)
                await _slots.WaitAsync(cancellationToken);
            return new Lease(() => _slots.Release(_concurrency));
        }
        catch
        {
            if (acquired > 0) _slots.Release(acquired);
            throw;
        }
    }

    // Caller holds either a render or exclusive lease for the entire operation.
    internal async Task<byte[]> RunAsync(
        ActivePlaywrightComponent component, RenderRequest request, CancellationToken cancellationToken)
    {
        request.Validate();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(150));
        var token = timeout.Token;
        var directory = Path.Combine(Path.GetTempPath(), $"izboard-render-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var outputPath = Path.Combine(directory, "image");
        using var process = new Process { StartInfo = CreateStartInfo(component, directory) };
        var started = false;
        Task<string>? output = null;
        Task<string>? error = null;
        try
        {
            started = process.Start();
            if (!started) throw new InvalidOperationException("The rendering component could not be started.");
            output = ReadBoundedAsync(process.StandardOutput, token);
            error = ReadBoundedAsync(process.StandardError, token);
            await process.StandardInput.WriteAsync(
                JsonSerializer.Serialize(request with { OutputPath = outputPath }, JsonOptions).AsMemory(), token);
            process.StandardInput.Close();
            await process.WaitForExitAsync(token);
            var response = JsonSerializer.Deserialize<RenderResponse>(await output, JsonOptions);
            await error;
            if (response?.ProtocolVersion != 1)
                throw new InvalidOperationException("The rendering component protocol is incompatible.");
            if (process.ExitCode != 0 || response.Success != true)
                throw new InvalidOperationException(response.Error ?? "The rendering component failed.");
            var file = new FileInfo(outputPath);
            if (!file.Exists || file.Length <= 0 || file.Length > RenderRequest.MaximumImageBytes)
                throw new InvalidOperationException("The rendering component produced a missing or oversized image.");
            return await File.ReadAllBytesAsync(outputPath, token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("The rendering component exceeded its 150 second timeout.");
        }
        finally
        {
            // Covers stdin failures, invalid responses, cancellation, and timeout alike.
            try
            {
                if (started)
                {
                    try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
                    catch (InvalidOperationException) when (process.HasExited) { /* exited during the check */ }
                    await process.WaitForExitAsync(CancellationToken.None);
                }
            }
            finally
            {
                timeout.Cancel();
                if (output is not null) { try { await output; } catch { /* observed during cleanup */ } }
                if (error is not null) { try { await error; } catch { /* observed during cleanup */ } }
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static async Task<string> ReadBoundedAsync(StreamReader reader, CancellationToken token)
    {
        var text = new StringBuilder();
        var buffer = new char[4096];
        int count;
        while ((count = await reader.ReadAsync(buffer.AsMemory(), token)) != 0)
        {
            if (text.Length + count > 64_000)
                throw new InvalidOperationException("The rendering component response exceeds the allowed size.");
            text.Append(buffer, 0, count);
        }
        return text.ToString();
    }

    private static ProcessStartInfo CreateStartInfo(ActivePlaywrightComponent component, string temporaryDirectory)
    {
        var info = new ProcessStartInfo
        {
            FileName = "dotnet", WorkingDirectory = component.RootPath,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            UseShellExecute = false, CreateNoWindow = true
        };
        info.ArgumentList.Add(component.WorkerAssemblyPath);
        // Do not pass application configuration, signing keys, or other users' credentials.
        info.Environment.Clear();
        foreach (var key in new[] { "PATH", "DOTNET_ROOT", "LANG", "LC_ALL", "TZ", "SSL_CERT_FILE", "SSL_CERT_DIR" })
            if (Environment.GetEnvironmentVariable(key) is { } value) info.Environment[key] = value;
        info.Environment["HOME"] = temporaryDirectory;
        info.Environment["TMPDIR"] = temporaryDirectory;
        info.Environment["DOTNET_EnableDiagnostics"] = "0";
        info.Environment["PLAYWRIGHT_BROWSERS_PATH"] = component.BrowserPath;
        info.Environment["IZBOARD_PLAYWRIGHT_LIBRARY_PATH"] = component.LibraryPath;
        if (component.DataPath is not null) info.Environment["IZBOARD_PLAYWRIGHT_DATA_PATH"] = component.DataPath;
        if (component.FontConfigPath is not null) info.Environment["IZBOARD_PLAYWRIGHT_FONTCONFIG_PATH"] = component.FontConfigPath;
        return info;
    }

    private sealed class Lease(Action release) : IDisposable
    {
        private Action? _release = release;
        public void Dispose() => Interlocked.Exchange(ref _release, null)?.Invoke();
    }
}
