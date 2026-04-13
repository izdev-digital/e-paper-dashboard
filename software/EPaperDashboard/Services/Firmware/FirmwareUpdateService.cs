using EPaperDashboard.Utilities;

namespace EPaperDashboard.Services.Firmware;

/// <summary>
/// Background service that periodically checks for firmware updates
/// and caches the latest firmware binary for OTA delivery to devices.
/// </summary>
public sealed class FirmwareUpdateService : BackgroundService
{
    private readonly IFirmwareReleaseProvider _provider;
    private readonly ILogger<FirmwareUpdateService> _logger;
    private readonly TimeSpan _checkInterval;
    private readonly string _firmwareCacheDir;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private FirmwareReleaseInfo? _latestRelease;
    private string? _cachedBinaryPath;

    public FirmwareUpdateService(
        IFirmwareReleaseProvider provider,
        ILogger<FirmwareUpdateService> logger)
    {
        _provider = provider;
        _logger = logger;
        _checkInterval = EnvironmentConfiguration.FirmwareCheckInterval;
        _firmwareCacheDir = Path.Combine(EnvironmentConfiguration.ConfigDir, "firmware-cache");
        Directory.CreateDirectory(_firmwareCacheDir);
    }

    /// <summary>
    /// Gets the latest known firmware release information.
    /// Returns null if no release info has been fetched yet.
    /// </summary>
    public FirmwareReleaseInfo? GetLatestRelease() => _latestRelease;

    /// <summary>
    /// Gets the cached firmware binary, downloading it if necessary.
    /// </summary>
    public async Task<byte[]?> GetFirmwareBinaryAsync(CancellationToken cancellationToken = default)
    {
        if (_latestRelease?.DownloadUrl is null)
            return null;

        // Serve from cache if available
        if (_cachedBinaryPath is not null && File.Exists(_cachedBinaryPath))
            return await File.ReadAllBytesAsync(_cachedBinaryPath, cancellationToken);

        // Download on demand
        return await DownloadAndCacheBinaryAsync(_latestRelease.DownloadUrl, _latestRelease.Version, cancellationToken);
    }

    /// <summary>
    /// Forces an immediate check for firmware updates.
    /// </summary>
    public async Task<FirmwareReleaseInfo?> RefreshAsync(CancellationToken cancellationToken = default)
    {
        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            await CheckForUpdatesAsync(cancellationToken);
            return _latestRelease;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Firmware Update Service started (check interval: {Interval})", _checkInterval);

        // Initial check after a short delay to allow app startup to complete
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _refreshLock.WaitAsync(stoppingToken);
                try
                {
                    await CheckForUpdatesAsync(stoppingToken);
                }
                finally
                {
                    _refreshLock.Release();
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking for firmware updates");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("Firmware Update Service stopped");
    }

    private async Task CheckForUpdatesAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Checking for firmware updates...");

        var release = await _provider.GetLatestReleaseAsync(cancellationToken);
        if (release is null)
        {
            _logger.LogWarning("No firmware release information available from provider");
            return;
        }

        var isNewVersion = _latestRelease is null || _latestRelease.Version != release.Version;
        _latestRelease = release;

        if (isNewVersion)
        {
            _logger.LogInformation("New firmware version detected: {Version}", release.Version);

            // Proactively download and cache the binary for faster device delivery
            if (release.DownloadUrl is not null)
            {
                await DownloadAndCacheBinaryAsync(release.DownloadUrl, release.Version, cancellationToken);
            }
        }
        else
        {
            _logger.LogInformation("Firmware is up to date: {Version}", release.Version);
        }
    }

    private async Task<byte[]?> DownloadAndCacheBinaryAsync(string downloadUrl, string version, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Downloading firmware binary v{Version}...", version);

            var binary = await _provider.DownloadFirmwareBinaryAsync(downloadUrl, cancellationToken);
            if (binary is null)
            {
                _logger.LogWarning("Failed to download firmware binary");
                return null;
            }

            // Clean up old cached binaries
            foreach (var oldFile in Directory.GetFiles(_firmwareCacheDir, "*.bin"))
            {
                try { File.Delete(oldFile); }
                catch { /* best effort cleanup */ }
            }

            // Cache the new binary
            var cachePath = Path.Combine(_firmwareCacheDir, $"firmware-{version}.bin");
            await File.WriteAllBytesAsync(cachePath, binary, cancellationToken);
            _cachedBinaryPath = cachePath;

            _logger.LogInformation("Firmware binary v{Version} cached successfully ({Size} bytes)", version, binary.Length);
            return binary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download and cache firmware binary v{Version}", version);
            return null;
        }
    }
}
