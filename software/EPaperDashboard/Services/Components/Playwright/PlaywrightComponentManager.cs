using System.Formats.Tar;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using EPaperDashboard.Utilities;

namespace EPaperDashboard.Services.Components.Playwright;

public sealed class PlaywrightComponentManager
{
    private const int ManifestSchemaVersion = 1;
    private const long MaximumComponentBytes = 1_500_000_000;
    private const long MaximumExtractedBytes = 3_000_000_000;
    private const string DefaultRepository = "izdev-digital/e-paper-dashboard";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PlaywrightComponentManager> _logger;
    private readonly string _componentRoot;
    private readonly string _repository;
    private readonly string _releaseTag;
    private readonly Uri? _componentBaseUri;
    private readonly object _statusLock = new();
    private Task? _installationTask;
    private PlaywrightComponentStatus _status;

    public PlaywrightComponentManager(
        IHttpClientFactory httpClientFactory,
        IEnvironmentConfiguration environmentConfiguration,
        IConfiguration configuration,
        ILogger<PlaywrightComponentManager> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _componentRoot = Path.Combine(environmentConfiguration.ConfigDir, "components", "playwright");
        _repository = configuration["PLAYWRIGHT_COMPONENT_REPOSITORY"] ?? DefaultRepository;
        _releaseTag = configuration["PLAYWRIGHT_COMPONENT_RELEASE_TAG"] ?? GetDefaultReleaseTag(Constants.AppVersion);
        _componentBaseUri = ParseComponentBaseUri(configuration["PLAYWRIGHT_COMPONENT_BASE_URL"]);
        CleanupInterruptedInstallations();
        _status = ReadInstalledStatus();
    }

    public PlaywrightComponentStatus GetStatus()
    {
        lock (_statusLock)
        {
            return _status;
        }
    }

    public bool StartInstallation()
    {
        if (!IsRuntimeSupported())
            throw new PlatformNotSupportedException("The Playwright component is currently available for Linux x64 and arm64 only.");

        lock (_statusLock)
        {
            if (_installationTask is { IsCompleted: false })
                return false;

            _status = NewStatus(PlaywrightComponentState.Installing);
            _installationTask = InstallCoreAsync();
            return true;
        }
    }

    public Task UninstallAsync()
    {
        lock (_statusLock)
        {
            if (_installationTask is { IsCompleted: false })
                throw new InvalidOperationException("The Playwright component is currently being installed.");
        }

        if (Directory.Exists(_componentRoot))
            Directory.Delete(_componentRoot, recursive: true);

        SetStatus(NewStatus(PlaywrightComponentState.NotInstalled));
        _logger.LogInformation("Uninstalled the Playwright component");
        return Task.CompletedTask;
    }

    internal ActivePlaywrightComponent? GetActiveComponent()
    {
        var status = GetStatus();
        if (status.State != PlaywrightComponentState.Installed)
            return null;

        try
        {
            return ReadAndValidateComponent(GetActiveDirectory());
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "The installed Playwright component is invalid");
            SetStatus(NewStatus(PlaywrightComponentState.Failed, error: exception.Message));
            return null;
        }
    }

    private async Task InstallCoreAsync()
    {
        var operationDirectory = Path.Combine(_componentRoot, $".install-{Guid.NewGuid():N}");
        var archivePath = Path.Combine(operationDirectory, "component.tar.gz");
        var extractPath = Path.Combine(operationDirectory, "extracted");

        try
        {
            Directory.CreateDirectory(_componentRoot);
            Directory.CreateDirectory(operationDirectory);
            var runtimeIdentifier = GetRuntimeIdentifier();
            var assetName = $"playwright-component-{runtimeIdentifier}.tar.gz";
            var (archiveUrl, checksumUrl) = await ResolveAssetUrlsAsync(assetName);

            var expectedChecksum = await DownloadChecksumAsync(checksumUrl);
            await DownloadArchiveAsync(archiveUrl, archivePath);
            await VerifyChecksumAsync(archivePath, expectedChecksum);

            Directory.CreateDirectory(extractPath);
            await ExtractArchiveAsync(archivePath, extractPath);
            _ = ReadAndValidateComponent(extractPath);
            Activate(extractPath);

            var active = ReadAndValidateComponent(GetActiveDirectory());
            SetStatus(NewStatus(
                PlaywrightComponentState.Installed,
                installedVersion: active.Manifest.ComponentVersion));
            _logger.LogInformation(
                "Installed Playwright component {ComponentVersion} for {RuntimeIdentifier}",
                active.Manifest.ComponentVersion,
                runtimeIdentifier);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to install the Playwright component");
            SetStatus(NewStatus(PlaywrightComponentState.Failed, error: exception.Message));
        }
        finally
        {
            if (Directory.Exists(operationDirectory))
            {
                try
                {
                    Directory.Delete(operationDirectory, recursive: true);
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(exception, "Could not remove temporary component files at {Path}", operationDirectory);
                }
            }
        }
    }

    private async Task<(string ArchiveUrl, string ChecksumUrl)> ResolveAssetUrlsAsync(string assetName)
    {
        if (_componentBaseUri is not null)
        {
            _logger.LogInformation("Installing the Playwright component from {BaseUri}", _componentBaseUri);
            return (
                new Uri(_componentBaseUri, Uri.EscapeDataString(assetName)).AbsoluteUri,
                new Uri(_componentBaseUri, Uri.EscapeDataString($"{assetName}.sha256")).AbsoluteUri);
        }

        var release = await GetReleaseAsync();
        return (
            FindAssetUrl(release, assetName),
            FindAssetUrl(release, $"{assetName}.sha256"));
    }

    private async Task<JsonElement> GetReleaseAsync()
    {
        var client = _httpClientFactory.CreateClient(Constants.PlaywrightComponentHttpClientName);
        var url = $"https://api.github.com/repos/{_repository}/releases/tags/{Uri.EscapeDataString(_releaseTag)}";
        using var response = await client.GetAsync(url);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"GitHub release '{_releaseTag}' is unavailable ({(int)response.StatusCode}).");

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        return document.RootElement.Clone();
    }

    private static string FindAssetUrl(JsonElement release, string assetName)
    {
        if (release.TryGetProperty("assets", out var assets))
        {
            foreach (var asset in assets.EnumerateArray())
            {
                if (asset.TryGetProperty("name", out var name)
                    && string.Equals(name.GetString(), assetName, StringComparison.Ordinal)
                    && asset.TryGetProperty("browser_download_url", out var url)
                    && !string.IsNullOrWhiteSpace(url.GetString()))
                    return url.GetString()!;
            }
        }

        throw new InvalidOperationException($"Release '{release.GetProperty("tag_name").GetString()}' does not contain {assetName}.");
    }

    private async Task<string> DownloadChecksumAsync(string url)
    {
        var client = _httpClientFactory.CreateClient(Constants.PlaywrightComponentHttpClientName);
        var checksumText = await client.GetStringAsync(url);
        var checksum = checksumText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (checksum is null || checksum.Length != 64 || !checksum.All(Uri.IsHexDigit))
            throw new InvalidOperationException("The component checksum is invalid.");
        return checksum.ToUpperInvariant();
    }

    private async Task DownloadArchiveAsync(string url, string destination)
    {
        var client = _httpClientFactory.CreateClient(Constants.PlaywrightComponentHttpClientName);
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength;
        if (totalBytes is > MaximumComponentBytes)
            throw new InvalidOperationException("The component archive exceeds the allowed size.");

        UpdateProgress(0, totalBytes);
        await using var input = await response.Content.ReadAsStreamAsync();
        await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81_920, true);
        var buffer = new byte[81_920];
        long downloaded = 0;

        while (true)
        {
            var read = await input.ReadAsync(buffer);
            if (read == 0)
                break;

            downloaded += read;
            if (downloaded > MaximumComponentBytes)
                throw new InvalidOperationException("The component archive exceeds the allowed size.");

            await output.WriteAsync(buffer.AsMemory(0, read));
            UpdateProgress(downloaded, totalBytes);
        }
    }

    private static async Task VerifyChecksumAsync(string path, string expectedChecksum)
    {
        await using var stream = File.OpenRead(path);
        var actualChecksum = Convert.ToHexString(await SHA256.HashDataAsync(stream));
        if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(actualChecksum),
                Convert.FromHexString(expectedChecksum)))
            throw new InvalidOperationException("The downloaded component failed checksum verification.");
    }

    private static async Task ExtractArchiveAsync(string archivePath, string destination)
    {
        var destinationRoot = Path.GetFullPath(destination) + Path.DirectorySeparatorChar;
        await using var file = File.OpenRead(archivePath);
        await using var gzip = new GZipStream(file, CompressionMode.Decompress);
        using var reader = new TarReader(gzip);
        long extractedBytes = 0;

        while (await reader.GetNextEntryAsync() is { } entry)
        {
            var destinationPath = Path.GetFullPath(Path.Combine(destination, entry.Name));
            if (!string.Equals(destinationPath, destinationRoot.TrimEnd(Path.DirectorySeparatorChar), StringComparison.Ordinal)
                && !destinationPath.StartsWith(destinationRoot, StringComparison.Ordinal))
                throw new InvalidOperationException("The component archive contains an unsafe path.");

            switch (entry.EntryType)
            {
                case TarEntryType.Directory:
                    Directory.CreateDirectory(destinationPath);
                    break;
                case TarEntryType.RegularFile:
                case TarEntryType.V7RegularFile:
                    extractedBytes += entry.Length;
                    if (extractedBytes > MaximumExtractedBytes)
                        throw new InvalidOperationException("The extracted component exceeds the allowed size.");
                    Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                    await using (var output = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write))
                    {
                        if (entry.DataStream is not null)
                            await entry.DataStream.CopyToAsync(output);
                    }
                    if (!OperatingSystem.IsWindows())
                        File.SetUnixFileMode(destinationPath, entry.Mode);
                    break;
                default:
                    throw new InvalidOperationException($"The component archive contains unsupported entry type {entry.EntryType}.");
            }
        }
    }

    private void Activate(string extractedPath)
    {
        var activePath = GetActiveDirectory();
        var previousPath = Path.Combine(_componentRoot, $".previous-{Guid.NewGuid():N}");
        var hadPrevious = Directory.Exists(activePath);

        if (hadPrevious)
            Directory.Move(activePath, previousPath);

        try
        {
            Directory.Move(extractedPath, activePath);
            if (hadPrevious)
            {
                try
                {
                    Directory.Delete(previousPath, recursive: true);
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(exception, "Could not remove the previous Playwright component at {Path}", previousPath);
                }
            }
        }
        catch
        {
            if (!Directory.Exists(activePath) && Directory.Exists(previousPath))
                Directory.Move(previousPath, activePath);
            throw;
        }
    }

    private PlaywrightComponentStatus ReadInstalledStatus()
    {
        if (!Directory.Exists(GetActiveDirectory()))
            return NewStatus(PlaywrightComponentState.NotInstalled);

        try
        {
            var active = ReadAndValidateComponent(GetActiveDirectory(), requireCompatibleVersion: false);
            return string.Equals(active.Manifest.AppVersion, GetCompatibilityVersion(Constants.AppVersion), StringComparison.Ordinal)
                ? NewStatus(PlaywrightComponentState.Installed, installedVersion: active.Manifest.ComponentVersion)
                : NewStatus(
                    PlaywrightComponentState.Incompatible,
                    installedVersion: active.Manifest.ComponentVersion,
                    error: $"Component targets izBoard {active.Manifest.AppVersion}; {GetCompatibilityVersion(Constants.AppVersion)} is required.");
        }
        catch (Exception exception)
        {
            return NewStatus(PlaywrightComponentState.Failed, error: exception.Message);
        }
    }

    private static ActivePlaywrightComponent ReadAndValidateComponent(string root, bool requireCompatibleVersion = true)
    {
        var manifestPath = Path.Combine(root, "component-manifest.json");
        if (!File.Exists(manifestPath))
            throw new InvalidOperationException("The component manifest is missing.");

        var manifest = JsonSerializer.Deserialize<PlaywrightComponentManifest>(File.ReadAllText(manifestPath), JsonOptions)
            ?? throw new InvalidOperationException("The component manifest is invalid.");
        if (manifest.SchemaVersion != ManifestSchemaVersion)
            throw new InvalidOperationException($"Unsupported component manifest schema {manifest.SchemaVersion}.");
        if (!string.Equals(manifest.RuntimeIdentifier, GetRuntimeIdentifier(), StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"The component targets {manifest.RuntimeIdentifier}, not {GetRuntimeIdentifier()}.");
        var compatibilityVersion = GetCompatibilityVersion(Constants.AppVersion);
        if (requireCompatibleVersion && !string.Equals(manifest.AppVersion, compatibilityVersion, StringComparison.Ordinal))
            throw new InvalidOperationException($"The component targets izBoard {manifest.AppVersion}, not {compatibilityVersion}.");

        var worker = ResolveComponentPath(root, manifest.WorkerAssembly);
        var browser = ResolveComponentPath(root, manifest.BrowserPath);
        var libraries = ResolveComponentPath(root, manifest.LibraryPath);
        var data = manifest.DataPath is null ? null : ResolveComponentPath(root, manifest.DataPath);
        var fontConfig = manifest.FontConfigPath is null ? null : ResolveComponentPath(root, manifest.FontConfigPath);
        if (!File.Exists(worker) || !Directory.Exists(browser) || !Directory.Exists(libraries))
            throw new InvalidOperationException("The component payload is incomplete.");

        return new ActivePlaywrightComponent(root, worker, browser, libraries, data, fontConfig, manifest);
    }

    private static string ResolveComponentPath(string root, string relativePath)
    {
        if (Path.IsPathFullyQualified(relativePath))
            throw new InvalidOperationException("Component paths must be relative.");

        var rootPrefix = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(root, relativePath));
        if (!fullPath.StartsWith(rootPrefix, StringComparison.Ordinal))
            throw new InvalidOperationException("The component manifest contains an unsafe path.");
        return fullPath;
    }

    private string GetActiveDirectory() => Path.Combine(_componentRoot, "current");

    private void CleanupInterruptedInstallations()
    {
        try
        {
            if (!Directory.Exists(_componentRoot))
                return;

            foreach (var directory in Directory.EnumerateDirectories(_componentRoot, ".install-*"))
            {
                try
                {
                    Directory.Delete(directory, recursive: true);
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(exception, "Could not clean interrupted component installation at {Path}", directory);
                }
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Could not inspect interrupted Playwright component installations");
        }
    }

    private void SetStatus(PlaywrightComponentStatus status)
    {
        lock (_statusLock)
        {
            _status = status;
        }
    }

    private void UpdateProgress(long bytesDownloaded, long? totalBytes)
    {
        lock (_statusLock)
        {
            _status = _status with { BytesDownloaded = bytesDownloaded, TotalBytes = totalBytes };
        }
    }

    private static PlaywrightComponentStatus NewStatus(
        PlaywrightComponentState state,
        string? installedVersion = null,
        string? error = null) =>
        new(state, Constants.AppVersion, GetRuntimeIdentifier(), IsRuntimeSupported(), installedVersion, Error: error);

    internal static string GetDefaultReleaseTag(string appVersion)
    {
        var cleanVersion = appVersion.Split('+', 2)[0];
        if (!Version.TryParse(cleanVersion, out var version))
            return "dev";
        return version.Revision > 0 ? "dev" : $"v{version.Major}.{version.Minor}.{version.Build}";
    }

    internal static string GetCompatibilityVersion(string appVersion)
    {
        var cleanVersion = appVersion.Split('+', 2)[0];
        if (!Version.TryParse(cleanVersion, out var version) || version.Build < 0)
            return cleanVersion;
        return $"{version.Major}.{version.Minor}.{version.Build}";
    }

    internal static string GetRuntimeIdentifier()
    {
        var architecture = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            _ => RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant()
        };
        var os = OperatingSystem.IsLinux()
            ? "linux"
            : OperatingSystem.IsMacOS()
                ? "osx"
                : OperatingSystem.IsWindows() ? "windows" : "unsupported";
        return $"{os}-{architecture}";
    }

    private static Uri? ParseComponentBaseUri(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (!Uri.TryCreate($"{value.TrimEnd('/')}/", UriKind.Absolute, out var uri)
            || uri.Scheme is not (Uri.UriSchemeHttp or Uri.UriSchemeHttps))
            throw new InvalidOperationException("PLAYWRIGHT_COMPONENT_BASE_URL must be an absolute HTTP or HTTPS URL.");

        return uri;
    }

    private static bool IsRuntimeSupported() =>
        OperatingSystem.IsLinux()
        && RuntimeInformation.ProcessArchitecture is Architecture.X64 or Architecture.Arm64;
}
