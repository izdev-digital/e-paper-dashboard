using EPaperDashboard.Services.Rendering;
using EPaperDashboard.Services.Providers;
using EPaperDashboard.Services.Providers.HomeAssistant;
using EPaperDashboard.Services.Ai;
using EPaperDashboard.Utilities;
using EPaperDashboard.Data.LiteDb;
using EPaperDashboard.Data.Repositories;
using Microsoft.AspNetCore.Authentication.Cookies;
using EPaperDashboard.Services;
using EPaperDashboard.Services.Firmware;
using EPaperDashboard.Authentication;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.FileProviders;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Register deployment strategy based on APP_MODE
switch (EnvironmentConfiguration.AppMode)
{
	case DeploymentMode.Addon:
		builder.Services.AddSingleton<IDeploymentStrategy, HomeAssistantAddonStrategy>();
		break;
	case DeploymentMode.Host:
		builder.Services.AddSingleton<IDeploymentStrategy, HostModeStrategy>();
		break;
	default:
		builder.Services.AddSingleton<IDeploymentStrategy, StandaloneStrategy>();
		break;
}

// Validate configuration using strategy
IDeploymentStrategy validationStrategy = EnvironmentConfiguration.AppMode switch
{
	DeploymentMode.Addon => new HomeAssistantAddonStrategy(new Microsoft.Extensions.Logging.Abstractions.NullLogger<HomeAssistantAddonStrategy>()),
	DeploymentMode.Host => new HostModeStrategy(new Microsoft.Extensions.Logging.Abstractions.NullLogger<HostModeStrategy>()),
	_ => new StandaloneStrategy(new Microsoft.Extensions.Logging.Abstractions.NullLogger<StandaloneStrategy>())
};

var validationResult = validationStrategy.ValidateConfiguration();
if (validationResult.IsFailure)
{
	Console.Error.WriteLine($"Configuration Error: {validationResult.Error}");
	Environment.Exit(1);
}

var dataProtectionKeysDir = EnvironmentConfiguration.DataProtectionKeysDir;
Directory.CreateDirectory(dataProtectionKeysDir);
builder.Services.AddDataProtection()
	.PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysDir));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddControllers()
	.AddJsonOptions(options =>
	{
		options.JsonSerializerOptions.Converters.Add(new TimeOnlyJsonConverter());
	});

#if DEBUG
builder.Services.AddSwaggerGen(options =>
{
	options.AddSecurityDefinition("ApiKey", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
	{
		Description = "API Key needed to access the endpoints. X-Api-Key: {apiKey}",
		In = Microsoft.OpenApi.Models.ParameterLocation.Header,
		Name = "X-Api-Key",
		Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
		Scheme = "ApiKeyScheme"
	});
	options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
	{
		{
			new Microsoft.OpenApi.Models.OpenApiSecurityScheme
			{
				Reference = new Microsoft.OpenApi.Models.OpenApiReference
				{
					Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
					Id = "ApiKey"
				},
				Scheme = "ApiKeyScheme",
				Name = "X-Api-Key",
				In = Microsoft.OpenApi.Models.ParameterLocation.Header
			},
			new List<string>()
		}
	});
});
#endif

builder.Services
	.AddMemoryCache()
	.AddTransient<IPageToImageRenderingService, PageToImageRenderingService>()
	.AddSingleton<IImageFactory, ImageFactory>()
	.AddSingleton<LiteDbContext>()
	.AddSingleton<IUserRepository, LiteDbUserRepository>()
	.AddSingleton<IDashboardRepository, LiteDbDashboardRepository>()
	.AddSingleton<IDeviceRepository, LiteDbDeviceRepository>()
	.AddSingleton<IPairingSessionRepository, LiteDbPairingSessionRepository>()
	.AddSingleton<UserService>()
	.AddSingleton<DashboardService>()
	.AddSingleton<DeviceService>()
	.AddSingleton<PairingService>()
	.AddSingleton<HomeAssistantAuthService>()
	.AddSingleton<HomeAssistantConnectionService>()
	.AddSingleton<HomeAssistantService>()
	.AddSingleton<IEntityStateProvider, HomeAssistantEntityStateProvider>()
	.AddSingleton<ITodoDataProvider, HomeAssistantTodoDataProvider>()
	.AddSingleton<ICalendarDataProvider, HomeAssistantCalendarDataProvider>()
	.AddSingleton<IWeatherForecastProvider, HomeAssistantWeatherForecastProvider>()
	.AddSingleton<IRssFeedDataProvider, HomeAssistantRssFeedDataProvider>()
	.AddSingleton<IEntityHistoryProvider, HomeAssistantEntityHistoryProvider>()
	.AddSingleton<IAiContentProvider, AiContentProvider>()
	.AddSingleton<ISsrDataProvider, SsrDataProvider>()
	.AddSingleton<FontAwesomeIconRegistry>()
	.AddSingleton<RenderingUtilities>(sp =>
		RenderingUtilities.Create(
			sp.GetRequiredService<IWebHostEnvironment>(),
			sp.GetRequiredService<FontAwesomeIconRegistry>()))
	.AddSingleton<DashboardImageRenderingService>()
	.AddSingleton<IWidgetRenderer, EPaperDashboard.Services.Rendering.Widgets.HeaderWidgetRenderer>()
	.AddSingleton<IWidgetRenderer, EPaperDashboard.Services.Rendering.Widgets.CalendarWidgetRenderer>()
	.AddSingleton<IWidgetRenderer, EPaperDashboard.Services.Rendering.Widgets.WeatherWidgetRenderer>()
	.AddSingleton<IWidgetRenderer, EPaperDashboard.Services.Rendering.Widgets.WeatherForecastWidgetRenderer>()
	.AddSingleton<IWidgetRenderer, EPaperDashboard.Services.Rendering.Widgets.TodoWidgetRenderer>()
	.AddSingleton<EPaperDashboard.Services.Rendering.Widgets.MarkdownWidgetRenderer>()
	.AddSingleton<IWidgetRenderer>(sp => sp.GetRequiredService<EPaperDashboard.Services.Rendering.Widgets.MarkdownWidgetRenderer>())
	.AddSingleton<IWidgetRenderer, EPaperDashboard.Services.Rendering.Widgets.AiContentWidgetRenderer>()
	.AddSingleton<IWidgetRenderer, EPaperDashboard.Services.Rendering.Widgets.RssFeedWidgetRenderer>()
	.AddSingleton<IWidgetRenderer, EPaperDashboard.Services.Rendering.Widgets.VersionWidgetRenderer>()
	.AddSingleton<IWidgetRenderer, EPaperDashboard.Services.Rendering.Widgets.AppIconWidgetRenderer>()
	.AddSingleton<IWidgetRenderer, EPaperDashboard.Services.Rendering.Widgets.ImageWidgetRenderer>()
	.AddSingleton<IWidgetRenderer, EPaperDashboard.Services.Rendering.Widgets.GraphWidgetRenderer>()
	.AddSingleton<IAiServiceFactory, AiServiceFactory>()
	.AddSingleton<AiPromptBuilder>()
	.AddSingleton<AiDataFetcher>()
	.AddSingleton<AiResponseParser>()
	.AddSingleton<WidgetValidator>()
	.AddSingleton<WidgetLayoutEngine>()
	.AddSingleton<GridPacker>()
	.AddSingleton<AiDashboardGenerationService>()
	.AddHostedService<DashboardScheduleMonitorService>()
	.AddHostedService<AiPreGenerationService>();

// Firmware update services
if (EnvironmentConfiguration.FirmwareUpdateEnabled)
{
	switch (EnvironmentConfiguration.FirmwareReleaseProvider.ToLowerInvariant())
	{
		case "github":
		default:
			builder.Services.AddSingleton<IFirmwareReleaseProvider, GitHubFirmwareReleaseProvider>();
			break;
	}
	builder.Services.AddSingleton<FirmwareUpdateService>();
	builder.Services.AddHostedService(sp => sp.GetRequiredService<FirmwareUpdateService>());
}

builder.Services.AddHttpClient(Constants.DashboardHttpClientName);
builder.Services.AddHttpClient(Constants.HassHttpClientName);
builder.Services.AddHttpClient(Constants.SsrImageHttpClientName, client =>
{
	client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddHttpClient(Constants.FirmwareHttpClientName, client =>
{
	client.DefaultRequestHeaders.Add("User-Agent", $"{Constants.AppName}/{Constants.AppVersion}");
	client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
	client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
	options.LoginPath = "/Login";
	options.LogoutPath = "/Logout";
	options.AccessDeniedPath = "/AccessDenied";
	options.Cookie.HttpOnly = true;
	options.Events.OnRedirectToLogin = ReturnForbiddenInsteadOfRedirect;
	options.Events.OnRedirectToAccessDenied = ReturnForbiddenInsteadOfRedirect;

	static Task ReturnForbiddenInsteadOfRedirect(Microsoft.AspNetCore.Authentication.RedirectContext<CookieAuthenticationOptions> context)
	{
		if (context.Request.Path.StartsWithSegments("/api"))
		{
			context.Response.StatusCode = 403;
		}
		else
		{
			context.Response.Redirect(context.RedirectUri);
		}
		return Task.CompletedTask;
	}
})
.AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>("ApiKey", null);

builder.Services.AddAuthorizationBuilder()
	.AddPolicy("SuperUserOnly", policy => policy.RequireClaim(Constants.IsSuperUserClaim, "true"))
	.AddPolicy("ApiKeyPolicy", policy =>
	{
		policy.RequireAssertion(context =>
		{
			var httpContext = context.Resource as HttpContext
				?? (context.Resource as Microsoft.AspNetCore.Mvc.Filters.AuthorizationFilterContext)?.HttpContext;

			if (httpContext is null)
			{
				return false;
			}

			if (!httpContext.Request.Headers.TryGetValue("X-Api-Key", out var apiKey) || string.IsNullOrWhiteSpace(apiKey))
			{
				return false;
			}

			var deviceService = httpContext.RequestServices.GetService(typeof(DeviceService)) as DeviceService;

			if (deviceService is null)
			{
				return false;
			}

			return deviceService.GetDeviceByApiKey(apiKey!).HasValue;
		});
	});

builder.Services.Configure<RazorPagesOptions>(options =>
{
	options.Conventions.AllowAnonymousToPage("/Login");
	options.Conventions.AllowAnonymousToPage("/Register");
	options.Conventions.AllowAnonymousToPage("/AccessDenied");
	options.Conventions.AllowAnonymousToPage("/Privacy");
});

var app = builder.Build();

var logger = app.Services.GetRequiredService<ILogger<Program>>();
var strategy = app.Services.GetRequiredService<IDeploymentStrategy>();

// Perform deployment-specific initial setup
using (var scope = app.Services.CreateScope())
{
	strategy.PerformInitialSetup(scope.ServiceProvider);
}

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
	ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost
});

app.UseCors(builder => builder
	.AllowAnyOrigin()
	.AllowAnyMethod()
	.AllowAnyHeader());

#if DEBUG
if (app.Environment.IsDevelopment())
{
	app.UseSwagger().UseSwaggerUI();
}
#endif

if (!app.Environment.IsDevelopment())
{
	app.UseHsts();
}

strategy.ApplyMiddleware(app, app.Environment);

app.UseRouting();

var devicePort = builder.Configuration.GetValue<int>("DevicePort", 8129);

app.Use(async (context, next) =>
{
	var isDevicePort = context.Connection.LocalPort == devicePort;
	
	if (isDevicePort)
	{
		var endpoint = context.GetEndpoint();
		var deviceAttr = endpoint?.Metadata.GetMetadata<EPaperDashboard.Guards.DeviceAccessibleAttribute>();

		if (deviceAttr is null)
		{
			context.Response.StatusCode = 404;
			return;
		}

		if (deviceAttr.RequireActivePairing)
		{
			var pairingService = context.RequestServices.GetRequiredService<PairingService>();
			pairingService.CleanupExpiredSessions();

			if (!pairingService.HasActiveSessions())
			{
				context.Response.StatusCode = 503;
				await context.Response.WriteAsync("Pairing service unavailable");
				return;
			}
		}

		// Add latest firmware version header to all device-port responses
		var firmwareService = context.RequestServices.GetService<FirmwareUpdateService>();
		var latestRelease = firmwareService?.GetLatestRelease();
		if (latestRelease?.DownloadUrl is not null)
		{
			context.Response.OnStarting(() =>
			{
				context.Response.Headers[HttpHeaderNames.FirmwareVersionHeaderName] = latestRelease.Version;
				return Task.CompletedTask;
			});
		}

		// Track device firmware version and update last seen timestamp
		if (context.Request.Headers.TryGetValue(HttpHeaderNames.DeviceFirmwareVersionHeaderName, out var deviceFwVersion)
			&& context.Request.Headers.TryGetValue(HttpHeaderNames.DeviceIdHeaderName, out var deviceIdHeader))
		{
			var deviceService = context.RequestServices.GetRequiredService<DeviceService>();
			var device = deviceService.GetDeviceByIdentifier(deviceIdHeader.ToString());
			if (device.HasValue)
			{
				var fwStr = deviceFwVersion.ToString();
				if (device.Value.FirmwareVersion != fwStr || device.Value.LastSeenAt is null
					|| DateTimeOffset.UtcNow - device.Value.LastSeenAt > TimeSpan.FromMinutes(1))
				{
					device.Value.FirmwareVersion = fwStr;
					device.Value.LastSeenAt = DateTimeOffset.UtcNow;
					deviceService.UpdateDevice(device.Value);
				}
			}
		}
	}

	await next();
});

app.UseAuthentication();
strategy.ApplyPostAuthenticationMiddleware(app, app.Environment);
app.UseAuthorization();
app.MapControllers();

strategy.ApplyPostStaticFilesMiddleware(app, app.Environment);

app.UseStaticFiles();

app.UseStaticFiles(new StaticFileOptions
{
	FileProvider = new PhysicalFileProvider(
		Path.Combine(builder.Environment.WebRootPath, "browser")),
	RequestPath = ""
});

app.UseSpa(spa =>
{
	spa.Options.DefaultPageStaticFileOptions = new StaticFileOptions
	{
		FileProvider = new PhysicalFileProvider(
			Path.Combine(builder.Environment.WebRootPath, "browser"))
	};

	if (app.Environment.IsDevelopment())
	{
		spa.UseProxyToSpaDevelopmentServer("http://localhost:4200");
	}
});

app.Run();
public class TimeOnlyJsonConverter : JsonConverter<TimeOnly>
{
	private const string Format = "HH:mm";

	public override TimeOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		var value = reader.GetString();
		if (string.IsNullOrEmpty(value))
		{
			return TimeOnly.MinValue;
		}
		if (TimeOnly.TryParseExact(value, Format, null, System.Globalization.DateTimeStyles.None, out var result))
		{
			return result;
		}
		return TimeOnly.Parse(value);
	}

	public override void Write(Utf8JsonWriter writer, TimeOnly value, JsonSerializerOptions options)
	{
		writer.WriteStringValue(value.ToString(Format));
	}
}