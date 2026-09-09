using Microsoft.AspNetCore.SignalR;
using Server.Hubs;
using Server.Middleware;
using Server.Services;

var builder = WebApplication.CreateBuilder(args);

const string WebClientCorsPolicy = "WebClient";

var rateLimitFilter = new RateLimitMiddleware();
builder.Services.AddSingleton(rateLimitFilter);
builder.Services.AddSignalR(options => options.AddFilter(rateLimitFilter));
builder.Services.AddControllers();
builder.Services.AddSingleton<Scheduler>();
builder.Services.AddSingleton<EffectExecutor>();
builder.Services.AddSingleton<RoomRegistry>();
builder.Services.AddCors(options =>
{
    options.AddPolicy(WebClientCorsPolicy, policy => policy
        .WithOrigins("http://localhost:5173")
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

var app = builder.Build();

app.UseCors(WebClientCorsPolicy);
app.MapControllers();
app.MapHub<GameHub>("/hubs/game");

app.Run();
