using EPaperDashboard.Services.Rendering;
using EPaperDashboard.Services.Weather;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSwaggerGen(config =>
{
    config.AddServer(new OpenApiServer
    {
        Description = "Development Server",
        Url = "https://localhost:7297"
    });
});
builder.Services.AddHttpClient(HttpClientConfigurations.WeatherService);
builder.Services
    .AddScoped<IWeatherService, WeatherService>()
    .AddScoped<ILocationService, LocationService>()
    .AddTransient<IPageToImageRenderingService, PageToImageRenderingService>()
    .AddSingleton<IWebDriver, WebDriver>()
    .AddSingleton<IImageFactory, ImageFactory>();
builder.Services.AddRazorPages();

var app = builder.Build();
app.UseCors(builder => builder.WithOrigins("*"));
app.UseSwagger().UseSwaggerUI();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller}/{action=Index}/{id?}");

app.MapFallbackToFile("index.html");
app.MapRazorPages();

app.Run();