using EPaperDashboard.Services.Rendering;
using EPaperDashboard.Utilities;

var builder = WebApplication.CreateBuilder(args);
ArgumentNullException.ThrowIfNull(EnvironmentConfiguration.RendererUri.Value);
ArgumentNullException.ThrowIfNull(EnvironmentConfiguration.DashboardUri.Value);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddControllers();
builder.Services.AddSwaggerGen(); 
builder.Services
	.AddTransient<IPageToImageRenderingService, PageToImageRenderingService>()
	.AddSingleton<IImageFactory, ImageFactory>()
	.AddHttpClient(Constants.RendererHttpClientName, client => client.BaseAddress = EnvironmentConfiguration.RendererUri.Value);

var app = builder.Build();
app.Logger.LogInformation("Renderer url:{0}", EnvironmentConfiguration.RendererUri.Value);
app.Logger.LogInformation("Dashboard url:{0}", EnvironmentConfiguration.DashboardUri.Value);

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

app.MapControllerRoute(name: "default", pattern: "{controller}/{action=Index}/{id?}");

app.Run();
