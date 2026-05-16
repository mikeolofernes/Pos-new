using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pos.Application.Common.Abstractions;
using Pos.Application.Features.Inventory;
using Pos.Domain.Tenancy;
using Pos.Infrastructure.Identity;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Tenancy;

namespace Pos.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration cfg)
    {
        var conn = cfg.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres missing");

        services.AddDbContext<AppDbContext>(opt =>
            opt.UseNpgsql(conn, npg =>
                {
                    npg.MigrationsHistoryTable("__ef_migrations_history");
                    npg.EnableRetryOnFailure(3);
                })
               .UseSnakeCaseNamingConvention());

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<IInventoryPostingService, InventoryPostingService>();

        services.Configure<JwtOptions>(cfg.GetSection("Jwt"));
        services.AddSingleton<IJwtService, JwtService>();
        services.AddSingleton<IPasswordHasher, Argon2PasswordHasher>();

        return services;
    }
}
