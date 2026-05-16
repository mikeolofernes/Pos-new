using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Pos.Api.Common;
using Pos.Api.Endpoints;
using Pos.Api.Hubs;
using Pos.Api.Middleware;
using Pos.Application;
using Pos.Application.Common.Abstractions;
using Pos.Infrastructure;
using Pos.Infrastructure.Persistence;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, _, lc) => lc.ReadFrom.Configuration(ctx.Configuration));

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// ---- Authentication / Authorization
var jwt = builder.Configuration.GetSection("Jwt");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt["Issuer"],
            ValidAudience = jwt["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwt["SigningKey"]
                    ?? throw new InvalidOperationException("Jwt:SigningKey missing"))),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
        opt.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var accessToken = ctx.Request.Query["access_token"];
                var path = ctx.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    ctx.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddSingleton<IAuthorizationHandler, PermissionHandler>();

// ---- CORS
var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

// ---- SignalR
builder.Services.AddSignalR();

// ---- Rate limiting (per-tenant + per-IP)
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.AddPolicy("default", ctx =>
    {
        var key = ctx.User.FindFirst("tid")?.Value
                  ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "anon";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ =>
            new FixedWindowRateLimiterOptions
            {
                PermitLimit = 300,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });
});

// ---- OpenAPI / Health
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("Postgres")!, name: "postgres");

var app = builder.Build();

// ---- Pipeline
app.UseSerilogRequestLogging();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseCors();
app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();
app.UseRateLimiter();

// ---- Migrations + seed ----
// In production we apply migrations; in dev, allow EnsureCreated when no migrations exist
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var hasMigrations = db.Database.GetMigrations().Any();
    if (hasMigrations)
        await db.Database.MigrateAsync();
    else if (app.Environment.IsDevelopment())
        await db.Database.EnsureCreatedAsync();

    // Seed runs unconditionally — the seeder is idempotent (returns early if
    // the demo tenant already exists), so it's safe in any environment for now.
    // Remove this once you have proper bootstrap/onboarding.
    try
    {
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        await DbSeeder.SeedAsync(db, hasher);
        logger.LogInformation("Seed check complete (env={Env})", app.Environment.EnvironmentName);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Seeding failed");
        throw;
    }
}

// ---- OpenAPI UI ----
app.MapOpenApi();
app.MapScalarApiReference();

// ---- Health
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");

// ---- API
app.MapAuthEndpoints();
app.MapShopEndpoints();
app.MapProductEndpoints();
app.MapInventoryEndpoints();
app.MapPosEndpoints();
app.MapSyncEndpoints();

// ---- Hubs
app.MapHub<PosHub>("/hubs/pos");

app.MapGet("/", () => Results.Redirect("/scalar/v1"));

app.Run();

public partial class Program;
