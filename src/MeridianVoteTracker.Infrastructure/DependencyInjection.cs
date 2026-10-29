using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MeridianVoteTracker.Infrastructure.Imports;
using MeridianVoteTracker.Infrastructure.Persistence;
using MeridianVoteTracker.Infrastructure.Services;

namespace MeridianVoteTracker.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");

        services.AddDbContext<MeridianDbContext>(options =>
            options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention());

        services.Configure<ImportStorageOptions>(options =>
            options.UploadDirectory = configuration["Storage:UploadDirectory"] ?? options.UploadDirectory);
        services.AddSingleton<ImportQueue>();
        services.AddScoped<ImportRunner>();
        services.AddScoped<ConflictService>();
        services.AddScoped<PositionService>();

        return services;
    }
}
