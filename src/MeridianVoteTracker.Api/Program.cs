using System.Text.Json.Serialization;
using MeridianVoteTracker.Api.Auth;
using MeridianVoteTracker.Api.Imports;
using MeridianVoteTracker.Api.Middleware;
using MeridianVoteTracker.Api.Snapshots;
using MeridianVoteTracker.Infrastructure;
using MeridianVoteTracker.Infrastructure.Persistence;
using MeridianVoteTracker.Infrastructure.Seeding;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddJsonConsole(o => o.IncludeScopes = true);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        o.JsonSerializerOptions.NumberHandling = JsonNumberHandling.Strict;
    });
// The OpenAPI document is built from these options, so enums appear as readable names in the contract and in the generated client types.
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    // Numbers are numbers, not text: this keeps the contract (and the generated client types) simple.
    o.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
});
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks().AddDbContextCheck<MeridianDbContext>("database");

builder.Services.AddHostedService<ImportWorker>();
builder.Services.AddHostedService<SnapshotWorker>();

builder.Services.AddAuthentication(SimulatedAuthHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, SimulatedAuthHandler>(SimulatedAuthHandler.SchemeName, null);
builder.Services.AddAuthorization();

builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = ctx =>
        ctx.ProblemDetails.Extensions["correlationId"] = ctx.HttpContext.GetCorrelationId());

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.MapControllers();
app.MapHealthChecks("/health");

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MeridianDbContext>();
    await db.Database.MigrateAsync();
    await DatabaseSeeder.SeedAsync(db);
}

app.Run();

// Makes the entry point visible to the integration tests.
public partial class Program;
