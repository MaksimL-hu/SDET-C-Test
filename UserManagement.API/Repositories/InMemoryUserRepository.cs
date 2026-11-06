using System.Collections.Concurrent;
using UserManagement.API.Interfaces;
using UserManagement.API.Models;

namespace UserManagement.API.Repositories;

public sealed class InMemoryUserRepository : IUserRepository
{
    private readonly ConcurrentDictionary<Guid, User> _users = new();

    public User Add(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (!_users.TryAdd(user.UserId, user))
            throw new InvalidOperationException("A user with this identifier already exists.");

        return user;
    }

    public bool Update(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        while (_users.TryGetValue(user.UserId, out var current))
            if (_users.TryUpdate(user.UserId, user, current))
                return true;

        return false;
    }

    public User? GetById(Guid userId) => _users.GetValueOrDefault(userId);

    public IEnumerable<User> GetAll() => _users.ToArray().Select(entry => entry.Value);
}
