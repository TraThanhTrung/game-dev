using GameServer.Data;
using GameServer.Services;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Services
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Accept camelCase from client (e.g., playerName instead of PlayerName)
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

var connectionString = builder.Configuration.GetConnectionString("GameDb") ?? "Data Source=gameserver.db";
builder.Services.AddDbContext<GameDbContext>(options => options.UseSqlite(connectionString));

builder.Services.AddSingleton<GameConfigService>();
builder.Services.AddSingleton<WorldService>();
builder.Services.AddScoped<PlayerService>();
builder.Services.AddHostedService<GameLoopService>();

var app = builder.Build();

// Apply database migrations
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
    db.Database.Migrate();
    app.Logger.LogInformation("Database migrations applied: {db}", connectionString);
}

// Log startup info
var urls = app.Urls.Count > 0 ? string.Join(", ", app.Urls) : "http://localhost:5220";
app.Logger.LogInformation("===========================================");
app.Logger.LogInformation("  Game Server Starting...");
app.Logger.LogInformation("  Environment: {env}", app.Environment.EnvironmentName);
app.Logger.LogInformation("  URLs: {urls}", urls);
app.Logger.LogInformation("  Scalar UI: {url}/scalar/v1", urls.Split(',')[0].Trim());
app.Logger.LogInformation("===========================================");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// Detailed request logging middleware
app.Use(async (ctx, next) =>
{
    var sw = System.Diagnostics.Stopwatch.StartNew();
    var method = ctx.Request.Method;
    var path = ctx.Request.Path;
    var query = ctx.Request.QueryString.HasValue ? ctx.Request.QueryString.Value : "";

    // Log request start (only for non-polling requests to reduce spam)
    bool isPolling = path.ToString().Contains("/state");
    
    await next();
    
    sw.Stop();
    var playerId = ctx.Items.TryGetValue("playerId", out var pid) ? pid?.ToString() ?? "-" : "-";
    var status = ctx.Response.StatusCode;

    // Color-code by status
    var logLevel = status >= 400 ? LogLevel.Warning : LogLevel.Information;

    // Skip logging polling requests unless there's an error or it's slow
    if (isPolling && status < 400 && sw.ElapsedMilliseconds < 100)
    {
        return; // Don't log successful fast polling requests
    }

    app.Logger.Log(logLevel, 
        "[{method}] {path}{query} -> {status} ({ms}ms) player:{pid}",
        method, path, query, status, sw.ElapsedMilliseconds, playerId);
});

app.MapControllers();

// Log when server is ready
app.Lifetime.ApplicationStarted.Register(() =>
{
    app.Logger.LogInformation("Server is ready and accepting connections!");
    app.Logger.LogInformation("Press Ctrl+C to stop.");
});

app.Run();
