using System.Text.Json;
using csharp_user_management.Contracts;
using csharp_user_management.Domain;
using csharp_user_management.Infrastructure;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "User Management API", Version = "v1" });
});

builder.Services.AddSingleton<IUserRepository, InMemoryUserRepository>();

var configuredTokens =
    builder.Configuration.GetSection("Authentication:Tokens").Get<string[]>()
    ?? Array.Empty<string>();
var singleToken = builder.Configuration["Authentication:Token"];
var tokenCandidates = string.IsNullOrWhiteSpace(singleToken)
    ? configuredTokens
    : configuredTokens.Append(singleToken).ToArray();
var validTokens = tokenCandidates
    .Select(token => token?.Trim())
    .Where(token => !string.IsNullOrWhiteSpace(token))
    .ToHashSet(StringComparer.Ordinal);

var app = builder.Build();

if (validTokens.Count == 0)
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

app.Use(
    async (context, next) =>
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

        if (!validTokens.Contains(token))
        {
            await WriteUnauthorizedAsync(context);
            return;
        }

        await next();
    }
);

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

app.MapGet("/", () => Results.Redirect("/swagger"));

var users = app.MapGroup("/api/users").WithTags("Users").WithOpenApi();

users
    .MapGet(
        "/",
        async (IUserRepository repository, CancellationToken cancellationToken) =>
        {
            var items = await repository.GetAllAsync(cancellationToken);
            var response = items.Select(user => user.ToResponse()).ToList();
            return Results.Ok(response);
        }
    )
    .WithName("GetUsers")
    .WithSummary("Get all users")
    .WithDescription("Returns all registered users.")
    .Produces<List<UserResponse>>(StatusCodes.Status200OK);

users
    .MapGet(
        "/{id:guid}",
        async (Guid id, IUserRepository repository, CancellationToken cancellationToken) =>
        {
            var user = await repository.GetByIdAsync(id, cancellationToken);
            return user is null
                ? Results.NotFound(CreateNotFoundProblem(id))
                : Results.Ok(user.ToResponse());
        }
    )
    .WithName("GetUserById")
    .WithSummary("Get a user by id")
    .WithDescription("Returns a single user when the identifier exists.")
    .Produces<UserResponse>(StatusCodes.Status200OK)
    .ProducesProblem(StatusCodes.Status404NotFound);

users
    .MapPost(
        "/",
        async (
            UserCreateRequest request,
            IUserRepository repository,
            CancellationToken cancellationToken
        ) =>
        {
            var validationErrors = request.Validate();
            if (validationErrors.Count > 0)
            {
                return Results.ValidationProblem(validationErrors);
            }

            var normalizedEmail = request.Email.Trim();
            if (await repository.EmailExistsAsync(normalizedEmail, null, cancellationToken))
            {
                return Results.Conflict(
                    CreateConflictProblem($"A user with email '{normalizedEmail}' already exists.")
                );
            }

            var now = DateTime.UtcNow;
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = normalizedEmail,
                FullName = request.FullName.Trim(),
                CreatedAt = now,
                UpdatedAt = null,
            };

            var created = await repository.CreateAsync(user, cancellationToken);
            var response = created.ToResponse();
            return Results.Created($"/api/users/{response.Id}", response);
        }
    )
    .WithName("CreateUser")
    .WithSummary("Create a new user")
    .WithDescription("Registers a new user when the request is valid.")
    .Produces<UserResponse>(StatusCodes.Status201Created)
    .ProducesValidationProblem()
    .ProducesProblem(StatusCodes.Status409Conflict);

users
    .MapPut(
        "/{id:guid}",
        async (
            Guid id,
            UserUpdateRequest request,
            IUserRepository repository,
            CancellationToken cancellationToken
        ) =>
        {
            var validationErrors = request.Validate();
            if (validationErrors.Count > 0)
            {
                return Results.ValidationProblem(validationErrors);
            }

            var normalizedEmail = request.Email.Trim();
            if (await repository.EmailExistsAsync(normalizedEmail, id, cancellationToken))
            {
                return Results.Conflict(
                    CreateConflictProblem(
                        $"A different user already uses email '{normalizedEmail}'."
                    )
                );
            }

            var updated = await repository.UpdateAsync(
                id,
                current =>
                    current with
                    {
                        Email = normalizedEmail,
                        FullName = request.FullName.Trim(),
                        UpdatedAt = DateTime.UtcNow,
                    },
                cancellationToken
            );

            return updated is null
                ? Results.NotFound(CreateNotFoundProblem(id))
                : Results.Ok(updated.ToResponse());
        }
    )
    .WithName("UpdateUser")
    .WithSummary("Update an existing user")
    .WithDescription("Updates a user when the identifier exists and the payload is valid.")
    .Produces<UserResponse>(StatusCodes.Status200OK)
    .ProducesValidationProblem()
    .ProducesProblem(StatusCodes.Status404NotFound)
    .ProducesProblem(StatusCodes.Status409Conflict);

users
    .MapDelete(
        "/{id:guid}",
        async (Guid id, IUserRepository repository, CancellationToken cancellationToken) =>
        {
            var deleted = await repository.DeleteAsync(id, cancellationToken);
            return deleted ? Results.NoContent() : Results.NotFound(CreateNotFoundProblem(id));
        }
    )
    .WithName("DeleteUser")
    .WithSummary("Delete a user")
    .WithDescription("Deletes the specified user when it exists.")
    .Produces(StatusCodes.Status204NoContent)
    .ProducesProblem(StatusCodes.Status404NotFound);

app.Run();

static Task WriteUnauthorizedAsync(HttpContext context)
{
    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
    context.Response.ContentType = "application/json";
    var payload = JsonSerializer.Serialize(new { error = "Unauthorized" });
    return context.Response.WriteAsync(payload);
}

static ProblemDetails CreateNotFoundProblem(Guid id) =>
    new()
    {
        Title = "User Not Found",
        Detail = $"User with id '{id}' was not found.",
        Status = StatusCodes.Status404NotFound,
    };

static ProblemDetails CreateConflictProblem(string detail) =>
    new()
    {
        Title = "User Conflict",
        Detail = detail,
        Status = StatusCodes.Status409Conflict,
    };

public partial class Program;
