using System;
using System.Text.Json;
using System.Threading.Tasks;
using csharp_user_management.Infrastructure.Authentication;
using Microsoft.AspNetCore.Http;

namespace csharp_user_management.Infrastructure.Middleware;

public sealed class TokenAuthenticationMiddleware
{
    private readonly RequestDelegate _next;

    public TokenAuthenticationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITokenValidator tokenValidator)
    {
        if (!context.Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            await WriteUnauthorizedAsync(context);
            return;
        }

        var token = authHeader.ToString();
        if (token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            token = token.Substring("Bearer ".Length).Trim();
        }
        else
        {
            token = token.Trim();
        }

        if (!tokenValidator.IsValid(token))
        {
            await WriteUnauthorizedAsync(context);
            return;
        }

        await _next(context);
    }

    private static Task WriteUnauthorizedAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";
        var payload = JsonSerializer.Serialize(new { error = "Unauthorized" });
        return context.Response.WriteAsync(payload);
    }
}
