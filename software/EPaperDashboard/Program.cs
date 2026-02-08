using EPaperDashboard.Services.Rendering;
using EPaperDashboard.Utilities;
using EPaperDashboard.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using EPaperDashboard.Services;
using EPaperDashboard.Authentication;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Claims;
using LiteDB;

var configValidation = EnvironmentConfiguration.ValidateConfiguration();
if (configValidation.IsFailure)
{
	Console.Error.WriteLine($"Configuration Error: {configValidation.Error}");
	Environment.Exit(1);
}

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddControllers()
	.AddJsonOptions(options =>
	{
		options.JsonSerializerOptions.Converters.Add(new ObjectIdJsonConverter());
		options.JsonSerializerOptions.Converters.Add(new TimeOnlyJsonConverter());
	});
builder.Services.AddSpaStaticFiles(configuration =>
{
	configuration.RootPath = "frontend/dist/frontend/browser";
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
	.AddTransient<IPageToImageRenderingService, PageToImageRenderingService>()
	.AddSingleton<IImageFactory, ImageFactory>()
	.AddSingleton<LiteDbContext>()
	.AddSingleton<UserService>()
	.AddSingleton<DashboardService>()
	.AddSingleton<HomeAssistantAuthService>()
	.AddSingleton<HomeAssistantService>()
	.AddSingleton<HomeAssistantEnvironmentService>()
	.AddSingleton<DashboardHtmlRenderingService>()
	.AddHostedService<DashboardScheduleMonitorService>();

builder.Services.AddHttpClient(Constants.DashboardHttpClientName);
builder.Services.AddHttpClient(Constants.HassHttpClientName);

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
	.AddPolicy("SuperUserOnly", policy => policy.RequireClaim("IsSuperUser", "true"))
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

			if (httpContext.RequestServices.GetService(typeof(DashboardService)) is not DashboardService dashboardService)
			{
				return false;
			}

			return dashboardService.GetDashboardByApiKey(apiKey!).HasValue;
		});
	});

builder.Services.Configure<RazorPagesOptions>(options =>
{
	options.Conventions.AllowAnonymousToPage("/Login");
	options.Conventions.AllowAnonymousToPage("/Register");
	options.Conventions.AllowAnonymousToPage("/AccessDenied");
	options.Conventions.AllowAnonymousToPage("/Privacy");
});

// Configure SPA static files
builder.Services.AddSpaStaticFiles(configuration =>
{
	configuration.RootPath = "frontend/dist/frontend/browser";
});

var app = builder.Build();

var logger = app.Services.GetRequiredService<ILogger<Program>>();
var haEnvironment = app.Services.GetRequiredService<HomeAssistantEnvironmentService>();

logger.LogInformation("Configuration directory: {ConfigDir}", EnvironmentConfiguration.ConfigDir);
logger.LogInformation("Client URL: {ClientUrl}", EnvironmentConfiguration.ClientUri);
logger.LogInformation("Running in Home Assistant add-on mode: {IsHAMode}", haEnvironment.IsValidHomeAssistantEnvironment());

using (var scope = app.Services.CreateScope())
{
	if (!haEnvironment.IsValidHomeAssistantEnvironment())
	{
		var userService = scope.ServiceProvider.GetRequiredService<UserService>();
		if (!userService.HasSuperUser())
		{
			userService.TryCreateUser(EnvironmentConfiguration.SuperUserUsername, EnvironmentConfiguration.SuperUserPassword, isSuperUser: true);
		}
	}
}

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
	ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost
});

app.UseCors(builder => builder.WithOrigins("*"));

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

app.UseRouting();

app.Use(async (context, next) =>
{
	var haEnv = context.RequestServices.GetRequiredService<HomeAssistantEnvironmentService>();
	if (haEnv.IsValidHomeAssistantEnvironment() && context.Request.Headers.ContainsKey("X-Ingress-Path"))
	{
		var claims = new List<Claim>
		{
			new Claim(ClaimTypes.NameIdentifier, "ha-admin"),
			new Claim(ClaimTypes.Name, "Home Assistant Admin"),
			new Claim("IsSuperUser", "true"),
			new Claim("HomeAssistantIngress", "true")
		};
		var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
		context.User = new ClaimsPrincipal(identity);
		
		var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
		if (path.StartsWith("/api/auth/") || path.StartsWith("/api/users/"))
		{
			if (path != "/api/auth/current")
			{
				context.Response.StatusCode = 403;
				await context.Response.WriteAsJsonAsync(new { message = "User management is disabled in Home Assistant add-on mode" });
				return;
			}
		}
	}
	await next();
});

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.UseSpaStaticFiles();
app.UseSpa(spa =>
{
	spa.Options.SourcePath = "frontend";

	if (app.Environment.IsDevelopment())
	{
		spa.UseProxyToSpaDevelopmentServer("http://localhost:4200");
	}
});

app.Run();

public class ObjectIdJsonConverter : JsonConverter<ObjectId>
{
	public override ObjectId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		var value = reader.GetString();
		if (string.IsNullOrEmpty(value))
		{
			return ObjectId.Empty;
		}
		return new ObjectId(value);
	}

	public override void Write(Utf8JsonWriter writer, ObjectId value, JsonSerializerOptions options)
	{
		writer.WriteStringValue(value.ToString());
	}
}

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