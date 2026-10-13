var builder = WebApplication.CreateBuilder(args);

// Load GEMINI_API_KEY from configuration (user-secrets or environment) into GeminiHelper.
// Ensure this runs before any code that calls GeminiHelper.
PokemonGoEvents.Helpers.GeminiHelper.Configure(builder.Configuration);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddHttpClient();
// Enable serving a static single-page UI from wwwroot
builder.Services.AddDirectoryBrowser();
// Allow browser-based UI to call the API from local dev origins
builder.Services.AddCors(options =>
{
    options.AddPolicy("LocalDev", builder =>
    {
        builder.WithOrigins("http://localhost:5230", "https://localhost:7216")
               .AllowAnyHeader()
               .AllowAnyMethod()
               .AllowCredentials();
    });
});
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
// Serve the modern scalar UI from wwwroot (static files)
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseDirectoryBrowser();

// Allow the unload permission for the served UI during development to avoid
// browser console "Permissions policy violation: unload is not allowed" warnings.
// This is safe for local development; review before enabling in production.
app.Use(async (context, next) =>
{
    // Modern header name
    context.Response.Headers["Permissions-Policy"] = "unload=(self)";
    // Some browsers still recognize the older header name
    context.Response.Headers["Feature-Policy"] = "unload=(self)";
    await next();
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();

// Apply CORS policy for browser clients
app.UseCors("LocalDev");

app.UseAuthorization();

app.MapControllers();

app.Run();
