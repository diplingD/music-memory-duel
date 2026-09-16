using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR;
using Server.Hubs;
using Server.Middleware;
using Server.Services;

var builder = WebApplication.CreateBuilder(args);

var rateLimitFilter = new RateLimitMiddleware();
builder.Services.AddSingleton(rateLimitFilter);
builder.Services.AddSignalR(options => options.AddFilter(rateLimitFilter))
    .AddJsonProtocol(options => options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddControllers();
builder.Services.AddSingleton<Scheduler>();
builder.Services.AddSingleton<EffectExecutor>();
builder.Services.AddSingleton<RoomRegistry>();

var app = builder.Build();

// Serves the React build copied into wwwroot by the Docker image.
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();
app.MapHub<GameHub>("/hubs/game");

app.Run();
