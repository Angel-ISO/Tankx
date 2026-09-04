using Microsoft.OpenApi;

namespace backend.api.Extensions;

public static class SwaggerExtensions
{
    public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();

        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "TankX API",
                Version = "v1",
                Description = """
                    REST API for TankX, a real-time multiplayer tank battle game.

                    This API provides endpoints for player profiles, matches,
                    match participants, statistics, and authentication.

                    Authentication is handled by Supabase Auth using JWT tokens.
                    """,
                Contact = new OpenApiContact
                {
                    Name = "TankX Development Team"
                }
            });

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description =
                    "Enter your Supabase Auth JWT token. " +
                    "Example: Bearer eyJhbGciOi..."
            });

            options.AddSecurityRequirement(document =>
                new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("Bearer", document)] = []
                });
        });

        return services;
    }

    public static WebApplication UseSwaggerDocumentation(this WebApplication app)
    {
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "TankX API v1");
                options.DocumentTitle = "TankX API Documentation";
            });

        return app;
    }
}