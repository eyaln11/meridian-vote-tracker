using MeridianVoteTracker.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddJsonConsole(o => o.IncludeScopes = true);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();

app.MapOpenApi();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();