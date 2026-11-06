using UserManagement.API.Models;
using UserManagement.API.Repositories;
using UserManagement.Tests.Infrastructure;

namespace UserManagement.Tests.Tests;

public sealed class InMemoryUserRepositoryTests
{
    private readonly InMemoryUserRepository _repository = new();

    [Fact]
    public void Add_ReturnsUser_AndMakesItAvailableForReads()
    {
        var user = NewUser();
        Assert.Same(user, _repository.Add(user));
        Assert.Same(user, _repository.GetById(user.UserId));
        Assert.Same(user, Assert.Single(_repository.GetAll()));
    }

    [Fact]
    public void DuplicateAdd_DoesNotOverwriteExistingUser()
    {
        var original = _repository.Add(NewUser());
        var duplicate = NewUser(original.UserId, "US", "en-US");
        Assert.Throws<InvalidOperationException>(() => _repository.Add(duplicate));
        Assert.Same(original, _repository.GetById(original.UserId));
        Assert.Single(_repository.GetAll());
    }

    [Fact]
    public void Update_ExistingUser_ReplacesIt()
    {
        var original = _repository.Add(NewUser());
        var updated = NewUser(original.UserId, "US", "en-US");
        Assert.True(_repository.Update(updated));
        Assert.Same(updated, _repository.GetById(original.UserId));
        Assert.Same(updated, Assert.Single(_repository.GetAll()));
    }

    [Fact]
    public void MissingUser_ReturnsNull_AndUpdateDoesNotInsert()
    {
        var missing = NewUser();
        Assert.Null(_repository.GetById(missing.UserId));
        Assert.False(_repository.Update(missing));
        Assert.Empty(_repository.GetAll());
    }

    [Fact]
    public void GetAll_ReturnsSnapshotUnaffectedBySubsequentWrites()
    {
        var original = _repository.Add(NewUser());
        var snapshot = _repository.GetAll();
        _repository.Add(NewUser());
        _repository.Update(NewUser(original.UserId, "US", "en-US"));
        Assert.Same(original, Assert.Single(snapshot));
        Assert.Equal(2, _repository.GetAll().Count());
    }

    [Fact]
    public void NullArguments_AreRejectedWithoutChangingState()
    {
        Assert.Throws<ArgumentNullException>(() => _repository.Add(null!));
        Assert.Throws<ArgumentNullException>(() => _repository.Update(null!));
        Assert.Empty(_repository.GetAll());
    }

    private static User NewUser(Guid? id = null, string country = "RU", string locale = "ru-RU") => new()
    {
        UserId = id ?? Guid.NewGuid(),
        CountryIsoCode = country,
        Locale = locale,
        DateCreated = TestDates.InitialUtc
    };
}
