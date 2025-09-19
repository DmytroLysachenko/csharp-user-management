using csharp_user_management.Contracts;
using csharp_user_management.Domain;
using csharp_user_management.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace csharp_user_management.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Tags("Users")]
public sealed class UsersController : ControllerBase
{
    private readonly IUserRepository _repository;

    public UsersController(IUserRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<UserResponse>), StatusCodes.Status200OK)]
    public async Task<Ok<List<UserResponse>>> GetUsers(CancellationToken cancellationToken)
    {
        var items = await _repository.GetAllAsync(cancellationToken);
        var response = items.Select(user => user.ToResponse()).ToList();
        return TypedResults.Ok(response);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<Results<Ok<UserResponse>, NotFound<ProblemDetails>>> GetUserById(
        Guid id,
        CancellationToken cancellationToken
    )
    {
        var user = await _repository.GetByIdAsync(id, cancellationToken);
        return user is null
            ? TypedResults.NotFound(CreateNotFoundProblem(id))
            : TypedResults.Ok(user.ToResponse());
    }

    [HttpPost]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<
        Results<Created<UserResponse>, ValidationProblem, Conflict<ProblemDetails>>
    > CreateUser(UserCreateRequest request, CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return TypedResults.ValidationProblem(validationErrors);
        }

        var normalizedEmail = request.Email.Trim();
        if (await _repository.EmailExistsAsync(normalizedEmail, null, cancellationToken))
        {
            return TypedResults.Conflict(
                CreateConflictProblem($"A user with email '{normalizedEmail}' already exists.")
            );
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            FullName = request.FullName.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = null,
        };

        var created = await _repository.CreateAsync(user, cancellationToken);
        var response = created.ToResponse();
        return TypedResults.Created($"/api/users/{response.Id}", response);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<
        Results<
            Ok<UserResponse>,
            ValidationProblem,
            NotFound<ProblemDetails>,
            Conflict<ProblemDetails>
        >
    > UpdateUser(Guid id, UserUpdateRequest request, CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return TypedResults.ValidationProblem(validationErrors);
        }

        var normalizedEmail = request.Email.Trim();
        if (await _repository.EmailExistsAsync(normalizedEmail, id, cancellationToken))
        {
            return TypedResults.Conflict(
                CreateConflictProblem($"A different user already uses email '{normalizedEmail}'.")
            );
        }

        var updated = await _repository.UpdateAsync(
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
            ? TypedResults.NotFound(CreateNotFoundProblem(id))
            : TypedResults.Ok(updated.ToResponse());
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<Results<NoContent, NotFound<ProblemDetails>>> DeleteUser(
        Guid id,
        CancellationToken cancellationToken
    )
    {
        var deleted = await _repository.DeleteAsync(id, cancellationToken);
        return deleted
            ? TypedResults.NoContent()
            : TypedResults.NotFound(CreateNotFoundProblem(id));
    }

    private static ProblemDetails CreateNotFoundProblem(Guid id) =>
        new()
        {
            Title = "User Not Found",
            Detail = $"User with id '{id}' was not found.",
            Status = StatusCodes.Status404NotFound,
        };

    private static ProblemDetails CreateConflictProblem(string detail) =>
        new()
        {
            Title = "User Conflict",
            Detail = detail,
            Status = StatusCodes.Status409Conflict,
        };
}
