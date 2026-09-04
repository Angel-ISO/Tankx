using AspNetCoreRateLimit;
using backend.api.Services;
using backend.application.UnitOfWork;
using backend.domain.interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace backend.api.Extensions;

public static class ApplicationServiceExtensions
{
    public static void ConfigureCors(this IServiceCollection services) =>
        services.AddCors(options =>
        {
            options.AddPolicy("AngularClient", policy =>
                policy.AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader());
        });

    public static void ConfigureSupabaseAuth(this IServiceCollection services, string supabaseUrl)
    {
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = $"{supabaseUrl}/auth/v1";
                options.Audience = "authenticated";
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = $"{supabaseUrl}/auth/v1",
                    ValidateAudience = true,
                    ValidAudience = "authenticated",
                    ValidateLifetime = true,
                    NameClaimType = "sub"
                };
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(accessToken)
                            && context.HttpContext.Request.Path.StartsWithSegments("/hubs/game"))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    },
                    OnTokenValidated = async context =>
                    {
                        var subject = context.Principal?.FindFirst("sub")?.Value;
                        if (Guid.TryParse(subject, out var userId)
                            && !string.IsNullOrEmpty(context.SecurityToken?.ToString()))
                        {
                            var cache = context.HttpContext.RequestServices
                                .GetRequiredService<RedisCacheService>();
                            await cache.StoreSessionAsync(userId, context.SecurityToken.ToString());
                        }
                    }
                };
            });

        services.AddAuthorization();
    }

    public static void AddAplicacionServices(this IServiceCollection services)
    {
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddHttpContextAccessor();
        services.AddScoped<UserContextService>();
        services.AddScoped<QueryBenchmarkService>();
        services.AddScoped<IndexAnalysisService>();
        services.AddScoped<IndexImpactBenchmark>();
    }

    public static void ConfigurationRatelimiting(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
        services.AddInMemoryRateLimiting();
        services.Configure<IpRateLimitOptions>(options =>
        {
            options.EnableEndpointRateLimiting = true;
            options.StackBlockedRequests = false;
            options.HttpStatusCode = StatusCodes.Status429TooManyRequests;
            options.RealIpHeader = "X-Real-IP";
            options.GeneralRules =
            [
                new RateLimitRule
                {
                    Endpoint = "*",
                    Period = "10s",
                    Limit = 8000
                }
            ];
        });
    }
}
