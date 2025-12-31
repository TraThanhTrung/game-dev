using GameServer.Data;
using GameServer.Services;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

var connectionString = builder.Configuration.GetConnectionString("GameDb") ?? "Data Source=gameserver.db";
builder.Services.AddDbContext<GameDbContext>(options => options.UseSqlite(connectionString));

builder.Services.AddSingleton<WorldService>();
builder.Services.AddHostedService<GameLoopService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// Simple request logging
app.Use(async (ctx, next) =>
{
    var sw = System.Diagnostics.Stopwatch.StartNew();
    await next();
    sw.Stop();
    app.Logger.LogInformation("{method} {path} -> {status} in {ms} ms",
        ctx.Request.Method,
        ctx.Request.Path,
        ctx.Response.StatusCode,
        sw.ElapsedMilliseconds);
});

app.MapControllers();

app.Run();
