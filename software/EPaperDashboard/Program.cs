using EPaperDashboard.Services.Rendering;
using EPaperDashboard.Utilities;
using EPaperDashboard.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using EPaperDashboard.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddControllers();
builder.Services.AddRazorPages();
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
builder.Services
	.AddTransient<IPageToImageRenderingService, PageToImageRenderingService>()
	.AddSingleton<IImageFactory, ImageFactory>();

builder.Services.AddHttpClient(Constants.DashboardHttpClientName);
builder.Services.AddHttpClient(Constants.HassHttpClientName, client => client.BaseAddress = EnvironmentConfiguration.HassUri);

// Register LiteDbContext as singleton
builder.Services.AddSingleton<LiteDbContext>();
builder.Services.AddSingleton<UserService>();
builder.Services.AddSingleton<DashboardService>();

builder.Services.AddAuthentication(options =>
{
	options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
	options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
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
.AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>("ApiKey", null);

builder.Services.AddAuthorizationBuilder()
	.AddPolicy("SuperUserOnly", policy => policy.RequireClaim("IsSuperUser", "true"))
	.AddPolicy("ApiKeyPolicy", policy =>
	{
		policy.RequireAssertion(context =>
		{
			var httpContext = context.Resource as Microsoft.AspNetCore.Http.HttpContext
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

builder.Services.Configure<Microsoft.AspNetCore.Mvc.RazorPages.RazorPagesOptions>(options =>
{
    options.Conventions.AllowAnonymousToPage("/Login");
    options.Conventions.AllowAnonymousToPage("/Register");
});

var app = builder.Build();
app.Logger.LogInformation("Dashboard url:{0}", EnvironmentConfiguration.DashboardUri);

// Seed superuser if not exists using registered LiteDbContext and UserService
using (var scope = app.Services.CreateScope())
{
	var userService = scope.ServiceProvider.GetRequiredService<UserService>();
	if (!userService.HasSuperUser())
	{
		userService.TryCreateUser(EnvironmentConfiguration.SuperUserUsername, EnvironmentConfiguration.SuperUserPassword, isSuperUser: true);
	}
}

app.UseCors(builder => builder.WithOrigins("*"));

if (app.Environment.IsDevelopment())
{
	app.UseSwagger().UseSwaggerUI();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
	// The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
	app.UseHsts();
}

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapRazorPages().RequireAuthorization();
app.UseStaticFiles();

app.Run();
