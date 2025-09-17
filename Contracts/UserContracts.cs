using System.ComponentModel.DataAnnotations;
using csharp_user_management.Domain;

namespace csharp_user_management.Contracts;

public sealed record class UserResponse(
    Guid Id,
    string Email,
    string FullName,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public sealed record class UserCreateRequest
{
    public string Email { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;

    public Dictionary<string, string[]> Validate()
    {
        return UserContractValidation.ValidateCommon(Email, FullName);
    }
}

public sealed record class UserUpdateRequest
{
    public string Email { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;

    public Dictionary<string, string[]> Validate()
    {
        return UserContractValidation.ValidateCommon(Email, FullName);
    }
}

internal static class UserContractValidation
{
    private static readonly EmailAddressAttribute EmailValidator = new();

    internal static Dictionary<string, string[]> ValidateCommon(string email, string fullName)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(email))
        {
            errors["Email"] = ["Email is required."];
        }
        else if (!EmailValidator.IsValid(email))
        {
            errors["Email"] = ["Email must be a valid email address."];
        }

        if (string.IsNullOrWhiteSpace(fullName))
        {
            errors["FullName"] = ["Full name is required."];
        }
        else if (fullName.Trim().Length < 2)
        {
            errors["FullName"] = ["Full name must be at least 2 characters long."];
        }

        return errors;
    }

    public static UserResponse ToResponse(this User user) =>
        new(user.Id, user.Email, user.FullName, user.CreatedAt, user.UpdatedAt);
}
