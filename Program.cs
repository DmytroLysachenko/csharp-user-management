using System.Text.Json;
using csharp_user_management.Infrastructure;
using csharp_user_management.Infrastructure.Authentication;
using csharp_user_management.Infrastructure.Middleware;
using Microsoft.AspNetCore.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "User Management API", Version = "v1" });
});

builder.Services.AddSingleton<IUserRepository, InMemoryUserRepository>();
builder.Services.AddSingleton<ITokenValidator>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    return TokenValidator.FromConfiguration(configuration);
});

var app = builder.Build();

var tokenValidator = app.Services.GetRequiredService<ITokenValidator>();
if (!tokenValidator.HasConfiguredTokens)
{
    app.Logger.LogWarning(
        "No authentication tokens are configured; all requests will be rejected."
    );
}

app.UseExceptionHandler(appBuilder =>
{
    appBuilder.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";

        var feature = context.Features.Get<IExceptionHandlerFeature>();
        if (feature?.Error is { } exception)
        {
            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogError(exception, "Unhandled exception while processing request");
        }

        var payload = JsonSerializer.Serialize(new { error = "Internal server error." });
        await context.Response.WriteAsync(payload);
    });
});

app.UseStatusCodePages();
app.UseHttpsRedirection();

app.UseSwagger();
app.UseSwaggerUI();

app.UseMiddleware<TokenAuthenticationMiddleware>();

app.Use(
    async (context, next) =>
    {
        var method = context.Request.Method;
        var path = context.Request.Path;
        await next();
        var statusCode = context.Response.StatusCode;
        app.Logger.LogInformation(
            "Handled {Method} {Path} with status code {StatusCode}",
            method,
            path,
            statusCode
        );
    }
);

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

app.MapControllers();

app.Run();

public partial class Program;
