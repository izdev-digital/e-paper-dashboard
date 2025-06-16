using EPaperDashboard.Services.Rendering;
using EPaperDashboard.Utilities;
using EPaperDashboard.Data;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddControllers();
builder.Services.AddRazorPages();
builder.Services.AddSwaggerGen();
builder.Services
	.AddTransient<IPageToImageRenderingService, PageToImageRenderingService>()
	.AddSingleton<IImageFactory, ImageFactory>();

builder.Services.AddHttpClient(Constants.DashboardHttpClientName);
builder.Services.AddHttpClient(Constants.HassHttpClientName, client => client.BaseAddress = EnvironmentConfiguration.HassUri);

// Register LiteDbContext as singleton
var dbPath = Path.Combine(AppContext.BaseDirectory, "epaperdashboard.db");
builder.Services.AddSingleton(new LiteDbContext(dbPath));
builder.Services.AddSingleton<UserService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login";
        options.LogoutPath = "/Logout";
        options.AccessDeniedPath = "/AccessDenied";
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("SuperUserOnly", policy => policy.RequireClaim("IsSuperUser", "true"));

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
app.UseStaticFiles();
app.MapRazorPages();

app.Run();
