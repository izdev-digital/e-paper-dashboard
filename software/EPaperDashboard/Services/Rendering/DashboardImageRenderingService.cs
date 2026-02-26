using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;
using EPaperDashboard.Models.Rendering;
using EPaperDashboard.Utilities;
using QRCoder;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Color = SixLabors.ImageSharp.Color;
using PointF = SixLabors.ImageSharp.PointF;
using RectangleF = SixLabors.ImageSharp.RectangleF;
using Size = EPaperDashboard.Models.Rendering.Size;

namespace EPaperDashboard.Services.Rendering;

/// <summary>
/// Renders a custom dashboard layout directly to an ImageSharp image,
/// without generating HTML or using Playwright. Uses the same parsed
/// layout and fetched HA data as the HTML rendering service.
/// </summary>
public sealed class DashboardImageRenderingService
{
    private readonly HomeAssistantService _homeAssistantService;
    private readonly ILogger<DashboardImageRenderingService> _logger;
    private readonly IWebHostEnvironment _env;
    private readonly FontFamily _fontFamily;

    /// <summary>
    /// FA icon registry: name → (svgPathData, viewBoxWidth, viewBoxHeight).
    /// Stripped "fa-" prefix is used as key.
    /// </summary>
    private static readonly Dictionary<string, (string Path, float VbW, float VbH)> FaIcons = new(StringComparer.OrdinalIgnoreCase)
    {
        ["temperature-half"] = ("M160 0C107 0 64 43 64 96l0 164.7C34.5 287 16 325.4 16 368 16 447.5 80.5 512 160 512s144-64.5 144-144c0-42.6-18.5-81-48-107.3L256 96c0-53-43-96-96-96zm64 368c0 35.3-28.7 64-64 64s-64-28.7-64-64c0-26.9 16.5-49.9 40-59.3l0-92.7c0-13.3 10.7-24 24-24s24 10.7 24 24l0 92.7c23.5 9.5 40 32.5 40 59.3z", 320, 512),
        ["cloud-sun"] = ("M453.6-14.8c4.9 2 8.5 6.4 9.5 11.6L480 80 563.2 96.8c5.2 1.1 9.5 4.6 11.6 9.5s1.5 10.5-1.4 14.9l-46.9 70.7 46.9 70.7c2.9 4.4 3.5 10 1.4 14.9s-6.4 8.5-11.6 9.5L501 299.7c-11.9-8.7-25.1-15.6-39.4-20.4-2.5-12.7-6.8-24.7-12.7-35.7 9.5-14.9 15.1-32.6 15.1-51.7 0-53-43-96-96-96-47.9 0-87.6 35.1-94.8 80.9-26.5-20.3-59.5-32.5-95.4-32.9l-15.1-22.7c-2.9-4.4-3.5-10-1.4-14.9s6.4-8.5 11.6-9.5L256 80 272.8-3.2c1.1-5.2 4.6-9.5 9.5-11.6s10.5-1.5 14.9 1.4L368 33.6 438.7-13.3c4.4-2.9 10-3.5 14.9-1.4zM416 192c0 3.8-.4 7.5-1.3 11.1-21.7-17-49-27.1-78.7-27.1-4.6 0-9.1 .2-13.5 .7 6.4-19 24.4-32.7 45.5-32.7 26.5 0 48 21.5 48 48zM96 512c-53 0-96-43-96-96 0-42.5 27.6-78.6 65.9-91.2-1.3-6.7-1.9-13.7-1.9-20.8 0-61.9 50.1-112 112-112 43.1 0 80.5 24.3 99.2 60 14.7-17.1 36.5-28 60.8-28 44.2 0 80 35.8 80 80 0 5.5-.6 10.8-1.6 16 .5 0 1.1 0 1.6 0 53 0 96 43 96 96s-43 96-96 96L96 512z", 576, 512),
        ["gauge"] = ("M0 256a256 256 0 1 1 512 0 256 256 0 1 1 -512 0zm320 96c0-26.9-16.5-49.9-40-59.3L280 120c0-13.3-10.7-24-24-24s-24 10.7-24 24l0 172.7c-23.5 9.5-40 32.5-40 59.3 0 35.3 28.7 64 64 64s64-28.7 64-64zM144 176a32 32 0 1 0 0-64 32 32 0 1 0 0 64zm-16 80a32 32 0 1 0 -64 0 32 32 0 1 0 64 0zm288 32a32 32 0 1 0 0-64 32 32 0 1 0 0 64zM400 144a32 32 0 1 0 -64 0 32 32 0 1 0 64 0z", 512, 512),
        ["droplet"] = ("M192 512C86 512 0 426 0 320 0 228.8 130.2 45.9 166.6-3.5 172.5-11.5 181.8-16 191.8-16l.4 0c10 0 19.3 4.5 25.2 12.5 36.4 49.4 166.6 232.3 166.6 323.5 0 106-86 192-192 192zM112 312c0-13.3-10.7-24-24-24s-24 10.7-24 24c0 75.1 60.9 136 136 136 13.3 0 24-10.7 24-24s-10.7-24-24-24c-48.6 0-88-39.4-88-88z", 384, 512),
        ["clock"] = ("M256 0a256 256 0 1 1 0 512 256 256 0 1 1 0-512zM232 120l0 136c0 8 4 15.5 10.7 20l96 64c11 7.4 25.9 4.4 33.3-6.7s4.4-25.9-6.7-33.3L280 243.2 280 120c0-13.3-10.7-24-24-24s-24 10.7-24 24z", 512, 512),
        ["heading"] = ("M0 64C0 46.3 14.3 32 32 32l96 0c17.7 0 32 14.3 32 32s-14.3 32-32 32l-16 0 0 112 224 0 0-112-16 0c-17.7 0-32-14.3-32-32s14.3-32 32-32l96 0c17.7 0 32 14.3 32 32s-14.3 32-32 32l-16 0 0 320 16 0c17.7 0 32 14.3 32 32s-14.3 32-32 32l-96 0c-17.7 0-32-14.3-32-32s14.3-32 32-32l16 0 0-144-224 0 0 144 16 0c17.7 0 32 14.3 32 32s-14.3 32-32 32l-96 0c-17.7 0-32-14.3-32-32s14.3-32 32-32l16 0 0-320-16 0C14.3 96 0 81.7 0 64z", 448, 512),
        ["location-dot"] = ("M0 188.6C0 84.4 86 0 192 0S384 84.4 384 188.6c0 119.3-120.2 262.3-170.4 316.8-11.8 12.8-31.5 12.8-43.3 0-50.2-54.5-170.4-197.5-170.4-316.8zM192 256a64 64 0 1 0 0-128 64 64 0 1 0 0 128z", 384, 512),
        ["align-left"] = ("M288 64c0 17.7-14.3 32-32 32L32 96C14.3 96 0 81.7 0 64S14.3 32 32 32l224 0c17.7 0 32 14.3 32 32zm0 256c0 17.7-14.3 32-32 32L32 352c-17.7 0-32-14.3-32-32s14.3-32 32-32l224 0c17.7 0 32 14.3 32 32zM0 192c0-17.7 14.3-32 32-32l384 0c17.7 0 32 14.3 32 32s-14.3 32-32 32L32 224c-17.7 0-32-14.3-32-32zM448 448c0 17.7-14.3 32-32 32L32 480c-17.7 0-32-14.3-32-32s14.3-32 32-32l384 0c17.7 0 32 14.3 32 32z", 448, 512),
        ["wind"] = ("M288 32c0 17.7 14.3 32 32 32l40 0c13.3 0 24 10.7 24 24s-10.7 24-24 24L32 112c-17.7 0-32 14.3-32 32s14.3 32 32 32l328 0c48.6 0 88-39.4 88-88S408.6 0 360 0L320 0c-17.7 0-32 14.3-32 32zm64 352c0 17.7 14.3 32 32 32l32 0c53 0 96-43 96-96s-43-96-96-96L32 224c-17.7 0-32 14.3-32 32s14.3 32 32 32l384 0c17.7 0 32 14.3 32 32s-14.3 32-32 32l-32 0c-17.7 0-32 14.3-32 32zM128 512l40 0c48.6 0 88-39.4 88-88s-39.4-88-88-88L32 336c-17.7 0-32 14.3-32 32s14.3 32 32 32l136 0c13.3 0 24 10.7 24 24s-10.7 24-24 24l-40 0c-17.7 0-32 14.3-32 32s14.3 32 32 32z", 512, 512),
        ["compass"] = ("M256 512a256 256 0 1 0 0-512 256 256 0 1 0 0 512zm50.7-186.9L162.4 380.6c-19.4 7.5-38.5-11.6-31-31l55.5-144.3c3.3-8.5 9.9-15.1 18.4-18.4l144.3-55.5c19.4-7.5 38.5 11.6 31 31L325.1 306.7c-3.2 8.5-9.9 15.1-18.4 18.4zM288 256a32 32 0 1 0 -64 0 32 32 0 1 0 64 0z", 512, 512),
        ["eye"] = ("M288 32c-80.8 0-145.5 36.8-192.6 80.6-46.8 43.5-78.1 95.4-93 131.1-3.3 7.9-3.3 16.7 0 24.6 14.9 35.7 46.2 87.7 93 131.1 47.1 43.7 111.8 80.6 192.6 80.6s145.5-36.8 192.6-80.6c46.8-43.5 78.1-95.4 93-131.1 3.3-7.9 3.3-16.7 0-24.6-14.9-35.7-46.2-87.7-93-131.1-47.1-43.7-111.8-80.6-192.6-80.6zM144 256a144 144 0 1 1 288 0 144 144 0 1 1 -288 0zm144-64c0 35.3-28.7 64-64 64-11.5 0-22.3-3-31.7-8.4-1 10.9-.1 22.1 2.9 33.2 13.7 51.2 66.4 81.6 117.6 67.9s81.6-66.4 67.9-117.6c-12.2-45.7-55.5-74.8-101.1-70.8 5.3 9.3 8.4 20.1 8.4 31.7z", 576, 512),
        ["sun"] = ("M178.2-10.1c7.4-3.1 15.8-2.2 22.5 2.2l87.8 58.2 87.8-58.2c6.7-4.4 15.1-5.2 22.5-2.2S411.4-.5 413 7.3l20.9 103.2 103.2 20.9c7.8 1.6 14.4 7 17.4 14.3s2.2 15.8-2.2 22.5l-58.2 87.8 58.2 87.8c4.4 6.7 5.2 15.1 2.2 22.5s-9.6 12.8-17.4 14.3L433.8 401.4 413 504.7c-1.6 7.8-7 14.4-14.3 17.4s-15.8 2.2-22.5-2.2l-87.8-58.2-87.8 58.2c-6.7 4.4-15.1 5.2-22.5 2.2s-12.8-9.6-14.3-17.4L143 401.4 39.7 380.5c-7.8-1.6-14.4-7-17.4-14.3s-2.2-15.8 2.2-22.5L82.7 256 24.5 168.2c-4.4-6.7-5.2-15.1-2.2-22.5s9.6-12.8 17.4-14.3L143 110.6 163.9 7.3c1.6-7.8 7-14.4 14.3-17.4zM207.6 256a80.4 80.4 0 1 1 160.8 0 80.4 80.4 0 1 1 -160.8 0zm208.8 0a128.4 128.4 0 1 0 -256.8 0 128.4 128.4 0 1 0 256.8 0z", 576, 512),
        ["cloud"] = ("M0 336c0 79.5 64.5 144 144 144l304 0c70.7 0 128-57.3 128-128 0-51.6-30.5-96.1-74.5-116.3 6.7-13.1 10.5-28 10.5-43.7 0-53-43-96-96-96-17.7 0-34.2 4.8-48.4 13.1-24.1-45.8-72.2-77.1-127.6-77.1-79.5 0-144 64.5-144 144 0 8 .7 15.9 1.9 23.5-56.9 19.2-97.9 73.1-97.9 136.5z", 576, 512),
        ["circle-info"] = ("M256 512a256 256 0 1 0 0-512 256 256 0 1 0 0 512zM224 160a32 32 0 1 1 64 0 32 32 0 1 1 -64 0zm-8 64l48 0c13.3 0 24 10.7 24 24l0 88 8 0c13.3 0 24 10.7 24 24s-10.7 24-24 24l-80 0c-13.3 0-24-10.7-24-24s10.7-24 24-24l24 0 0-64-24 0c-13.3 0-24-10.7-24-24s10.7-24 24-24z", 512, 512),
        ["calendar"] = ("M128 0C110.3 0 96 14.3 96 32l0 32-32 0C28.7 64 0 92.7 0 128l0 48 448 0 0-48c0-35.3-28.7-64-64-64l-32 0 0-32c0-17.7-14.3-32-32-32s-32 14.3-32 32l0 32-128 0 0-32c0-17.7-14.3-32-32-32zM0 224L0 416c0 35.3 28.7 64 64 64l320 0c35.3 0 64-28.7 64-64l0-192-448 0z", 448, 512),
        ["calendar-days"] = ("M128 0c17.7 0 32 14.3 32 32l0 32 128 0 0-32c0-17.7 14.3-32 32-32s32 14.3 32 32l0 32 32 0c35.3 0 64 28.7 64 64l0 288c0 35.3-28.7 64-64 64L64 480c-35.3 0-64-28.7-64-64L0 128C0 92.7 28.7 64 64 64l32 0 0-32c0-17.7 14.3-32 32-32zM64 240l0 32c0 8.8 7.2 16 16 16l32 0c8.8 0 16-7.2 16-16l0-32c0-8.8-7.2-16-16-16l-32 0c-8.8 0-16 7.2-16 16zm128 0l0 32c0 8.8 7.2 16 16 16l32 0c8.8 0 16-7.2 16-16l0-32c0-8.8-7.2-16-16-16l-32 0c-8.8 0-16 7.2-16 16zm144-16c-8.8 0-16 7.2-16 16l0 32c0 8.8 7.2 16 16 16l32 0c8.8 0 16-7.2 16-16l0-32c0-8.8-7.2-16-16-16l-32 0zM64 368l0 32c0 8.8 7.2 16 16 16l32 0c8.8 0 16-7.2 16-16l0-32c0-8.8-7.2-16-16-16l-32 0c-8.8 0-16 7.2-16 16zm144-16c-8.8 0-16 7.2-16 16l0 32c0 8.8 7.2 16 16 16l32 0c8.8 0 16-7.2 16-16l0-32c0-8.8-7.2-16-16-16l-32 0zm112 16l0 32c0 8.8 7.2 16 16 16l32 0c8.8 0 16-7.2 16-16l0-32c0-8.8-7.2-16-16-16l-32 0c-8.8 0-16 7.2-16 16z", 448, 512),
        ["list-check"] = ("M133.8 36.3c10.9 7.6 13.5 22.6 5.9 33.4l-56 80c-4.1 5.8-10.5 9.5-17.6 10.1S52 158 47 153L7 113C-2.3 103.6-2.3 88.4 7 79S31.6 69.7 41 79l19.8 19.8 39.6-56.6c7.6-10.9 22.6-13.5 33.4-5.9zm0 160c10.9 7.6 13.5 22.6 5.9 33.4l-56 80c-4.1 5.8-10.5 9.5-17.6 10.1S52 318 47 313L7 273c-9.4-9.4-9.4-24.6 0-33.9s24.6-9.4 33.9 0l19.8 19.8 39.6-56.6c7.6-10.9 22.6-13.5 33.4-5.9zM224 96c0-17.7 14.3-32 32-32l224 0c17.7 0 32 14.3 32 32s-14.3 32-32 32l-224 0c-17.7 0-32-14.3-32-32zm0 160c0-17.7 14.3-32 32-32l224 0c17.7 0 32 14.3 32 32s-14.3 32-32 32l-224 0c-17.7 0-32-14.3-32-32zM160 416c0-17.7 14.3-32 32-32l288 0c17.7 0 32 14.3 32 32s-14.3 32-32 32l-288 0c-17.7 0-32-14.3-32-32zM64 376a40 40 0 1 1 0 80 40 40 0 1 1 0-80z", 512, 512),
        ["circle"] = ("M0 256a256 256 0 1 1 512 0 256 256 0 1 1 -512 0z", 512, 512),
        ["check-circle"] = ("M256 512a256 256 0 1 1 0-512 256 256 0 1 1 0 512zM374 145.7c-10.7-7.8-25.7-5.4-33.5 5.3L221.1 315.2 169 263.1c-9.4-9.4-24.6-9.4-33.9 0s-9.4 24.6 0 33.9l72 72c5 5 11.8 7.5 18.8 7s13.4-4.1 17.5-9.8L379.3 179.2c7.8-10.7 5.4-25.7-5.3-33.5z", 512, 512),
        ["circle-check"] = ("M256 512a256 256 0 1 1 0-512 256 256 0 1 1 0 512zM374 145.7c-10.7-7.8-25.7-5.4-33.5 5.3L221.1 315.2 169 263.1c-9.4-9.4-24.6-9.4-33.9 0s-9.4 24.6 0 33.9l72 72c5 5 11.8 7.5 18.8 7s13.4-4.1 17.5-9.8L379.3 179.2c7.8-10.7 5.4-25.7-5.3-33.5z", 512, 512),
        ["rss"] = ("M0 64c0-17.7 14.3-32 32-32 229.8 0 416 186.2 416 416 0 17.7-14.3 32-32 32s-32-14.3-32-32C384 253.6 226.4 96 32 96 14.3 96 0 81.7 0 64zM0 416a64 64 0 1 1 128 0 64 64 0 1 1 -128 0zM32 160c159.1 0 288 128.9 288 288 0 17.7-14.3 32-32 32s-32-14.3-32-32c0-123.7-100.3-224-224-224-17.7 0-32-14.3-32-32s14.3-32 32-32z", 448, 512),
        ["chart-line"] = ("M64 64c0-17.7-14.3-32-32-32S0 46.3 0 64L0 400c0 44.2 35.8 80 80 80l400 0c17.7 0 32-14.3 32-32s-14.3-32-32-32L80 416c-8.8 0-16-7.2-16-16L64 64zm406.6 86.6c12.5-12.5 12.5-32.8 0-45.3s-32.8-12.5-45.3 0L320 210.7 262.6 153.4c-12.5-12.5-32.8-12.5-45.3 0l-96 96c-12.5 12.5-12.5 32.8 0 45.3s32.8 12.5 45.3 0l73.4-73.4 57.4 57.4c12.5 12.5 32.8 12.5 45.3 0l128-128z", 512, 512),
        ["temperature-low"] = ("M96 96c0-53 43-96 96-96s96 43 96 96l0 164.7c29.5 26.4 48 64.7 48 107.3 0 79.5-64.5 144-144 144S48 447.5 48 368c0-42.6 18.5-81 48-107.3L96 96zm96 336c35.3 0 64-28.7 64-64 0-26.9-16.5-49.9-40-59.3l0-28.7c0-13.3-10.7-24-24-24s-24 10.7-24 24l0 28.7c-23.5 9.5-40 32.5-40 59.3 0 35.3 28.7 64 64 64zM464 80a32 32 0 1 0 -64 0 32 32 0 1 0 64 0zM352 80a80 80 0 1 1 160 0 80 80 0 1 1 -160 0z", 512, 512),
    };

    public DashboardImageRenderingService(
        HomeAssistantService homeAssistantService,
        IWebHostEnvironment env,
        ILogger<DashboardImageRenderingService> logger)
    {
        _homeAssistantService = homeAssistantService;
        _env = env;
        _logger = logger;
        _fontFamily = LoadFontFamily();
    }

    /// <summary>
    /// Renders the dashboard to an ImageSharp image using stored configuration and live HA data.
    /// </summary>
    public async Task<Image<Rgba32>> RenderDashboardImageAsync(string dashboardId, string layoutConfigJson)
    {
        var layout = ParseLayout(layoutConfigJson);
        var data = await FetchSsrDataAsync(dashboardId, layout);
        return RenderToImage(layout, data);
    }

    // =============================================
    // LAYOUT PARSING
    // =============================================

    private LayoutConfig ParseLayout(string json)
    {
        _logger.LogInformation("SSR: Parsing layout JSON (first 1000 chars): {Json}", json.Substring(0, Math.Min(1000, json.Length)));

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Parse color scheme with defaults if missing
        ColorSchemeConfig colorScheme;
        if (root.TryGetProperty("colorScheme", out var cs))
        {
            var paletteArr = cs.TryGetProperty("palette", out var paletteEl) && paletteEl.ValueKind == JsonValueKind.Array
                ? paletteEl.EnumerateArray().Select(p => p.GetString() ?? "").ToArray()
                : new[] { "#000000", "#ffffff", "#ff0000" };

            colorScheme = new ColorSchemeConfig(
                Name: cs.TryGetProperty("name", out var nameEl) ? (nameEl.GetString() ?? "") : "Default",
                Variant: cs.TryGetProperty("variant", out var v) ? v.GetString() : null,
                Palette: paletteArr,
                Background: cs.TryGetProperty("background", out var bgEl) ? (bgEl.GetString() ?? "#ffffff") : "#ffffff",
                CanvasBackgroundColor: cs.TryGetProperty("canvasBackgroundColor", out var cbgEl) ? (cbgEl.GetString() ?? "#ffffff") : "#ffffff",
                WidgetBackgroundColor: cs.TryGetProperty("widgetBackgroundColor", out var wbgEl) ? (wbgEl.GetString() ?? "#ffffff") : "#ffffff",
                WidgetBorderColor: cs.TryGetProperty("widgetBorderColor", out var wbcEl) ? (wbcEl.GetString() ?? "#000000") : "#000000",
                WidgetTitleTextColor: cs.TryGetProperty("widgetTitleTextColor", out var wttcEl) ? (wttcEl.GetString() ?? "#000000") : "#000000",
                WidgetTextColor: cs.TryGetProperty("widgetTextColor", out var wtcEl) ? (wtcEl.GetString() ?? "#000000") : "#000000",
                IconColor: cs.TryGetProperty("iconColor", out var icEl) ? (icEl.GetString() ?? "#ff0000") : "#ff0000",
                Foreground: cs.TryGetProperty("foreground", out var fgEl) ? (fgEl.GetString() ?? "#000000") : "#000000",
                Accent: cs.TryGetProperty("accent", out var acEl) ? (acEl.GetString() ?? "#ff0000") : "#ff0000",
                Text: cs.TryGetProperty("text", out var txtEl) ? (txtEl.GetString() ?? "#000000") : "#000000"
            );
        }
        else
        {
            colorScheme = new ColorSchemeConfig(
                Name: "Default",
                Variant: null,
                Palette: new[] { "#000000", "#ffffff", "#ff0000" },
                Background: "#ffffff",
                CanvasBackgroundColor: "#ffffff",
                WidgetBackgroundColor: "#ffffff",
                WidgetBorderColor: "#000000",
                WidgetTitleTextColor: "#000000",
                WidgetTextColor: "#000000",
                IconColor: "#ff0000",
                Foreground: "#000000",
                Accent: "#ff0000",
                Text: "#000000"
            );
        }

        var widgets = new List<WidgetConfigEntry>();
        if (root.TryGetProperty("widgets", out var widgetsArr) && widgetsArr.ValueKind == JsonValueKind.Array)
        {
            _logger.LogInformation("SSR: Found widgets array with {Count} items", widgetsArr.GetArrayLength());
            int widgetIndex = 0;
            foreach (var w in widgetsArr.EnumerateArray())
            {
                widgetIndex++;
                _logger.LogInformation("SSR: Processing widget {Index}: {Widget}", widgetIndex, w.ToString());

                if (!w.TryGetProperty("position", out var pos) ||
                    !w.TryGetProperty("id", out var idEl) ||
                    !w.TryGetProperty("type", out var typeEl) ||
                    !w.TryGetProperty("config", out var configEl))
                {
                    _logger.LogWarning("SSR: Widget {Index} missing required properties - id:{HasId} type:{HasType} position:{HasPos} config:{HasConfig}",
                        widgetIndex,
                        w.TryGetProperty("id", out _),
                        w.TryGetProperty("type", out _),
                        w.TryGetProperty("position", out _),
                        w.TryGetProperty("config", out _));
                    continue;
                }

                if (!pos.TryGetProperty("x", out var xEl) ||
                    !pos.TryGetProperty("y", out var yEl) ||
                    !pos.TryGetProperty("w", out var wEl) ||
                    !pos.TryGetProperty("h", out var hEl))
                {
                    _logger.LogWarning("SSR: Widget {Index} missing position data - x:{HasX} y:{HasY} w:{HasW} h:{HasH}",
                        widgetIndex,
                        pos.TryGetProperty("x", out _),
                        pos.TryGetProperty("y", out _),
                        pos.TryGetProperty("w", out _),
                        pos.TryGetProperty("h", out _));
                    continue;
                }

                var position = new WidgetPositionConfig(
                    X: xEl.GetInt32(),
                    Y: yEl.GetInt32(),
                    W: wEl.GetInt32(),
                    H: hEl.GetInt32(),
                    PixelX: pos.TryGetProperty("pixelX", out var pxEl) && pxEl.ValueKind == JsonValueKind.Number ? pxEl.GetDouble() : null,
                    PixelY: pos.TryGetProperty("pixelY", out var pyEl) && pyEl.ValueKind == JsonValueKind.Number ? pyEl.GetDouble() : null,
                    PixelWidth: pos.TryGetProperty("pixelWidth", out var pwEl) && pwEl.ValueKind == JsonValueKind.Number ? pwEl.GetDouble() : null,
                    PixelHeight: pos.TryGetProperty("pixelHeight", out var phEl) && phEl.ValueKind == JsonValueKind.Number ? phEl.GetDouble() : null
                );

                WidgetColorOverridesConfig? overrides = null;
                if (w.TryGetProperty("colorOverrides", out var co) && co.ValueKind == JsonValueKind.Object)
                {
                    overrides = new WidgetColorOverridesConfig(
                        WidgetBackgroundColor: co.TryGetProperty("widgetBackgroundColor", out var wbg) ? wbg.GetString() : null,
                        WidgetBorderColor: co.TryGetProperty("widgetBorderColor", out var wbc) ? wbc.GetString() : null,
                        WidgetTitleTextColor: co.TryGetProperty("widgetTitleTextColor", out var wttc) ? wttc.GetString() : null,
                        WidgetTextColor: co.TryGetProperty("widgetTextColor", out var wtc) ? wtc.GetString() : null,
                        IconColor: co.TryGetProperty("iconColor", out var ic) ? ic.GetString() : null
                    );
                }

                widgets.Add(new WidgetConfigEntry(
                    Id: idEl.GetString() ?? "",
                    Type: typeEl.GetString() ?? "",
                    Position: position,
                    Config: configEl.Clone(),
                    ColorOverrides: overrides,
                    TitleOverride: w.TryGetProperty("titleOverride", out var toEl) ? toEl.GetString() : null,
                    ShowTitle: w.TryGetProperty("showTitle", out var stEl) && stEl.ValueKind == JsonValueKind.False ? false : true
                ));
                _logger.LogInformation("SSR: Successfully parsed widget {Index}: type={Type}, id={Id}, pos=({X},{Y},{W},{H})",
                    widgetIndex, typeEl.GetString(), idEl.GetString(), position.X, position.Y, position.W, position.H);
            }
        }
        else
        {
            _logger.LogWarning("SSR: No widgets property found or not an array in layout JSON");
        }

        _logger.LogInformation("SSR: Parsed {WidgetCount} widgets from layout", widgets.Count);

        return new LayoutConfig(
            Width: root.TryGetProperty("width", out var width) ? width.GetInt32() : 800,
            Height: root.TryGetProperty("height", out var height) ? height.GetInt32() : 480,
            GridCols: root.TryGetProperty("gridCols", out var gc) ? gc.GetInt32() : 12,
            GridRows: root.TryGetProperty("gridRows", out var gr) ? gr.GetInt32() : 8,
            ColorScheme: colorScheme,
            Widgets: widgets,
            CanvasPadding: root.TryGetProperty("canvasPadding", out var cp) ? cp.GetInt32() : 16,
            WidgetGap: root.TryGetProperty("widgetGap", out var wg) ? wg.GetInt32() : 4,
            WidgetBorder: root.TryGetProperty("widgetBorder", out var wb) ? wb.GetInt32() : 3,
            WidgetPadding: root.TryGetProperty("widgetPadding", out var wp) ? wp.GetInt32() : 4,
            TitleFontSize: root.TryGetProperty("titleFontSize", out var tf) ? tf.GetInt32() : 16,
            TextFontSize: root.TryGetProperty("textFontSize", out var txf) ? txf.GetInt32() : 14,
            TitleFontWeight: root.TryGetProperty("titleFontWeight", out var tfw) ? tfw.GetInt32() : 700,
            TextFontWeight: root.TryGetProperty("textFontWeight", out var txfw) ? txfw.GetInt32() : 400
        );
    }

    // =============================================
    // DATA FETCHING
    // =============================================

    private async Task<SsrData> FetchSsrDataAsync(string dashboardId, LayoutConfig layout)
    {
        var data = new SsrData();

        // Collect all entity IDs needed across all widgets
        var entityIds = CollectEntityIds(layout);

        // Fetch all entity states in one call
        if (entityIds.Count > 0)
        {
            var statesResult = await _homeAssistantService.FetchEntityStates(dashboardId, entityIds.ToArray());
            if (statesResult.IsSuccess)
            {
                foreach (var state in statesResult.Value)
                    data.EntityStates[state.EntityId] = state;
            }
            else
            {
                _logger.LogWarning("SSR: Failed to fetch entity states: {Error}", statesResult.Error);
            }
        }

        // Fetch todo items per widget
        foreach (var widget in layout.Widgets.Where(w => w.Type == "todo"))
        {
            var entityId = GetStringProp(widget.Config, "entityId");
            if (!string.IsNullOrEmpty(entityId))
            {
                var result = await _homeAssistantService.FetchTodoItems(dashboardId, entityId);
                if (result.IsSuccess) data.TodoItems[entityId] = result.Value;
            }
        }

        // Fetch calendar events per widget
        foreach (var widget in layout.Widgets.Where(w => w.Type == "calendar"))
        {
            var entityId = GetStringProp(widget.Config, "entityId");
            if (!string.IsNullOrEmpty(entityId))
            {
                var result = await _homeAssistantService.FetchCalendarEvents(dashboardId, entityId, 168);
                if (result.IsSuccess) data.CalendarEvents[entityId] = result.Value;
            }
        }

        // Fetch weather forecasts per widget
        foreach (var widget in layout.Widgets.Where(w => w.Type == "weather-forecast"))
        {
            var entityId = GetStringProp(widget.Config, "entityId");
            var forecastMode = GetStringProp(widget.Config, "forecastMode") ?? "daily";
            var forecastType = forecastMode == "hourly" ? "hourly" : "daily";
            if (!string.IsNullOrEmpty(entityId))
            {
                var result = await _homeAssistantService.FetchWeatherForecast(dashboardId, entityId, forecastType);
                if (result.IsSuccess
                    && result.Value.TryGetValue("forecast", out var forecastVal)
                    && forecastVal is List<object?> forecastList)
                {
                    data.WeatherForecasts[entityId] = forecastList;
                }
            }
        }

        // Fetch RSS feed entries per widget
        foreach (var widget in layout.Widgets.Where(w => w.Type == "rss-feed"))
        {
            var entityId = GetStringProp(widget.Config, "entityId");
            if (!string.IsNullOrEmpty(entityId))
            {
                var result = await _homeAssistantService.FetchRssFeedEntries(dashboardId, entityId);
                if (result.IsSuccess)
                {
                    data.RssFeedEntries[entityId] = result.Value;
                    _logger.LogDebug("SSR: Fetched {Count} RSS entries for {EntityId}", result.Value.Count, entityId);
                }
                else
                {
                    _logger.LogWarning("SSR: Failed to fetch RSS entries for {EntityId}: {Error}", entityId, result.Error);
                }
            }
        }

        // Fetch entity history for graph widgets
        foreach (var widget in layout.Widgets.Where(w => w.Type == "graph"))
        {
            if (widget.Config.TryGetProperty("series", out var series) && series.ValueKind == JsonValueKind.Array)
            {
                var graphEntityIds = series.EnumerateArray()
                    .Select(s => GetStringProp(s, "entityId"))
                    .Where(id => !string.IsNullOrEmpty(id))
                    .Cast<string>()
                    .ToList();

                if (graphEntityIds.Count > 0)
                {
                    var periodStr = GetStringProp(widget.Config, "period") ?? "24h";
                    var hours = periodStr switch
                    {
                        "1h" => 1,
                        "6h" => 6,
                        "24h" => 24,
                        "7d" => 168,
                        "30d" => 720,
                        _ => 24
                    };

                    var result = await _homeAssistantService.FetchEntityHistory(dashboardId, graphEntityIds, hours);
                    if (result.IsSuccess)
                    {
                        foreach (var (entityId, states) in result.Value)
                            data.HistoryData[entityId] = states;
                    }
                }
            }
        }

        return data;
    }

    private static HashSet<string> CollectEntityIds(LayoutConfig layout)
    {
        var ids = new HashSet<string>();
        foreach (var widget in layout.Widgets)
        {
            switch (widget.Type)
            {
                case "calendar":
                case "weather":
                case "weather-forecast":
                case "todo":
                case "rss-feed":
                    AddId(widget.Config, "entityId", ids);
                    break;
                case "graph":
                    if (widget.Config.TryGetProperty("series", out var series)
                        && series.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var s in series.EnumerateArray())
                            AddId(s, "entityId", ids);
                    }
                    break;
                case "header":
                    if (widget.Config.TryGetProperty("badges", out var badges)
                        && badges.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var badge in badges.EnumerateArray())
                            AddId(badge, "entityId", ids);
                    }
                    break;
            }
        }
        return ids;

        static void AddId(JsonElement el, string prop, HashSet<string> ids)
        {
            var val = el.TryGetProperty(prop, out var p) ? p.GetString() : null;
            if (!string.IsNullOrEmpty(val)) ids.Add(val);
        }
    }

    // =============================================
    // FONT LOADING
    // =============================================

    private static FontFamily LoadFontFamily()
    {
        var collection = new FontCollection();

        // Try system fonts first
        if (SystemFonts.TryGet("DejaVu Sans", out var systemFamily))
            return systemFamily;
        if (SystemFonts.TryGet("Liberation Sans", out systemFamily))
            return systemFamily;
        if (SystemFonts.TryGet("Arial", out systemFamily))
            return systemFamily;
        if (SystemFonts.TryGet("Helvetica", out systemFamily))
            return systemFamily;
        if (SystemFonts.TryGet("Segoe UI", out systemFamily))
            return systemFamily;
        if (SystemFonts.TryGet("Roboto", out systemFamily))
            return systemFamily;

        // Fallback: use any available system font
        foreach (var family in SystemFonts.Families)
            return family;

        throw new InvalidOperationException("No fonts available on the system for rendering.");
    }

    private Font GetFont(int size, FontStyle style = FontStyle.Regular)
    {
        return _fontFamily.CreateFont(size, style);
    }

    private Font GetFont(int size, int weight)
    {
        var style = weight >= 700 ? FontStyle.Bold : FontStyle.Regular;
        return _fontFamily.CreateFont(size, style);
    }

    // =============================================
    // IMAGE RENDERING
    // =============================================

    private Image<Rgba32> RenderToImage(LayoutConfig layout, SsrData data)
    {
        var image = new Image<Rgba32>(layout.Width, layout.Height);

        // Fill canvas background
        var canvasBg = ParseColor(layout.ColorScheme.CanvasBackgroundColor);
        image.Mutate(ctx => ctx.Fill(canvasBg));

        // Render each widget
        foreach (var widget in layout.Widgets)
        {
            RenderWidget(image, widget, layout, data);
        }

        return image;
    }

    private void RenderWidget(Image<Rgba32> image, WidgetConfigEntry widget, LayoutConfig layout, SsrData data)
    {
        var (px, py, pw, ph) = ResolvePixelPosition(widget.Position, layout);
        var widgetRect = new RectangleF((float)px, (float)py, (float)pw, (float)ph);

        // Draw widget background and border
        DrawWidgetContainer(image, widget, layout, widgetRect);

        // Content area (inside border + padding)
        var border = layout.WidgetBorder;
        var padding = layout.WidgetPadding;
        var inset = border + padding;
        var contentRect = new RectangleF(
            widgetRect.X + inset,
            widgetRect.Y + inset,
            Math.Max(0, widgetRect.Width - inset * 2),
            Math.Max(0, widgetRect.Height - inset * 2));

        if (contentRect.Width <= 0 || contentRect.Height <= 0)
            return;

        try
        {
            switch (widget.Type)
            {
                case "header":
                    RenderHeaderWidget(image, widget, layout, data, contentRect);
                    break;
                case "calendar":
                    RenderCalendarWidget(image, widget, layout, data, contentRect);
                    break;
                case "weather":
                    RenderWeatherWidget(image, widget, layout, data, contentRect);
                    break;
                case "weather-forecast":
                    RenderWeatherForecastWidget(image, widget, layout, data, contentRect);
                    break;
                case "todo":
                    RenderTodoWidget(image, widget, layout, data, contentRect);
                    break;
                case "markdown":
                    RenderMarkdownWidget(image, widget, layout, contentRect);
                    break;
                case "rss-feed":
                    RenderRssFeedWidget(image, widget, layout, data, contentRect);
                    break;
                case "version":
                    RenderVersionWidget(image, widget, layout, contentRect);
                    break;
                case "app-icon":
                    RenderAppIconWidget(image, widget, layout, data, contentRect);
                    break;
                case "image":
                    RenderImageWidget(image, widget, layout, contentRect);
                    break;
                case "graph":
                    RenderGraphWidget(image, widget, layout, data, contentRect);
                    break;
                default:
                    RenderPlaceholder(image, widget, layout, contentRect, widget.Type);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to render widget {WidgetId} of type {WidgetType}", widget.Id, widget.Type);
        }
    }

    // =============================================
    // WIDGET CONTAINER
    // =============================================

    private static void DrawWidgetContainer(Image<Rgba32> image, WidgetConfigEntry widget, LayoutConfig layout, RectangleF rect)
    {
        var cs = layout.ColorScheme;
        var bg = ParseColor(widget.ColorOverrides?.WidgetBackgroundColor ?? cs.WidgetBackgroundColor);
        var bc = ParseColor(widget.ColorOverrides?.WidgetBorderColor ?? cs.WidgetBorderColor);
        var borderWidth = layout.WidgetBorder;

        image.Mutate(ctx =>
        {
            // Fill background
            ctx.Fill(bg, new RectangularPolygon(rect));

            // Draw border
            if (borderWidth > 0)
            {
                ctx.Draw(bc, borderWidth, new RectangularPolygon(
                    rect.X + borderWidth / 2f,
                    rect.Y + borderWidth / 2f,
                    rect.Width - borderWidth,
                    rect.Height - borderWidth));
            }
        });
    }

    // =============================================
    // HEADER WIDGET
    // =============================================

    private void RenderHeaderWidget(Image<Rgba32> image, WidgetConfigEntry widget, LayoutConfig layout, SsrData data, RectangleF contentRect)
    {
        var titleColor = ResolveWidgetColor(widget, layout, c => c.WidgetTitleTextColor, o => o?.WidgetTitleTextColor);
        var textColor = ResolveWidgetColor(widget, layout, c => c.WidgetTextColor, o => o?.WidgetTextColor);
        var iconColor = ResolveWidgetColor(widget, layout, c => c.IconColor, o => o?.IconColor);
        var titleFontSize = layout.TitleFontSize > 0 ? layout.TitleFontSize : 16;
        var textFontSize = layout.TextFontSize > 0 ? layout.TextFontSize : 14;
        var titleFontWeight = layout.TitleFontWeight > 0 ? layout.TitleFontWeight : 700;
        var textFontWeight = layout.TextFontWeight > 0 ? layout.TextFontWeight : 400;

        var title = GetStringProp(widget.Config, "title") ?? "";
        var iconPosition = GetStringProp(widget.Config, "iconPosition") ?? "left";
        var iconSize = GetIntProp(widget.Config, "iconSize") ?? 32;
        var isIconOnLeft = iconPosition != "right";

        if (widget.ShowTitle && !string.IsNullOrEmpty(title))
        {
            var titleX = GetDoubleProp(widget.Config, "titleX") ?? (isIconOnLeft ? 58.0 : 0.0);
            var titleY = GetDoubleProp(widget.Config, "titleY") ?? 0.0;
            var titleW = GetDoubleProp(widget.Config, "titleW") ?? 42.0;
            var titleH = GetDoubleProp(widget.Config, "titleH") ?? 50.0;

            // The title section region (matches the Angular flex container)
            var sectionRect = new RectangleF(
                contentRect.X + (float)(titleX / 100.0 * contentRect.Width),
                contentRect.Y + (float)(titleY / 100.0 * contentRect.Height),
                (float)(titleW / 100.0 * contentRect.Width),
                (float)(titleH / 100.0 * contentRect.Height));

            // Clamp the icon to the section height, preserving aspect ratio
            var effectiveIconSize = Math.Min(iconSize, sectionRect.Height);

            float textLeftOffset = 0;
            float textRightOffset = 0;

            // Draw app icon — placed inside the title section like the Angular flex layout
            {
                RectangleF iconBounds;
                if (isIconOnLeft)
                {
                    iconBounds = new RectangleF(
                        sectionRect.X,
                        sectionRect.Y + (sectionRect.Height - effectiveIconSize) / 2f,
                        effectiveIconSize,
                        effectiveIconSize);
                    textLeftOffset = effectiveIconSize + 4;
                }
                else
                {
                    iconBounds = new RectangleF(
                        sectionRect.Right - effectiveIconSize,
                        sectionRect.Y + (sectionRect.Height - effectiveIconSize) / 2f,
                        effectiveIconSize,
                        effectiveIconSize);
                    textRightOffset = effectiveIconSize + 4;
                }
                DrawAppIcon(image, iconColor, iconBounds);
            }

            // Title text fills the remaining space beside the icon
            var titleRect = new RectangleF(
                sectionRect.X + textLeftOffset,
                sectionRect.Y,
                sectionRect.Width - textLeftOffset - textRightOffset,
                sectionRect.Height);

            DrawTextEllipsis(image, title, GetFont(titleFontSize, titleFontWeight), titleColor, titleRect);
        }

        // Render badges
        if (widget.Config.TryGetProperty("badges", out var badges) && badges.ValueKind == JsonValueKind.Array)
        {
            int badgeIndex = 0;
            foreach (var badge in badges.EnumerateArray())
            {
                var bEntityId = badge.TryGetProperty("entityId", out var eid) ? eid.GetString() : null;
                var bIcon = badge.TryGetProperty("icon", out var ic) ? ic.GetString() : null;
                bool hasContent = !string.IsNullOrWhiteSpace(bEntityId) || !string.IsNullOrWhiteSpace(bIcon);
                if (!hasContent) { badgeIndex++; continue; }

                var bx = GetBadgeDoubleProp(badge, "x") ?? (badgeIndex % 4) * 22.0;
                var by = GetBadgeDoubleProp(badge, "y") ?? Math.Floor((double)badgeIndex / 4) * 30.0;
                var bw = GetBadgeDoubleProp(badge, "w") ?? 22.0;
                var bh = GetBadgeDoubleProp(badge, "h") ?? 30.0;

                var badgeRect = new RectangleF(
                    contentRect.X + (float)(bx / 100.0 * contentRect.Width),
                    contentRect.Y + (float)(by / 100.0 * contentRect.Height),
                    (float)(bw / 100.0 * contentRect.Width),
                    (float)(bh / 100.0 * contentRect.Height));

                float textStartX = badgeRect.X;

                // Draw badge FA icon if present
                if (!string.IsNullOrEmpty(bIcon))
                {
                    var badgeIconSize = Math.Min(textFontSize, badgeRect.Height * 0.6f);
                    var iconBounds = new RectangleF(
                        badgeRect.X,
                        badgeRect.Y + (badgeRect.Height - badgeIconSize) / 2f,
                        badgeIconSize,
                        badgeIconSize);
                    DrawFaIcon(image, bIcon, iconColor, iconBounds);
                    textStartX = iconBounds.Right + 2;
                }

                if (!string.IsNullOrEmpty(bEntityId) && data.EntityStates.TryGetValue(bEntityId, out var es))
                {
                    var badgeText = es.State;
                    var uom = GetEntityAttr(es, "unit_of_measurement");
                    if (!string.IsNullOrEmpty(uom)) badgeText += $" {uom}";
                    var textRect = new RectangleF(textStartX, badgeRect.Y, badgeRect.Right - textStartX, badgeRect.Height);
                    DrawTextEllipsis(image, badgeText, GetFont(textFontSize, textFontWeight), textColor, textRect);
                }

                badgeIndex++;
            }
        }
    }

    // =============================================
    // CALENDAR WIDGET
    // =============================================

    private void RenderCalendarWidget(Image<Rgba32> image, WidgetConfigEntry widget, LayoutConfig layout, SsrData data, RectangleF contentRect)
    {
        var titleColor = ResolveWidgetColor(widget, layout, c => c.WidgetTitleTextColor, o => o?.WidgetTitleTextColor);
        var textColor = ResolveWidgetColor(widget, layout, c => c.WidgetTextColor, o => o?.WidgetTextColor);
        var iconColor = ResolveWidgetColor(widget, layout, c => c.IconColor, o => o?.IconColor);
        var titleFontSize = layout.TitleFontSize > 0 ? layout.TitleFontSize : 15;
        var textFontSize = layout.TextFontSize > 0 ? layout.TextFontSize : 12;
        var titleFontWeight = layout.TitleFontWeight > 0 ? layout.TitleFontWeight : 700;
        var textFontWeight = layout.TextFontWeight > 0 ? layout.TextFontWeight : 400;

        var entityId = GetStringProp(widget.Config, "entityId") ?? "";
        var maxEvents = GetIntProp(widget.Config, "maxEvents") ?? 7;
        var eventGap = GetIntProp(widget.Config, "eventGap") ?? 0;
        var visibleItems = GetCalendarEventItems(widget.Config);

        float yOffset = contentRect.Y;

        if (widget.ShowTitle)
        {
            var titleText = widget.TitleOverride ?? "Events";
            var titleRect = new RectangleF(contentRect.X, yOffset, contentRect.Width, titleFontSize + 4);
            DrawTextEllipsis(image, titleText, GetFont(titleFontSize, titleFontWeight), titleColor, titleRect);
            yOffset += titleFontSize + 6;
        }

        if (!string.IsNullOrEmpty(entityId)
            && data.CalendarEvents.TryGetValue(entityId, out var events)
            && events.Count > 0)
        {
            var now = DateTimeOffset.UtcNow;
            var upcoming = events
                .Where(e =>
                {
                    if (DateTimeOffset.TryParse(e.End ?? e.Start, CultureInfo.InvariantCulture, DateTimeStyles.None, out var endDt))
                        return endDt > now;
                    if (DateTimeOffset.TryParse(e.Start, CultureInfo.InvariantCulture, DateTimeStyles.None, out var startDt))
                        return startDt >= now;
                    return false;
                })
                .Take(maxEvents).ToList();

            var lineHeight = textFontSize + 4;
            var iconSize = textFontSize * 0.9f;

            foreach (var ev in upcoming)
            {
                if (yOffset + lineHeight > contentRect.Bottom) break;

                foreach (var item in visibleItems)
                {
                    if (yOffset + lineHeight > contentRect.Bottom) break;

                    string? text = item.Type switch
                    {
                        "datetime" => FormatEventDate(ev.Start),
                        "title" => ev.Summary ?? ev.Description ?? "-",
                        "location" => ev.Location,
                        "description" => ev.Description,
                        _ => null
                    };
                    if (string.IsNullOrEmpty(text)) continue;

                    var itemIcon = item.Icon ?? GetDefaultCalendarEventItemIcon(item.Type);
                    float textX = contentRect.X;

                    // Draw icon if present
                    if (!string.IsNullOrEmpty(itemIcon))
                    {
                        var iconBounds = new RectangleF(
                            contentRect.X,
                            yOffset + (lineHeight - iconSize) / 2f,
                            iconSize, iconSize);
                        DrawFaIcon(image, itemIcon, iconColor, iconBounds);
                        textX = iconBounds.Right + 4;
                    }

                    var textRect = new RectangleF(textX, yOffset, contentRect.Right - textX, lineHeight);
                    DrawTextEllipsis(image, text, GetFont(textFontSize, textFontWeight), textColor, textRect);
                    yOffset += lineHeight;
                }

                yOffset += eventGap;
            }
        }
        else
        {
            DrawCenteredText(image, "No events", GetFont(textFontSize), textColor, contentRect);
        }
    }

    private record CalendarEventItemEntry(string Type, bool Visible, string? Icon, double X, double Y, double W, double H);

    private static List<CalendarEventItemEntry> GetCalendarEventItems(JsonElement config)
    {
        var defaults = new List<CalendarEventItemEntry>
        {
            new("datetime", true, "fa-clock", 0, 0, 100, 50),
            new("title", true, null, 0, 50, 100, 50),
            new("location", false, "fa-location-dot", 0, 50, 100, 25),
            new("description", false, "fa-align-left", 0, 75, 100, 25),
        };

        if (config.TryGetProperty("items", out var itemsEl) && itemsEl.ValueKind == JsonValueKind.Array)
        {
            var result = new List<CalendarEventItemEntry>();
            foreach (var el in itemsEl.EnumerateArray())
            {
                var type = el.TryGetProperty("type", out var tProp) ? tProp.GetString() ?? "" : "";
                var visible = !el.TryGetProperty("visible", out var vProp) || vProp.ValueKind != JsonValueKind.False;
                var icon = el.TryGetProperty("icon", out var iProp) ? iProp.GetString() : null;
                var def = defaults.FirstOrDefault(d => d.Type == type) ?? defaults[0];
                var x = el.TryGetProperty("x", out var xP) && xP.TryGetDouble(out var xv) ? xv : def.X;
                var y = el.TryGetProperty("y", out var yP) && yP.TryGetDouble(out var yv) ? yv : def.Y;
                var w = el.TryGetProperty("w", out var wP) && wP.TryGetDouble(out var wv) ? wv : def.W;
                var h = el.TryGetProperty("h", out var hP) && hP.TryGetDouble(out var hv) ? hv : def.H;
                if (visible)
                    result.Add(new CalendarEventItemEntry(type, visible, icon, x, y, w, h));
            }
            return result;
        }

        return defaults.Where(d => d.Visible).ToList();
    }

    private static string GetDefaultCalendarEventItemIcon(string type) => type switch
    {
        "datetime" => "fa-clock",
        "title" => "",
        "location" => "fa-location-dot",
        "description" => "fa-align-left",
        _ => ""
    };

    // =============================================
    // WEATHER WIDGET
    // =============================================

    private void RenderWeatherWidget(Image<Rgba32> image, WidgetConfigEntry widget, LayoutConfig layout, SsrData data, RectangleF contentRect)
    {
        var titleColor = ResolveWidgetColor(widget, layout, c => c.WidgetTitleTextColor, o => o?.WidgetTitleTextColor);
        var textColor = ResolveWidgetColor(widget, layout, c => c.WidgetTextColor, o => o?.WidgetTextColor);
        var iconColor = ResolveWidgetColor(widget, layout, c => c.IconColor, o => o?.IconColor);
        var titleFontSize = layout.TitleFontSize > 0 ? layout.TitleFontSize : 15;
        var textFontSize = layout.TextFontSize > 0 ? layout.TextFontSize : 12;
        var titleFontWeight = layout.TitleFontWeight > 0 ? layout.TitleFontWeight : 700;
        var textFontWeight = layout.TextFontWeight > 0 ? layout.TextFontWeight : 400;

        var entityId = GetStringProp(widget.Config, "entityId") ?? "";

        if (string.IsNullOrEmpty(entityId) || !data.EntityStates.TryGetValue(entityId, out var es))
        {
            DrawCenteredText(image, "Weather", GetFont(titleFontSize, titleFontWeight), titleColor, contentRect);
            return;
        }

        var temperature = GetEntityAttr(es, "temperature") ?? "";
        var condition = es.State ?? "";
        var pressure = GetEntityAttr(es, "pressure") ?? "";

        // Parse items from config, fall back to defaults
        var items = GetWeatherItems(widget.Config);
        var iconSize = textFontSize * 0.9f;

        foreach (var item in items)
        {
            var visible = item.Visible;
            if (item.Type == "title" && !widget.ShowTitle) visible = false;
            if (!visible) continue;

            var itemRect = new RectangleF(
                contentRect.X + (float)(item.X / 100.0 * contentRect.Width),
                contentRect.Y + (float)(item.Y / 100.0 * contentRect.Height),
                (float)(item.W / 100.0 * contentRect.Width),
                (float)(item.H / 100.0 * contentRect.Height));

            switch (item.Type)
            {
                case "title":
                    DrawTextEllipsis(image, widget.TitleOverride ?? "Weather", GetFont(titleFontSize, titleFontWeight), titleColor, itemRect);
                    break;
                case "temperature":
                {
                    var tempIcon = item.Icon ?? "fa-temperature-half";
                    var (textX, textW) = DrawWeatherItemIcon(image, tempIcon, iconColor, iconSize, itemRect);
                    DrawTextEllipsis(image, $"{temperature}°", GetFont(textFontSize, textFontWeight), textColor,
                        new RectangleF(textX, itemRect.Y, textW, itemRect.Height));
                    break;
                }
                case "condition":
                {
                    var condIcon = item.Icon ?? "fa-cloud-sun";
                    var (textX, textW) = DrawWeatherItemIcon(image, condIcon, iconColor, iconSize, itemRect);
                    DrawTextEllipsis(image, condition, GetFont(textFontSize, textFontWeight), textColor,
                        new RectangleF(textX, itemRect.Y, textW, itemRect.Height));
                    break;
                }
                case "pressure":
                {
                    var pressIcon = item.Icon ?? "fa-gauge";
                    var (textX, textW) = DrawWeatherItemIcon(image, pressIcon, iconColor, iconSize, itemRect);
                    DrawTextEllipsis(image, pressure, GetFont(textFontSize, textFontWeight), textColor,
                        new RectangleF(textX, itemRect.Y, textW, itemRect.Height));
                    break;
                }
                case "attribute":
                {
                    var attrKey = item.AttributeKey ?? "humidity";
                    var attrVal = GetEntityAttr(es, attrKey) ?? "";
                    var suffix = attrKey == "humidity" ? "%" : "";
                    var attrIcon = item.Icon ?? attrKey switch
                    {
                        "humidity" => "fa-droplet",
                        "wind_speed" => "fa-wind",
                        _ => "fa-circle-info"
                    };
                    var (textX, textW) = DrawWeatherItemIcon(image, attrIcon, iconColor, iconSize, itemRect);
                    DrawTextEllipsis(image, $"{attrVal}{suffix}", GetFont(textFontSize, textFontWeight), textColor,
                        new RectangleF(textX, itemRect.Y, textW, itemRect.Height));
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Draws a weather item's FA icon on the left side of the item rect and returns the remaining text area.
    /// </summary>
    private static (float TextX, float TextW) DrawWeatherItemIcon(Image<Rgba32> image, string? icon, Color iconColor, float iconSize, RectangleF itemRect)
    {
        if (!string.IsNullOrEmpty(icon))
        {
            var iconBounds = new RectangleF(
                itemRect.X + 2,
                itemRect.Y + (itemRect.Height - iconSize) / 2f,
                iconSize, iconSize);
            DrawFaIcon(image, icon, iconColor, iconBounds);
            return (iconBounds.Right + 4, itemRect.Width - iconSize - 6);
        }
        return (itemRect.X, itemRect.Width);
    }

    // =============================================
    // WEATHER FORECAST WIDGET
    // =============================================

    private void RenderWeatherForecastWidget(Image<Rgba32> image, WidgetConfigEntry widget, LayoutConfig layout, SsrData data, RectangleF contentRect)
    {
        var titleColor = ResolveWidgetColor(widget, layout, c => c.WidgetTitleTextColor, o => o?.WidgetTitleTextColor);
        var textColor = ResolveWidgetColor(widget, layout, c => c.WidgetTextColor, o => o?.WidgetTextColor);
        var titleFontSize = layout.TitleFontSize > 0 ? layout.TitleFontSize : 15;
        var textFontSize = layout.TextFontSize > 0 ? layout.TextFontSize : 12;
        var titleFontWeight = layout.TitleFontWeight > 0 ? layout.TitleFontWeight : 700;
        var textFontWeight = layout.TextFontWeight > 0 ? layout.TextFontWeight : 400;

        var entityId = GetStringProp(widget.Config, "entityId") ?? "";
        var forecastMode = GetStringProp(widget.Config, "forecastMode") ?? "daily";
        var maxItems = GetIntProp(widget.Config, "maxItems");
        var visibleFields = GetStringArrayProp(widget.Config, "visibleFields") ?? new[] { "time", "condition", "tempHigh", "tempLow" };
        if (visibleFields.Contains("temperature"))
            visibleFields = visibleFields.Where(f => f != "temperature").Concat(new[] { "tempHigh", "tempLow" }).Distinct().ToArray();
        var rowGap = GetIntProp(widget.Config, "rowGap") ?? 0;

        float yOffset = contentRect.Y;

        // Title
        if (widget.ShowTitle && widget.Position.H > 1)
        {
            var headerRect = new RectangleF(contentRect.X, yOffset, contentRect.Width, titleFontSize + 4);
            DrawTextEllipsis(image, widget.TitleOverride ?? "Forecast", GetFont(titleFontSize, titleFontWeight), titleColor, headerRect);
            yOffset += titleFontSize + 6;
        }

        if (string.IsNullOrEmpty(entityId)
            || !data.WeatherForecasts.TryGetValue(entityId, out var forecastList)
            || forecastList.Count == 0)
        {
            DrawCenteredText(image, "Forecast", GetFont(titleFontSize, titleFontWeight), titleColor, contentRect);
            return;
        }

        var w = widget.Position.W;
        var h = widget.Position.H;
        var itemCount = maxItems ?? GetDefaultMaxItems(w, h, forecastMode);
        var items = forecastList.Take(itemCount).ToList();

        // Temperature unit
        var tempUnit = "°C";
        if (data.EntityStates.TryGetValue(entityId, out var es))
            tempUnit = GetEntityAttr(es, "temperature_unit") ?? "°C";

        // Distribute columns evenly
        if (items.Count == 0) return;
        var colWidth = contentRect.Width / items.Count;
        var lineHeight = textFontSize + 2;

        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] is not Dictionary<string, object?> dict) continue;
            var colX = contentRect.X + i * colWidth;
            float itemY = yOffset;

            var dt = dict.TryGetValue("datetime", out var dtVal) ? dtVal?.ToString() : "";
            if (visibleFields.Contains("time"))
            {
                var timeRect = new RectangleF(colX, itemY, colWidth, lineHeight);
                DrawTextCentered(image, FormatForecastTime(dt, forecastMode), GetFont(textFontSize, titleFontWeight), titleColor, timeRect);
                itemY += lineHeight + rowGap;
            }

            if (visibleFields.Contains("condition"))
            {
                var condStr = dict.TryGetValue("condition", out var cv) ? FormatCondition(cv?.ToString()) : "";
                var condRect = new RectangleF(colX, itemY, colWidth, lineHeight);
                DrawTextCentered(image, condStr, GetFont(textFontSize, textFontWeight), textColor, condRect);
                itemY += lineHeight + rowGap;
            }

            if (visibleFields.Contains("tempHigh"))
            {
                var temp = dict.TryGetValue("temperature", out var tVal) ? RoundNum(tVal) : "";
                var tempRect = new RectangleF(colX, itemY, colWidth, lineHeight);
                DrawTextCentered(image, $"{temp}{tempUnit}", GetFont(textFontSize, titleFontWeight), textColor, tempRect);
                itemY += lineHeight + rowGap;
            }

            if (visibleFields.Contains("tempLow") && forecastMode != "hourly")
            {
                var tempLow = dict.TryGetValue("templow", out var tlVal) ? RoundNum(tlVal) : "";
                if (!string.IsNullOrEmpty(tempLow))
                {
                    var tlRect = new RectangleF(colX, itemY, colWidth, lineHeight);
                    DrawTextCentered(image, $"{tempLow}{tempUnit}", GetFont(textFontSize, textFontWeight), textColor, tlRect);
                    itemY += lineHeight + rowGap;
                }
            }

            if (visibleFields.Contains("precipitation"))
            {
                var precip = dict.TryGetValue("precipitation_probability", out var ppVal) ? RoundNum(ppVal) : null;
                if (!string.IsNullOrEmpty(precip))
                {
                    var precipRect = new RectangleF(colX, itemY, colWidth, lineHeight);
                    DrawTextCentered(image, $"{precip}%", GetFont(textFontSize, textFontWeight), textColor, precipRect);
                    itemY += lineHeight + rowGap;
                }
            }

            if (visibleFields.Contains("wind"))
            {
                var windSpeed = dict.TryGetValue("wind_speed", out var wsVal) ? RoundNum(wsVal) : null;
                if (!string.IsNullOrEmpty(windSpeed))
                {
                    var windUnit = data.EntityStates.TryGetValue(entityId, out var wes) ? GetEntityAttr(wes, "wind_speed_unit") ?? "" : "";
                    var windRect = new RectangleF(colX, itemY, colWidth, lineHeight);
                    DrawTextCentered(image, $"{windSpeed} {windUnit}", GetFont(textFontSize, textFontWeight), textColor, windRect);
                }
            }
        }
    }

    // =============================================
    // TODO WIDGET
    // =============================================

    private void RenderTodoWidget(Image<Rgba32> image, WidgetConfigEntry widget, LayoutConfig layout, SsrData data, RectangleF contentRect)
    {
        var titleColor = ResolveWidgetColor(widget, layout, c => c.WidgetTitleTextColor, o => o?.WidgetTitleTextColor);
        var textColor = ResolveWidgetColor(widget, layout, c => c.WidgetTextColor, o => o?.WidgetTextColor);
        var iconColor = ResolveWidgetColor(widget, layout, c => c.IconColor, o => o?.IconColor);
        var titleFontSize = layout.TitleFontSize > 0 ? layout.TitleFontSize : 15;
        var textFontSize = layout.TextFontSize > 0 ? layout.TextFontSize : 12;
        var titleFontWeight = layout.TitleFontWeight > 0 ? layout.TitleFontWeight : 700;
        var textFontWeight = layout.TextFontWeight > 0 ? layout.TextFontWeight : 400;

        var entityId = GetStringProp(widget.Config, "entityId") ?? "";
        var showCompleted = GetBoolProp(widget.Config, "showCompleted") ?? true;
        var pendingIcon = GetStringProp(widget.Config, "pendingIcon") ?? "fa-circle";
        var completedIcon = GetStringProp(widget.Config, "completedIcon") ?? "fa-check-circle";
        var w = widget.Position.W;
        var h = widget.Position.H;

        if (string.IsNullOrEmpty(entityId) || !data.TodoItems.TryGetValue(entityId, out var items))
        {
            DrawCenteredText(image, "Tasks", GetFont(titleFontSize, titleFontWeight), titleColor, contentRect);
            return;
        }

        var mapped = items
            .Select(i => (i.Summary, Complete: i.Status is "completed" or "done"))
            .ToList();
        if (!showCompleted)
            mapped = mapped.Where(i => !i.Complete).ToList();
        mapped = mapped.OrderBy(i => i.Complete ? 1 : 0).ToList();

        // Compact mode: 1x1 shows count only
        if (w == 1 && h == 1)
        {
            var pendingCount = mapped.Count(i => !i.Complete);
            var listIconSize = Math.Min(contentRect.Width, contentRect.Height) * 0.3f;
            var iconBounds = new RectangleF(
                contentRect.X + (contentRect.Width - listIconSize) / 2f,
                contentRect.Y + contentRect.Height * 0.1f,
                listIconSize, listIconSize);
            DrawFaIcon(image, "fa-list-check", iconColor, iconBounds);

            var countRect = new RectangleF(contentRect.X, iconBounds.Bottom + 2, contentRect.Width, titleFontSize + 4);
            DrawTextCentered(image, pendingCount.ToString(), GetFont(titleFontSize, titleFontWeight), titleColor, countRect);

            var labelRect = new RectangleF(contentRect.X, countRect.Bottom, contentRect.Width, textFontSize + 2);
            DrawTextCentered(image, "Pending", GetFont(textFontSize - 2, textFontWeight), textColor, labelRect);
            return;
        }

        float yOffset = contentRect.Y;

        // Title
        if (widget.ShowTitle)
        {
            var friendlyName = "Tasks";
            if (data.EntityStates.TryGetValue(entityId, out var es))
                friendlyName = GetEntityAttr(es, "friendly_name") ?? "Tasks";
            var titleText = widget.TitleOverride ?? friendlyName;
            var titleRect = new RectangleF(contentRect.X, yOffset, contentRect.Width, titleFontSize + 4);
            DrawTextEllipsis(image, titleText, GetFont(titleFontSize, titleFontWeight), titleColor, titleRect);
            yOffset += titleFontSize + 6;
        }

        var maxShow = GetIntProp(widget.Config, "maxItems") ?? 50;
        var limited = mapped.Take(maxShow).ToList();
        var lineHeight = textFontSize + 4;
        var todoIconSize = textFontSize * 0.85f;

        foreach (var (summary, complete) in limited)
        {
            if (yOffset + lineHeight > contentRect.Bottom) break;

            // Draw configurable FA icon
            var itemIconClass = complete ? completedIcon : pendingIcon;
            var itemIconColor = complete ? ParseColor(layout.ColorScheme.IconColor + "99") : iconColor;
            var iconBounds = new RectangleF(
                contentRect.X + 2,
                yOffset + (lineHeight - todoIconSize) / 2f,
                todoIconSize, todoIconSize);
            DrawFaIcon(image, itemIconClass, itemIconColor, iconBounds);

            // Draw text
            var textX = iconBounds.Right + 6;
            var textRect = new RectangleF(textX, yOffset, contentRect.Right - textX, lineHeight);
            var itemColor = complete ? ParseColor(layout.ColorScheme.WidgetTextColor + "99") : textColor;
            DrawTextEllipsis(image, summary, GetFont(textFontSize, textFontWeight), itemColor, textRect);
            yOffset += lineHeight;
        }
    }

    // =============================================
    // MARKDOWN WIDGET
    // =============================================

    private void RenderMarkdownWidget(Image<Rgba32> image, WidgetConfigEntry widget, LayoutConfig layout, RectangleF contentRect)
    {
        var textColor = ResolveWidgetColor(widget, layout, c => c.WidgetTextColor, o => o?.WidgetTextColor);
        var textFontSize = layout.TextFontSize > 0 ? layout.TextFontSize : 14;
        var textFontWeight = layout.TextFontWeight > 0 ? layout.TextFontWeight : 400;
        var titleFontSize = layout.TitleFontSize > 0 ? layout.TitleFontSize : 16;
        var titleFontWeight = layout.TitleFontWeight > 0 ? layout.TitleFontWeight : 700;

        var content = GetStringProp(widget.Config, "content") ?? "";
        if (string.IsNullOrEmpty(content)) return;

        var lines = content.Split('\n');
        float yOffset = contentRect.Y;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');
            if (yOffset > contentRect.Bottom) break;

            int fontSize;
            int fontWeight;
            string text;
            float xIndent = 0;

            // Headings
            if (line.StartsWith("#### "))
            {
                fontSize = (int)(textFontSize * 1.05);
                fontWeight = titleFontWeight;
                text = StripInlineMarkdown(line[5..]);
            }
            else if (line.StartsWith("### "))
            {
                fontSize = (int)(textFontSize * 1.1);
                fontWeight = titleFontWeight;
                text = StripInlineMarkdown(line[4..]);
            }
            else if (line.StartsWith("## "))
            {
                fontSize = (int)(titleFontSize * 1.0);
                fontWeight = titleFontWeight;
                text = StripInlineMarkdown(line[3..]);
            }
            else if (line.StartsWith("# "))
            {
                fontSize = (int)(titleFontSize * 1.2);
                fontWeight = titleFontWeight;
                text = StripInlineMarkdown(line[2..]);
            }
            // Horizontal rules
            else if (Regex.IsMatch(line, @"^[-*_]{3,}\s*$"))
            {
                var lineY = yOffset + textFontSize / 2f;
                image.Mutate(ctx => ctx.DrawLine(
                    textColor, 1f,
                    new PointF(contentRect.X, lineY),
                    new PointF(contentRect.Right, lineY)));
                yOffset += textFontSize + 2;
                continue;
            }
            // Blockquotes
            else if (line.StartsWith("> ") || line == ">")
            {
                fontSize = textFontSize;
                fontWeight = textFontWeight;
                text = StripInlineMarkdown(line.Length > 2 ? line[2..] : "");
                xIndent = textFontSize * 0.8f;

                // Draw blockquote bar
                var barX = contentRect.X + xIndent * 0.3f;
                var barTop = yOffset;
                var barBottom = yOffset + fontSize + 4;
                image.Mutate(ctx => ctx.DrawLine(
                    textColor, 2f,
                    new PointF(barX, barTop),
                    new PointF(barX, barBottom)));
            }
            // Unordered lists
            else if (line.StartsWith("- ") || line.StartsWith("* ") || line.StartsWith("+ "))
            {
                fontSize = textFontSize;
                fontWeight = textFontWeight;
                text = $"• {StripInlineMarkdown(line[2..])}";
            }
            // Numbered lists (e.g. "1. item", "12. item")
            else if (Regex.IsMatch(line, @"^\d+\.\s"))
            {
                fontSize = textFontSize;
                fontWeight = textFontWeight;
                var match = Regex.Match(line, @"^(\d+\.)\s(.*)$");
                text = match.Success
                    ? $"{match.Groups[1].Value} {StripInlineMarkdown(match.Groups[2].Value)}"
                    : StripInlineMarkdown(line);
            }
            // Empty lines
            else if (string.IsNullOrWhiteSpace(line))
            {
                yOffset += textFontSize / 2f;
                continue;
            }
            // Regular paragraph text - strip inline markdown formatting
            else
            {
                fontSize = textFontSize;
                // Use bold weight if line is entirely bold
                fontWeight = IsEntirelyBold(line) ? titleFontWeight : textFontWeight;
                text = StripInlineMarkdown(line);
            }

            var lineHeight = fontSize + 4;
            var lineRect = new RectangleF(contentRect.X + xIndent, yOffset, contentRect.Width - xIndent, lineHeight);
            DrawTextEllipsis(image, text, GetFont(fontSize, fontWeight), textColor, lineRect);
            yOffset += lineHeight;
        }
    }

    /// <summary>
    /// Strips inline markdown formatting syntax, preserving the visible text content.
    /// Handles: bold, italic, strikethrough, inline code, links, and images.
    /// </summary>
    private static string StripInlineMarkdown(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        // Images: ![alt](url) → alt
        text = Regex.Replace(text, @"!\[([^\]]*)\]\([^)]*\)", "$1");
        // Links: [text](url) → text
        text = Regex.Replace(text, @"\[([^\]]*)\]\([^)]*\)", "$1");
        // Bold+italic: ***text*** or ___text___
        text = Regex.Replace(text, @"\*{3}(.+?)\*{3}", "$1");
        text = Regex.Replace(text, @"_{3}(.+?)_{3}", "$1");
        // Bold: **text** or __text__
        text = Regex.Replace(text, @"\*{2}(.+?)\*{2}", "$1");
        text = Regex.Replace(text, @"_{2}(.+?)_{2}", "$1");
        // Italic: *text* or _text_
        text = Regex.Replace(text, @"\*(.+?)\*", "$1");
        text = Regex.Replace(text, @"(?<=\s|^)_(.+?)_(?=\s|$)", "$1");
        // Strikethrough: ~~text~~
        text = Regex.Replace(text, @"~~(.+?)~~", "$1");
        // Inline code: `code`
        text = Regex.Replace(text, @"`(.+?)`", "$1");

        return text;
    }

    /// <summary>
    /// Checks if a line consists entirely of bold text (e.g. "**some text**").
    /// </summary>
    private static bool IsEntirelyBold(string line)
    {
        var trimmed = line.Trim();
        return (trimmed.StartsWith("**") && trimmed.EndsWith("**") && trimmed.Length > 4)
            || (trimmed.StartsWith("__") && trimmed.EndsWith("__") && trimmed.Length > 4);
    }

    // =============================================
    // RSS FEED WIDGET
    // =============================================

    private void RenderRssFeedWidget(Image<Rgba32> image, WidgetConfigEntry widget, LayoutConfig layout, SsrData data, RectangleF contentRect)
    {
        var titleColor = ResolveWidgetColor(widget, layout, c => c.WidgetTitleTextColor, o => o?.WidgetTitleTextColor);
        var textColor = ResolveWidgetColor(widget, layout, c => c.WidgetTextColor, o => o?.WidgetTextColor);
        var titleFontSize = layout.TitleFontSize > 0 ? layout.TitleFontSize : 16;
        var textFontSize = layout.TextFontSize > 0 ? layout.TextFontSize : 12;
        var titleFontWeight = layout.TitleFontWeight > 0 ? layout.TitleFontWeight : 700;
        var textFontWeight = layout.TextFontWeight > 0 ? layout.TextFontWeight : 400;
        var widgetBg = widget.ColorOverrides?.WidgetBackgroundColor ?? layout.ColorScheme.WidgetBackgroundColor;

        var entityId = GetStringProp(widget.Config, "entityId") ?? "";
        var feedTitle = GetStringProp(widget.Config, "title");

        if (string.IsNullOrEmpty(entityId)
            || !data.RssFeedEntries.TryGetValue(entityId, out var entries)
            || entries.Count == 0)
        {
            DrawCenteredText(image, "RSS Feed", GetFont(titleFontSize, titleFontWeight), titleColor, contentRect);
            return;
        }

        var entry = entries[0];
        float yOffset = contentRect.Y;

        // Feed title
        if (widget.ShowTitle && !string.IsNullOrEmpty(widget.TitleOverride ?? feedTitle))
        {
            var feedTitleRect = new RectangleF(contentRect.X, yOffset, contentRect.Width, titleFontSize + 4);
            DrawTextEllipsis(image, widget.TitleOverride ?? feedTitle!, GetFont(titleFontSize, titleFontWeight), titleColor, feedTitleRect);
            yOffset += titleFontSize + 8;
        }

        // Entry title
        var entryTitleRect = new RectangleF(contentRect.X, yOffset, contentRect.Width, textFontSize * 2 + 4);
        DrawTextEllipsis(image, entry.Title, GetFont(textFontSize, textFontWeight), titleColor, entryTitleRect);
        yOffset += textFontSize * 2 + 8;

        // QR code (rendered as ImageSharp image from QRCoder)
        if (!string.IsNullOrEmpty(entry.Link))
        {
            try
            {
                var qrSize = Math.Min(contentRect.Width, contentRect.Bottom - yOffset);
                if (qrSize > 20)
                {
                    var darkColor = ParseColor(layout.ColorScheme.Text);
                    var lightColor = ParseColor(widgetBg);
                    var qrImage = GenerateQrCodeImage(entry.Link, darkColor, lightColor, (int)qrSize);
                    if (qrImage != null)
                    {
                        var qrX = (int)(contentRect.X + (contentRect.Width - qrSize) / 2);
                        var qrY = (int)yOffset;
                        image.Mutate(ctx => ctx.DrawImage(qrImage, new SixLabors.ImageSharp.Point(qrX, qrY), 1f));
                        qrImage.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to render QR code for RSS entry");
            }
        }
    }

    // =============================================
    // VERSION WIDGET
    // =============================================

    private void RenderVersionWidget(Image<Rgba32> image, WidgetConfigEntry widget, LayoutConfig layout, RectangleF contentRect)
    {
        var textColor = ResolveWidgetColor(widget, layout, c => c.WidgetTextColor, o => o?.WidgetTextColor);
        var textFontSize = layout.TextFontSize > 0 ? layout.TextFontSize : 14;
        var textFontWeight = layout.TextFontWeight > 0 ? layout.TextFontWeight : 400;
        var version = typeof(DashboardImageRenderingService).Assembly.GetName().Version?.ToString() ?? "?";
        DrawCenteredText(image, $"v{version}", GetFont(textFontSize, textFontWeight), textColor, contentRect);
    }

    // =============================================
    // APP-ICON WIDGET
    // =============================================

    private void RenderAppIconWidget(Image<Rgba32> image, WidgetConfigEntry widget, LayoutConfig layout, SsrData data, RectangleF contentRect)
    {
        var iconColor = ResolveWidgetColor(widget, layout, c => c.IconColor, o => o?.IconColor);
        var size = GetIntProp(widget.Config, "size") ?? 64;

        // Center the icon in the content rect, capped to configured size
        var actualSize = Math.Min(size, Math.Min(contentRect.Width, contentRect.Height));
        var iconBounds = new RectangleF(
            contentRect.X + (contentRect.Width - actualSize) / 2f,
            contentRect.Y + (contentRect.Height - actualSize) / 2f,
            actualSize, actualSize);
        DrawAppIcon(image, iconColor, iconBounds);
    }

    // =============================================
    // IMAGE WIDGET
    // =============================================

    private void RenderImageWidget(Image<Rgba32> image, WidgetConfigEntry widget, LayoutConfig layout, RectangleF contentRect)
    {
        var imageUrl = GetStringProp(widget.Config, "imageUrl") ?? "";
        if (string.IsNullOrEmpty(imageUrl)) return;

        try
        {
            byte[] imageBytes;

            // Images are stored on disk and served via /api/dashboards/{id}/images/{file}
            // Load directly from disk instead of making an HTTP request to ourselves.
            var localMatch = System.Text.RegularExpressions.Regex.Match(
                imageUrl, @"^/api/dashboards/([^/]+)/images/([^/]+)$");
            if (localMatch.Success)
            {
                var dashId = localMatch.Groups[1].Value;
                var fileName = localMatch.Groups[2].Value;
                // Guard against traversal
                if (fileName.Contains("..") || fileName.Contains('/') || fileName.Contains('\\'))
                    return;
                var filePath = System.IO.Path.Combine(
                    Utilities.EnvironmentConfiguration.ConfigDir, "uploads", dashId, fileName);
                if (!File.Exists(filePath))
                {
                    _logger.LogWarning("Image file not found on disk: {Path}", filePath);
                    return;
                }
                imageBytes = File.ReadAllBytes(filePath);
            }
            else
            {
                // Fallback: external URL
                using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                imageBytes = httpClient.GetByteArrayAsync(imageUrl).GetAwaiter().GetResult();
            }

            using var srcImage = Image.Load<Rgba32>(imageBytes);

            var zoom = GetDoubleProp(widget.Config, "zoom") ?? 1.0;
            var panX = GetDoubleProp(widget.Config, "offsetX") ?? 0.0;
            var panY = GetDoubleProp(widget.Config, "offsetY") ?? 0.0;

            var containerW = contentRect.Width;
            var containerH = contentRect.Height;

            // The Angular component sets the img element to (zoom * 100%) of the container,
            // then uses object-fit: contain to preserve aspect ratio.
            // Replicate: first compute the "virtual img element" size, then fit the
            // actual image within that while maintaining aspect ratio.
            var imgElW = containerW * (float)zoom;
            var imgElH = containerH * (float)zoom;

            // Fit the source image within the virtual img element (object-fit: contain)
            float srcAspect = (float)srcImage.Width / srcImage.Height;
            float elAspect = imgElW / imgElH;

            float drawW, drawH;
            if (srcAspect > elAspect)
            {
                // Source is wider → constrained by width
                drawW = imgElW;
                drawH = imgElW / srcAspect;
            }
            else
            {
                // Source is taller → constrained by height
                drawH = imgElH;
                drawW = imgElH * srcAspect;
            }

            // Center the fitted image within the virtual element
            float fitOffsetX = (imgElW - drawW) / 2f;
            float fitOffsetY = (imgElH - drawH) / 2f;

            // Angular positions the img element at:
            //   left = -((zoom - 1) * (offsetX + 1) * 50)%   of container width
            //   top  = -((zoom - 1) * (offsetY + 1) * 50)%   of container height
            float elLeft = -(float)((zoom - 1) * (panX + 1) * 50.0 / 100.0) * containerW;
            float elTop = -(float)((zoom - 1) * (panY + 1) * 50.0 / 100.0) * containerH;

            // Final draw position = container origin + element offset + fit centering
            float drawX = contentRect.X + elLeft + fitOffsetX;
            float drawY = contentRect.Y + elTop + fitOffsetY;

            // Resize source image to the fitted dimensions
            var resizedW = Math.Max(1, (int)Math.Round(drawW));
            var resizedH = Math.Max(1, (int)Math.Round(drawH));
            srcImage.Mutate(ctx => ctx.Resize(new SixLabors.ImageSharp.Size(resizedW, resizedH)));

            image.Mutate(ctx => ctx.DrawImage(srcImage, new SixLabors.ImageSharp.Point((int)drawX, (int)drawY), 1f));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load image from URL: {Url}", imageUrl);
            var textColor = ResolveWidgetColor(widget, layout, c => c.WidgetTextColor, o => o?.WidgetTextColor);
            DrawCenteredText(image, "Image", GetFont(layout.TextFontSize > 0 ? layout.TextFontSize : 12), textColor, contentRect);
        }
    }

    // =============================================
    // GRAPH WIDGET
    // =============================================

    private void RenderGraphWidget(Image<Rgba32> image, WidgetConfigEntry widget, LayoutConfig layout, SsrData data, RectangleF contentRect)
    {
        var textColor = ResolveWidgetColor(widget, layout, c => c.WidgetTextColor, o => o?.WidgetTextColor);
        var titleColor = ResolveWidgetColor(widget, layout, c => c.WidgetTitleTextColor, o => o?.WidgetTitleTextColor);
        var textFontSize = layout.TextFontSize > 0 ? layout.TextFontSize : 12;
        var gridColorStr = (widget.ColorOverrides?.WidgetBorderColor ?? layout.ColorScheme.WidgetBorderColor);

        var plotType = GetStringProp(widget.Config, "plotType") ?? "line";
        var lineWidth = GetIntProp(widget.Config, "lineWidth") ?? 2;
        var barWidth = GetIntProp(widget.Config, "barWidth") ?? 2;

        var seriesList = new List<(string EntityId, string Label, string Color)>();
        if (widget.Config.TryGetProperty("series", out var series) && series.ValueKind == JsonValueKind.Array)
        {
            int idx = 0;
            foreach (var s in series.EnumerateArray())
            {
                var sEntityId = GetStringProp(s, "entityId") ?? "";
                var sLabel = GetStringProp(s, "label") ?? sEntityId;
                var sColor = GetStringProp(s, "color") ?? GetDefaultSeriesColor(layout.ColorScheme, idx);
                if (!string.IsNullOrEmpty(sEntityId))
                    seriesList.Add((sEntityId, sLabel, sColor));
                idx++;
            }
        }

        var hasData = seriesList.Any(s => data.HistoryData.ContainsKey(s.EntityId) && data.HistoryData[s.EntityId].Count > 0);
        if (!hasData)
        {
            DrawCenteredText(image, "Graph", GetFont(textFontSize), titleColor, contentRect);
            return;
        }

        // Collect all data points
        var allValues = new List<double>();
        var allTimestamps = new List<DateTime>();
        foreach (var (entityId, _, _) in seriesList)
        {
            if (!data.HistoryData.TryGetValue(entityId, out var states)) continue;
            foreach (var s in states)
            {
                allValues.Add(s.NumericValue);
                allTimestamps.Add(s.LastChanged);
            }
        }

        if (allValues.Count == 0) return;

        var minVal = allValues.Min();
        var maxVal = allValues.Max();
        if (Math.Abs(maxVal - minVal) < 0.001) { minVal -= 1; maxVal += 1; }
        var valRange = maxVal - minVal;

        var minTime = allTimestamps.Min();
        var maxTime = allTimestamps.Max();
        var timeRange = (maxTime - minTime).TotalSeconds;
        if (timeRange < 1) timeRange = 1;

        var padL = Math.Max(35, textFontSize * 4);
        var padR = 10f;
        var padT = 10f;
        var padB = Math.Max(20, textFontSize + 10);
        var plotW = contentRect.Width - padL - padR;
        var plotH = contentRect.Height - padT - padB;
        var originX = contentRect.X + padL;
        var originY = contentRect.Y + padT;

        var gridColor = ParseColor(gridColorStr + "33");
        var labelFont = GetFont(Math.Max(8, textFontSize - 2));

        // Grid lines
        image.Mutate(ctx =>
        {
            for (int i = 0; i <= 3; i++)
            {
                var y = originY + plotH * i / 3f;
                ctx.DrawLine(gridColor, 0.5f, new PointF(originX, y), new PointF(originX + plotW, y));

                var val = maxVal - (valRange * i / 3.0);
                var labelRect = new RectangleF(contentRect.X, y - textFontSize / 2f, padL - 4, textFontSize);
                DrawTextAligned(ctx, image, $"{val:F0}", labelFont, textColor, labelRect, HorizontalAlignment.Right);
            }

            // X axis
            ctx.DrawLine(gridColor, 0.5f,
                new PointF(originX, originY + plotH),
                new PointF(originX + plotW, originY + plotH));
        });

        // X axis labels
        for (int i = 0; i <= 4; i++)
        {
            var t = minTime.AddSeconds(timeRange * i / 4.0);
            var x = originX + plotW * i / 4f;
            var labelRect = new RectangleF(x - 20, originY + plotH + 4, 40, textFontSize + 4);
            DrawTextCentered(image, t.ToString("HH:mm"), labelFont, textColor, labelRect);
        }

        // Render series
        foreach (var (entityId, label, color) in seriesList)
        {
            if (!data.HistoryData.TryGetValue(entityId, out var states) || states.Count == 0) continue;
            var seriesColor = ParseColor(color);
            var ordered = states.OrderBy(s => s.LastChanged).ToList();

            if (plotType == "bar")
            {
                var bw = Math.Max(2, plotW / (ordered.Count + 1));
                image.Mutate(ctx =>
                {
                    foreach (var s in ordered)
                    {
                        var xFrac = (float)((s.LastChanged - minTime).TotalSeconds / timeRange);
                        var x = originX + xFrac * plotW;
                        var yFrac = (float)((s.NumericValue - minVal) / valRange);
                        var barH = yFrac * plotH;
                        var y = originY + plotH - barH;
                        ctx.Fill(seriesColor, new RectangularPolygon(x, y, bw, barH));
                    }
                });
            }
            else
            {
                // Line chart
                if (ordered.Count < 2) continue;
                var points = ordered.Select(s =>
                {
                    var xFrac = (float)((s.LastChanged - minTime).TotalSeconds / timeRange);
                    var yFrac = (float)((s.NumericValue - minVal) / valRange);
                    return new PointF(originX + xFrac * plotW, originY + plotH - yFrac * plotH);
                }).ToArray();

                image.Mutate(ctx => ctx.DrawLine(seriesColor, lineWidth, points));
            }
        }
    }

    // =============================================
    // PLACEHOLDER (for unsupported widget types)
    // =============================================

    private void RenderPlaceholder(Image<Rgba32> image, WidgetConfigEntry widget, LayoutConfig layout, RectangleF contentRect, string label)
    {
        var textColor = ResolveWidgetColor(widget, layout, c => c.WidgetTextColor, o => o?.WidgetTextColor);
        var fontSize = layout.TextFontSize > 0 ? layout.TextFontSize : 14;
        DrawCenteredText(image, label, GetFont(fontSize), textColor, contentRect);
    }

    // =============================================
    // TEXT DRAWING HELPERS
    // =============================================

    /// <summary>
    /// Draws text within a bounding rectangle, truncating with ellipsis if it would overflow.
    /// </summary>
    private void DrawTextEllipsis(Image<Rgba32> image, string text, Font font, Color color, RectangleF bounds)
    {
        if (string.IsNullOrEmpty(text) || bounds.Width <= 0 || bounds.Height <= 0)
            return;

        var measuredSize = TextMeasurer.MeasureSize(text, new TextOptions(font));

        // If text fits, draw it directly
        if (measuredSize.Width <= bounds.Width)
        {
            var y = bounds.Y + (bounds.Height - measuredSize.Height) / 2f;
            image.Mutate(ctx => ctx.DrawText(text, font, color, new PointF(bounds.X, y)));
            return;
        }

        // Truncate with ellipsis
        var ellipsis = "…";
        var ellipsisSize = TextMeasurer.MeasureSize(ellipsis, new TextOptions(font));
        var availableWidth = bounds.Width - ellipsisSize.Width;

        if (availableWidth <= 0)
        {
            // Not even room for ellipsis — draw what fits
            var cy = bounds.Y + (bounds.Height - measuredSize.Height) / 2f;
            image.Mutate(ctx => ctx.DrawText(
                new RichTextOptions(font)
                {
                    Origin = new PointF(bounds.X, cy),
                    WrappingLength = bounds.Width,
                    WordBreaking = WordBreaking.BreakAll,
                },
                text, new SolidBrush(color), null));
            return;
        }

        // Binary search for the truncation point
        int lo = 0, hi = text.Length;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            var subSize = TextMeasurer.MeasureSize(text[..mid], new TextOptions(font));
            if (subSize.Width <= availableWidth)
                lo = mid;
            else
                hi = mid - 1;
        }

        var truncated = lo > 0 ? text[..lo] + ellipsis : ellipsis;
        var truncSize = TextMeasurer.MeasureSize(truncated, new TextOptions(font));
        var ty = bounds.Y + (bounds.Height - truncSize.Height) / 2f;
        image.Mutate(ctx => ctx.DrawText(truncated, font, color, new PointF(bounds.X, ty)));
    }

    /// <summary>
    /// Draws text centered within a bounding rectangle.
    /// </summary>
    private void DrawCenteredText(Image<Rgba32> image, string text, Font font, Color color, RectangleF bounds)
    {
        if (string.IsNullOrEmpty(text)) return;
        var size = TextMeasurer.MeasureSize(text, new TextOptions(font));
        var x = bounds.X + (bounds.Width - size.Width) / 2f;
        var y = bounds.Y + (bounds.Height - size.Height) / 2f;
        image.Mutate(ctx => ctx.DrawText(text, font, color, new PointF(x, y)));
    }

    /// <summary>
    /// Draws text centered horizontally within a bounding rectangle, vertically centered.
    /// </summary>
    private void DrawTextCentered(Image<Rgba32> image, string text, Font font, Color color, RectangleF bounds)
    {
        if (string.IsNullOrEmpty(text)) return;
        var size = TextMeasurer.MeasureSize(text, new TextOptions(font));

        // Truncate if too wide
        if (size.Width > bounds.Width)
        {
            DrawTextEllipsis(image, text, font, color, bounds);
            return;
        }

        var x = bounds.X + (bounds.Width - size.Width) / 2f;
        var y = bounds.Y + (bounds.Height - size.Height) / 2f;
        image.Mutate(ctx => ctx.DrawText(text, font, color, new PointF(x, y)));
    }

    /// <summary>
    /// Draws text with horizontal alignment within a bounding rectangle.
    /// </summary>
    private static void DrawTextAligned(IImageProcessingContext ctx, Image<Rgba32> image, string text, Font font, Color color, RectangleF bounds, HorizontalAlignment alignment)
    {
        if (string.IsNullOrEmpty(text)) return;
        var size = TextMeasurer.MeasureSize(text, new TextOptions(font));
        var x = alignment switch
        {
            HorizontalAlignment.Right => bounds.Right - size.Width,
            HorizontalAlignment.Center => bounds.X + (bounds.Width - size.Width) / 2f,
            _ => bounds.X
        };
        var y = bounds.Y + (bounds.Height - size.Height) / 2f;
        ctx.DrawText(text, font, color, new PointF(x, y));
    }

    private enum HorizontalAlignment { Left, Center, Right }

    // =============================================
    // FA ICON DRAWING
    // =============================================

    /// <summary>
    /// Draws a Font Awesome icon (from the embedded registry) scaled to fit the given bounds.
    /// </summary>
    private static void DrawFaIcon(Image<Rgba32> image, string? iconClass, Color color, RectangleF bounds)
    {
        if (string.IsNullOrEmpty(iconClass) || bounds.Width <= 0 || bounds.Height <= 0)
            return;

        // Strip "fa-" prefix
        var key = iconClass.StartsWith("fa-", StringComparison.OrdinalIgnoreCase)
            ? iconClass[3..]
            : iconClass;

        if (!FaIcons.TryGetValue(key, out var entry))
            return;

        try
        {
            var path = SvgPathParser.Parse(entry.Path);
            var pathBounds = path.Bounds;
            if (pathBounds.Width < 0.1f || pathBounds.Height < 0.1f)
                return;

            // Use viewBox dimensions for scaling
            var vbW = entry.VbW;
            var vbH = entry.VbH;

            var scale = Math.Min(bounds.Width / vbW, bounds.Height / vbH);
            var offsetX = bounds.X + (bounds.Width - vbW * scale) / 2f;
            var offsetY = bounds.Y + (bounds.Height - vbH * scale) / 2f;

            var matrix = Matrix3x2.CreateScale(scale) *
                         Matrix3x2.CreateTranslation(offsetX, offsetY);

            var transformed = path.Transform(matrix);
            image.Mutate(ctx => ctx.Fill(color, transformed));
        }
        catch
        {
            // Silently ignore icon rendering failures
        }
    }

    /// <summary>
    /// Draws the app dashboard icon directly using ImageSharp primitives.
    /// Reproduces the layout from icon-tab-dynamic.svg (viewBox 0 0 370 370):
    /// a 2-column grid of rounded rectangles and two diagonal polygons.
    /// </summary>
    private static void DrawAppIcon(Image<Rgba32> image, Color accentColor, RectangleF bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        const float vb = 370f;
        var scale = Math.Min(bounds.Width / vb, bounds.Height / vb);
        var ox = bounds.X + (bounds.Width - vb * scale) / 2f;
        var oy = bounds.Y + (bounds.Height - vb * scale) / 2f;

        // Accent color shades (matching the SVG CSS classes)
        var p = accentColor.ToPixel<Rgba32>();
        var darkest  = new Color(new Rgba32((byte)(p.R * 0.3f), (byte)(p.G * 0.3f), (byte)(p.B * 0.3f), p.A));
        var darker   = new Color(new Rgba32((byte)(p.R * 0.7f), (byte)(p.G * 0.7f), (byte)(p.B * 0.7f), p.A));
        var baseC    = accentColor;
        var light    = new Color(new Rgba32((byte)(p.R + (255 - p.R) * 0.2f), (byte)(p.G + (255 - p.G) * 0.2f), (byte)(p.B + (255 - p.B) * 0.2f), p.A));
        var lighter  = new Color(new Rgba32((byte)(p.R + (255 - p.R) * 0.4f), (byte)(p.G + (255 - p.G) * 0.4f), (byte)(p.B + (255 - p.B) * 0.4f), p.A));
        var lightest = new Color(new Rgba32((byte)(p.R + (255 - p.R) * 0.6f), (byte)(p.G + (255 - p.G) * 0.6f), (byte)(p.B + (255 - p.B) * 0.6f), p.A));

        // Shape definitions: (x, y, w, h, rx, color)
        (float x, float y, float w, float h, float rx, Color c)[] rects =
        [
            (20, 20, 90, 96, 4, darkest),   // top-left small
            (20, 128, 90, 196, 4, darker),   // left tall
            (122, 20, 134, 96, 4, baseC),    // top-center wide
            (268, 20, 82, 96, 4, light),     // top-right
            (122, 236, 84, 88, 4, light),    // bottom-left
            (218, 236, 132, 88, 4, lighter), // bottom-right
        ];

        foreach (var (rx, ry, rw, rh, rrx, color) in rects)
        {
            var x1 = rx * scale + ox;
            var y1 = ry * scale + oy;
            var w1 = rw * scale;
            var h1 = rh * scale;
            var cr = Math.Min(rrx * scale, Math.Min(w1, h1) / 2f);
            image.Mutate(ctx => ctx.Fill(color, BuildRoundedRect(x1, y1, w1, h1, cr)));
        }

        // Middle row diagonal split — two trapezoid polygons
        // At icon scale the rounded-corner clip is sub-pixel, so draw directly.
        // Left trapezoid (lightest)
        PointF[] leftPoly = [
            new(122 * scale + ox, 128 * scale + oy),
            new(256 * scale + ox, 128 * scale + oy),
            new(206 * scale + ox, 224 * scale + oy),
            new(122 * scale + ox, 224 * scale + oy),
        ];
        // Right trapezoid (lighter)
        PointF[] rightPoly = [
            new(268 * scale + ox, 128 * scale + oy),
            new(350 * scale + ox, 128 * scale + oy),
            new(350 * scale + ox, 224 * scale + oy),
            new(218 * scale + ox, 224 * scale + oy),
        ];

        image.Mutate(ctx =>
        {
            ctx.Fill(lightest, new Polygon(new LinearLineSegment(leftPoly)));
            ctx.Fill(lighter, new Polygon(new LinearLineSegment(rightPoly)));
        });
    }

    /// <summary>
    /// Builds a rounded rectangle IPath from position, size, and corner radius.
    /// </summary>
    private static IPath BuildRoundedRect(float x, float y, float w, float h, float cr)
    {
        if (cr < 0.5f)
            return new RectangularPolygon(x, y, w, h);

        cr = Math.Min(cr, Math.Min(w, h) / 2f);
        var pb = new PathBuilder();
        pb.MoveTo(new PointF(x + cr, y));
        pb.LineTo(new PointF(x + w - cr, y));
        pb.ArcTo(cr, cr, 0, false, true, new PointF(x + w, y + cr));
        pb.LineTo(new PointF(x + w, y + h - cr));
        pb.ArcTo(cr, cr, 0, false, true, new PointF(x + w - cr, y + h));
        pb.LineTo(new PointF(x + cr, y + h));
        pb.ArcTo(cr, cr, 0, false, true, new PointF(x, y + h - cr));
        pb.LineTo(new PointF(x, y + cr));
        pb.ArcTo(cr, cr, 0, false, true, new PointF(x + cr, y));
        pb.CloseFigure();
        return pb.Build();
    }

    // =============================================
    // QR CODE GENERATION
    // =============================================

    private Image<Rgba32>? GenerateQrCodeImage(string url, Color darkColor, Color lightColor, int size)
    {
        try
        {
            var qrGenerator = new QRCodeGenerator();
            var qrCodeData = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.L);

            // Use PNG QR code and load into ImageSharp
            var pngQrCode = new PngByteQRCode(qrCodeData);
            var darkRgba = darkColor.ToPixel<Rgba32>();
            var lightRgba = lightColor.ToPixel<Rgba32>();
            var pngBytes = pngQrCode.GetGraphic(
                20,
                new byte[] { darkRgba.R, darkRgba.G, darkRgba.B, darkRgba.A },
                new byte[] { lightRgba.R, lightRgba.G, lightRgba.B, lightRgba.A });

            var qrImage = Image.Load<Rgba32>(pngBytes);
            qrImage.Mutate(ctx => ctx.Resize(new SixLabors.ImageSharp.Size(size, size)));
            return qrImage;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate QR code for URL: {Url}", url);
            return null;
        }
    }

    // =============================================
    // PIXEL POSITION RESOLUTION (matches HTML service)
    // =============================================

    private static (double X, double Y, double Width, double Height) ResolvePixelPosition(WidgetPositionConfig pos, LayoutConfig layout)
    {
        if (pos.PixelX.HasValue && pos.PixelY.HasValue && pos.PixelWidth.HasValue && pos.PixelHeight.HasValue)
        {
            return (pos.PixelX.Value, pos.PixelY.Value, pos.PixelWidth.Value, pos.PixelHeight.Value);
        }

        var padding = layout.CanvasPadding;
        var gap = layout.WidgetGap;
        var cols = Math.Max(1, layout.GridCols);
        var rows = Math.Max(1, layout.GridRows);
        var innerWidth = Math.Max(0, layout.Width - padding * 2 - gap * (cols - 1));
        var innerHeight = Math.Max(0, layout.Height - padding * 2 - gap * (rows - 1));
        var cellWidth = (double)innerWidth / cols;
        var cellHeight = (double)innerHeight / rows;

        var x = padding + pos.X * (cellWidth + gap);
        var y = padding + pos.Y * (cellHeight + gap);
        var w = pos.W * cellWidth + (pos.W - 1) * gap;
        var h = pos.H * cellHeight + (pos.H - 1) * gap;

        return (Math.Round(x * 100) / 100, Math.Round(y * 100) / 100, Math.Round(w * 100) / 100, Math.Round(h * 100) / 100);
    }

    // =============================================
    // COLOR HELPERS
    // =============================================

    private static Color ParseColor(string hex)
    {
        if (string.IsNullOrEmpty(hex))
            return Color.Black;

        try
        {
            return Color.ParseHex(hex);
        }
        catch
        {
            return Color.Black;
        }
    }

    private static Color ResolveWidgetColor(
        WidgetConfigEntry widget,
        LayoutConfig layout,
        Func<ColorSchemeConfig, string> schemeSelector,
        Func<WidgetColorOverridesConfig?, string?> overrideSelector)
    {
        var hex = overrideSelector(widget.ColorOverrides) ?? schemeSelector(layout.ColorScheme);
        return ParseColor(hex);
    }

    // =============================================
    // JSON PROPERTY HELPERS (mirrors HTML service)
    // =============================================

    private static string? GetStringProp(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static int? GetIntProp(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var p) && p.ValueKind == JsonValueKind.Number ? p.GetInt32() : null;

    private static double? GetDoubleProp(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var p) && p.ValueKind == JsonValueKind.Number ? p.GetDouble() : null;

    private static bool? GetBoolProp(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var p)
            ? p.ValueKind == JsonValueKind.True ? true : p.ValueKind == JsonValueKind.False ? false : null
            : null;

    private static string[]? GetStringArrayProp(JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var p) || p.ValueKind != JsonValueKind.Array)
            return null;
        return p.EnumerateArray()
            .Where(v => v.ValueKind == JsonValueKind.String)
            .Select(v => v.GetString()!)
            .ToArray();
    }

    private static double? GetBadgeDoubleProp(JsonElement badge, string prop) =>
        badge.TryGetProperty(prop, out var p) && p.ValueKind == JsonValueKind.Number ? p.GetDouble() : null;

    private static string? GetEntityAttr(HassEntityState state, string key)
    {
        if (state.Attributes.TryGetValue(key, out var val) && val != null)
        {
            return val switch
            {
                string s => s,
                long l => l.ToString(CultureInfo.InvariantCulture),
                double d => d.ToString(CultureInfo.InvariantCulture),
                bool b => b ? "true" : "false",
                _ => val.ToString()
            };
        }
        return null;
    }

    // =============================================
    // WEATHER ITEMS PARSING (mirrors HTML service)
    // =============================================

    private record WeatherItemEntry(string Type, bool Visible, double X, double Y, double W, double H, string? AttributeKey, string? Label, string? Icon);

    private List<WeatherItemEntry> GetWeatherItems(JsonElement config)
    {
        var defaults = new List<WeatherItemEntry>
        {
            new("title", true, 0, 0, 100, 20, null, null, null),
            new("temperature", true, 0, 22, 50, 20, null, null, "fa-temperature-half"),
            new("condition", true, 50, 22, 50, 20, null, null, "fa-cloud-sun"),
            new("pressure", true, 0, 44, 50, 20, null, null, "fa-gauge"),
            new("attribute", true, 50, 44, 50, 20, "humidity", "Humidity", "fa-droplet"),
        };

        if (config.TryGetProperty("items", out var itemsEl) && itemsEl.ValueKind == JsonValueKind.Array)
        {
            var result = new List<WeatherItemEntry>();
            foreach (var el in itemsEl.EnumerateArray())
            {
                var type = el.TryGetProperty("type", out var tProp) ? tProp.GetString() ?? "" : "";
                var visible = !el.TryGetProperty("visible", out var vProp) || vProp.ValueKind != JsonValueKind.False;
                var x = el.TryGetProperty("x", out var xProp) ? xProp.GetDouble() : 0;
                var y = el.TryGetProperty("y", out var yProp) ? yProp.GetDouble() : 0;
                var w = el.TryGetProperty("w", out var wProp) ? wProp.GetDouble() : 100;
                var h = el.TryGetProperty("h", out var hProp) ? hProp.GetDouble() : 20;
                var attrKey = el.TryGetProperty("attributeKey", out var akProp) ? akProp.GetString() : null;
                var label = el.TryGetProperty("label", out var lProp) ? lProp.GetString() : null;
                var icon = el.TryGetProperty("icon", out var iProp) ? iProp.GetString() : null;
                result.Add(new WeatherItemEntry(type, visible, x, y, w, h, attrKey, label, icon));
            }
            return result;
        }

        return defaults;
    }

    // =============================================
    // FORMAT HELPERS (mirrors HTML service)
    // =============================================

    private static string FormatEventDate(string? dateStr)
    {
        if (string.IsNullOrEmpty(dateStr)) return "";
        if (DateTimeOffset.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
        {
            return dateStr.Length == 10
                ? dt.ToString("ddd, MMM d", CultureInfo.InvariantCulture)
                : dt.ToString("MMM d, HH:mm", CultureInfo.InvariantCulture);
        }
        return dateStr;
    }

    private static string FormatForecastTime(string? datetime, string mode)
    {
        if (string.IsNullOrEmpty(datetime)) return "";
        if (!DateTimeOffset.TryParse(datetime, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            return datetime;
        return mode switch
        {
            "hourly" => dt.ToString("HH:mm"),
            "weekly" => dt.ToString("ddd"),
            _ => dt.Day.ToString()
        };
    }

    private static string FormatCondition(string? condition)
    {
        if (string.IsNullOrEmpty(condition)) return "";
        return condition.ToLower() switch
        {
            "clear-night" => "Clear",
            "cloudy" => "Cloudy",
            "fog" => "Fog",
            "hail" => "Hail",
            "lightning" => "Storm",
            "lightning-rainy" => "Stormy",
            "partlycloudy" => "Pt. Cloudy",
            "pouring" => "Pouring",
            "rainy" => "Rainy",
            "snowy" => "Snowy",
            "snowy-rainy" => "Snowy Rain",
            "sunny" => "Sunny",
            "windy" => "Windy",
            "windy-variant" => "Windy",
            "exceptional" => "Exceptional",
            _ => condition
        };
    }

    private static string RoundNum(object? val)
    {
        if (val == null) return "";
        if (val is long l) return l.ToString();
        if (val is double d) return Math.Round(d).ToString(CultureInfo.InvariantCulture);
        if (double.TryParse(val.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var num))
            return Math.Round(num).ToString(CultureInfo.InvariantCulture);
        return val.ToString() ?? "";
    }

    private static int GetDefaultMaxItems(int w, int h, string mode)
    {
        if (w == 1 && h == 1) return 0;
        if (h == 1) return mode switch
        {
            "hourly" => Math.Min(4, w * 2),
            "daily" => Math.Min(2, w),
            "weekly" => 1,
            _ => 2
        };
        if (h == 2) return mode switch
        {
            "hourly" => w switch { 1 => 3, 2 => 5, _ => 7 },
            "daily" => w switch { 1 => 2, 2 => 3, _ => 4 },
            "weekly" => w switch { 1 => 1, 2 => 2, _ => 3 },
            _ => 3
        };
        return mode switch
        {
            "hourly" => w switch { 1 => 4, 2 => 6, _ => 8 },
            "daily" => w switch { 1 => 2, 2 => 4, _ => 5 },
            "weekly" => w switch { 1 => 1, 2 => 2, _ => 4 },
            _ => 3
        };
    }

    private static string GetDefaultSeriesColor(ColorSchemeConfig cs, int index)
    {
        var chartColors = cs.Palette
            .Where(c => !string.IsNullOrEmpty(c) && c != cs.Background && c != cs.CanvasBackgroundColor)
            .ToArray();
        if (chartColors.Length > 0)
            return chartColors[index % chartColors.Length];
        var fallback = new[] { "#ff0000", "#00ff00", "#0000ff", "#ffff00", "#ff00ff", "#00ffff" };
        return fallback[index % fallback.Length];
    }
}
