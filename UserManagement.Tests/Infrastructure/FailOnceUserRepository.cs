using UserManagement.API.Interfaces;
using UserManagement.API.Models;
using UserManagement.API.Repositories;

namespace UserManagement.Tests.Infrastructure;

public sealed class FailOnceUserRepository : IUserRepository
{
    public const string FailureMessage = "Смоделирована внутренняя ошибка репозитория.";

    private readonly InMemoryUserRepository _inner = new();
    private int _hasFailed;

    public User Add(User user) => _inner.Add(user);

    public bool Update(User user) => _inner.Update(user);

    public User? GetById(Guid userId) => _inner.GetById(userId);

    public IEnumerable<User> GetAll()
    {
        if (Interlocked.Exchange(ref _hasFailed, 1) == 0)
        {
            throw new InvalidOperationException(FailureMessage);
        }

        return _inner.GetAll();
    }
}
