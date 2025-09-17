using System.Collections.Concurrent;
using csharp_user_management.Domain;

namespace csharp_user_management.Infrastructure;

public interface IUserRepository
{
    Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken);
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<User> CreateAsync(User user, CancellationToken cancellationToken);
    Task<User?> UpdateAsync(Guid id, Func<User, User> updater, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> EmailExistsAsync(
        string email,
        Guid? excludeUserId,
        CancellationToken cancellationToken
    );
}

public sealed class InMemoryUserRepository : IUserRepository
{
    private readonly ConcurrentDictionary<Guid, User> _users = new();
    private static readonly StringComparer EmailComparer = StringComparer.OrdinalIgnoreCase;

    public Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken)
    {
        var snapshot = _users
            .Values.OrderBy(u => u.FullName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(u => u.Email, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Task.FromResult<IReadOnlyList<User>>(snapshot);
    }

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        _users.TryGetValue(id, out var user);
        return Task.FromResult(user);
    }

    public Task<User> CreateAsync(User user, CancellationToken cancellationToken)
    {
        if (user is null)
        {
            throw new ArgumentNullException(nameof(user));
        }

        if (!_users.TryAdd(user.Id, user))
        {
            throw new InvalidOperationException($"A user with id {user.Id} already exists.");
        }

        return Task.FromResult(user);
    }

    public Task<User?> UpdateAsync(
        Guid id,
        Func<User, User> updater,
        CancellationToken cancellationToken
    )
    {
        if (updater is null)
        {
            throw new ArgumentNullException(nameof(updater));
        }

        while (_users.TryGetValue(id, out var current))
        {
            var updated = updater(current);
            if (_users.TryUpdate(id, updated, current))
            {
                return Task.FromResult<User?>(updated);
            }
        }

        return Task.FromResult<User?>(null);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        return Task.FromResult(_users.TryRemove(id, out _));
    }

    public Task<bool> EmailExistsAsync(
        string email,
        Guid? excludeUserId,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return Task.FromResult(false);
        }

        var exists = _users.Values.Any(user =>
            EmailComparer.Equals(user.Email, email)
            && (!excludeUserId.HasValue || user.Id != excludeUserId.Value)
        );
        return Task.FromResult(exists);
    }
}
