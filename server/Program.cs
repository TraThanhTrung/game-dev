using GameServer.Data;
using GameServer.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using StackExchange.Redis;

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

// Razor Pages for Admin Area
builder.Services.AddRazorPages(options =>
{
    // IMPORTANT: AllowAnonymous must be set BEFORE AuthorizeAreaFolder
    // Allow anonymous access to Login page
    options.Conventions.AllowAnonymousToAreaPage("Admin", "/Login");
    // Then authorize the rest of the Admin area
    options.Conventions.AuthorizeAreaFolder("Admin", "/");
    
    // Allow anonymous access to Player Login page
    options.Conventions.AllowAnonymousToAreaPage("Player", "/Login");
});

// Session configuration for Player area authentication
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = ".Player.Session";
});

// Google OAuth Authentication
builder.Services.AddAuthentication()
    .AddGoogle(options =>
    {
        var googleAuth = builder.Configuration.GetSection("Authentication:Google");
        options.ClientId = googleAuth["ClientId"] ?? throw new InvalidOperationException("Google ClientId not configured");
        options.ClientSecret = googleAuth["ClientSecret"] ?? throw new InvalidOperationException("Google ClientSecret not configured");
        options.CallbackPath = "/signin-google";
        options.SignInScheme = "External";
    });

// Database Context - Combined Game and Identity
var connectionString = builder.Configuration.GetConnectionString("GameDb") ?? "Data Source=gameserver.db";
builder.Services.AddDbContext<GameDbContext>(options => options.UseSqlite(connectionString));

// ASP.NET Core Identity
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    // Password settings
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;

    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    // User settings
    options.User.RequireUniqueEmail = false;
})
.AddEntityFrameworkStores<GameDbContext>()
.AddDefaultTokenProviders();

// Identity cookie settings
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Admin/Login";
    options.LogoutPath = "/Admin/Logout";
    options.AccessDeniedPath = "/Admin/Login";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

// Redis
var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var configuration = ConfigurationOptions.Parse(redisConnectionString);
    configuration.AbortOnConnectFail = false;
    return ConnectionMultiplexer.Connect(configuration);
});

// Application Services
// Note: GameConfigService needs to be registered after DbContext and RedisService
builder.Services.AddSingleton<GameConfigService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<GameConfigService>>();
    return new GameConfigService(logger, sp);
});
builder.Services.AddSingleton<WorldService>();
builder.Services.AddScoped<PlayerService>();
builder.Services.AddScoped<RedisService>();
builder.Services.AddScoped<EnemyConfigService>();
builder.Services.AddScoped<AdminService>();
builder.Services.AddScoped<SessionTrackingService>();
builder.Services.AddScoped<PlayerWebService>(sp =>
{
    var db = sp.GetRequiredService<GameDbContext>();
    var logger = sp.GetRequiredService<ILogger<PlayerWebService>>();
    var configService = sp.GetService<GameConfigService>();
    return new PlayerWebService(db, logger, configService);
});
builder.Services.AddHostedService<GameLoopService>();

var app = builder.Build();

// Apply database migrations
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
    db.Database.Migrate();
    app.Logger.LogInformation("Database migrations applied: {db}", connectionString);

    // Seed default admin user
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

    // Create Admin role if it doesn't exist
    if (!await roleManager.RoleExistsAsync("Admin"))
    {
        await roleManager.CreateAsync(new IdentityRole("Admin"));
    }

    // Create default admin user if it doesn't exist
    var adminUser = await userManager.FindByNameAsync("admin");
    if (adminUser == null)
    {
        adminUser = new IdentityUser
        {
            UserName = "admin",
            Email = "admin@game.local"
        };
        var result = await userManager.CreateAsync(adminUser, "Admin123!");
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(adminUser, "Admin");
            app.Logger.LogInformation("Default admin user created: admin / Admin123!");
        }
    }

    // Migrate enemies from config.json to database
    try
    {
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "shared", "game-config.json");
        if (!File.Exists(configPath))
        {
            configPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "shared", "game-config.json");
        }
        
        if (File.Exists(configPath))
        {
            await GameServer.Scripts.MigrateEnemiesFromConfig.RunAsync(db, app.Logger, configPath);
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Failed to migrate enemies from config.json");
    }
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

// Static files for admin template
app.UseStaticFiles();

// Session middleware (must be before UseAuthentication)
app.UseSession();

// Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// Map Razor Pages
app.MapRazorPages();

app.MapControllers();

// Log when server is ready
app.Lifetime.ApplicationStarted.Register(() =>
{
    app.Logger.LogInformation("Server is ready and accepting connections!");
    app.Logger.LogInformation("Press Ctrl+C to stop.");
});

app.Run();
