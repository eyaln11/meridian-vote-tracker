using MeridianVoteTracker.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddJsonConsole(o => o.IncludeScopes = true);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = ctx =>
        ctx.ProblemDetails.Extensions["correlationId"] = ctx.HttpContext.GetCorrelationId());

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapOpenApi();
app.MapControllers();
app.MapHealthChecks("/health");



app.Run();