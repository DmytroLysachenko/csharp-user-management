using System.Net;
using System.Net.Http.Json;
using csharp_user_management.Contracts;

namespace csharp_user_management.Tests;

public class UserApiTests
{
    [Fact]
    public async Task GetUsers_ReturnsEmptyList_WhenRepositoryIsEmpty()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/users");

        response.EnsureSuccessStatusCode();
        var users = await response.Content.ReadFromJsonAsync<List<UserResponse>>();

        Assert.NotNull(users);
        Assert.Empty(users!);
    }

    [Fact]
    public async Task CreateUser_ReturnsCreatedUser()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        var request = new UserCreateRequest
        {
            Email = "jane.doe@example.com",
            FullName = "Jane Doe",
        };

        var response = await client.PostAsJsonAsync("/api/users", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var created = await response.Content.ReadFromJsonAsync<UserResponse>();

        Assert.NotNull(created);
        Assert.Equal(request.Email, created!.Email);
        Assert.Equal(request.FullName, created.FullName);
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.NotEqual(default, created.CreatedAt);
        Assert.Null(created.UpdatedAt);
    }

    [Fact]
    public async Task GetUserById_ReturnsNotFound_WhenUserDoesNotExist()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/users/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateUser_ReturnsConflict_WhenEmailAlreadyInUse()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        var firstUser = await CreateUserAsync(client, "jane.doe@example.com", "Jane Doe");
        var secondUser = await CreateUserAsync(client, "john.smith@example.com", "John Smith");

        var update = new UserUpdateRequest
        {
            Email = firstUser.Email,
            FullName = "Johnathan Smith",
        };

        var response = await client.PutAsJsonAsync($"/api/users/{secondUser.Id}", update);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task DeleteUser_RemovesUser()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        var created = await CreateUserAsync(client, "samantha.lee@example.com", "Samantha Lee");

        var deleteResponse = await client.DeleteAsync($"/api/users/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getResponse = await client.GetAsync($"/api/users/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    private static async Task<UserResponse> CreateUserAsync(
        HttpClient client,
        string email,
        string fullName
    )
    {
        var request = new UserCreateRequest { Email = email, FullName = fullName };

        var response = await client.PostAsJsonAsync("/api/users", request);
        response.EnsureSuccessStatusCode();

        var created = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.NotNull(created);
        return created!;
    }
}
