using System.Net;
using System.Net.Http.Json;
using UserManagement.API.DTOs;
using UserManagement.API.Models;
using UserManagement.Tests.Infrastructure;

namespace UserManagement.Tests.Tests;

public sealed class UsersConcurrencyTests : ApiTestBase
{
    private static readonly UserDto[] Updates =
    [
        ValidDto("us", "en-US"),
        ValidDto("de", "de-DE"),
        ValidDto("gb", "en-GB"),
        ValidDto("fr", "fr-FR")
    ];

    [Fact]
    public async Task ConcurrentCreates_PreserveAllUsers_AndGenerateUniqueIdentifiers()
    {
        const int count = 64;
        var created = await RunConcurrentlyAsync(count, async index =>
        {
            var dto = Updates[index % Updates.Length];
            return await CreateUserAsync(dto.CountryIsoCode, dto.Locale);
        });

        Assert.Equal(count, created.Select(user => user.UserId).Distinct().Count());
        var stored = await GetAllAsync();
        Assert.Equal(count, stored.Length);
        foreach (var expected in created)
        {
            Assert.Null(expected.DateModified);
            Assert.Equal(Factory.Clock.GetUtcNow().UtcDateTime, expected.DateCreated);
            UserAssertions.Equal(expected, Assert.Single(stored, user => user.UserId == expected.UserId));
        }
    }

    [Fact]
    public async Task ConcurrentUpdatesAndReads_NeverExposePartialUserState()
    {
        var original = await CreateUserAsync("US", "en-US");
        Factory.Clock.Advance(TimeSpan.FromMinutes(1));
        await RunConcurrentlyAsync(80, async index =>
        {
            if (index % 2 == 0)
            {
                var dto = Updates[index / 2 % Updates.Length];
                using var response = await Client.PutAsJsonAsync($"/api/users/{original.UserId}", dto);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var updated = await UserAssertions.ReadUserAsync(response);
                Assert.Equal(dto.CountryIsoCode.ToUpperInvariant(), updated.CountryIsoCode);
                Assert.Equal(dto.Locale, updated.Locale);
                Assert.Equal(original.UserId, updated.UserId);
                Assert.Equal(original.DateCreated, updated.DateCreated);
                Assert.Equal(Factory.Clock.GetUtcNow().UtcDateTime, updated.DateModified);
            }
            else
            {
                AssertCompleteVersion(original, await GetUserAsync(original.UserId));
            }

            return true;
        });

        var final = Assert.Single(await GetAllAsync());
        AssertCompleteVersion(original, final);
        Assert.NotNull(final.DateModified);
        var next = await CreateUserAsync();
        UserAssertions.Equal(next, await GetUserAsync(next.UserId));
    }

    [Fact]
    public async Task ConcurrentCreatesUpdatesAndListReads_AreSafeAndKeepAllRecords()
    {
        var original = await CreateUserAsync("US", "en-US");
        Factory.Clock.Advance(TimeSpan.FromMinutes(1));
        var results = await RunConcurrentlyAsync<Guid?>(90, async index =>
        {
            switch (index % 3)
            {
                case 0:
                    return (await CreateUserAsync("DE", "de-DE")).UserId;
                case 1:
                    using (var response = await Client.PutAsJsonAsync($"/api/users/{original.UserId}", Updates[index % Updates.Length]))
                    {
                        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                        AssertCompleteVersion(original, await UserAssertions.ReadUserAsync(response));
                    }

                    return null;
                default:
                    var users = await GetAllAsync();
                    Assert.Equal(users.Length, users.Select(user => user.UserId).Distinct().Count());
                    AssertCompleteVersion(original, Assert.Single(users, user => user.UserId == original.UserId));
                    Assert.All(users.Where(user => user.UserId != original.UserId), user =>
                    {
                        Assert.Equal("DE", user.CountryIsoCode);
                        Assert.Equal("de-DE", user.Locale);
                        Assert.Null(user.DateModified);
                    });
                    return null;
            }
        });

        var createdIds = results.OfType<Guid>().ToArray();
        Assert.Equal(30, createdIds.Length);
        var final = await GetAllAsync();
        Assert.Equal(31, final.Length);
        Assert.All(createdIds, id => Assert.Single(final, user => user.UserId == id));
        AssertCompleteVersion(original, Assert.Single(final, user => user.UserId == original.UserId));
    }

    private static void AssertCompleteVersion(User original, User actual)
    {
        Assert.Equal(original.UserId, actual.UserId);
        Assert.Equal(original.DateCreated, actual.DateCreated);
        Assert.Contains(Updates, dto => dto.CountryIsoCode.ToUpperInvariant() == actual.CountryIsoCode && dto.Locale == actual.Locale);
    }

    private static async Task<T[]> RunConcurrentlyAsync<T>(int count, Func<int, Task<T>> action)
    {
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = Enumerable.Range(0, count).Select(async index =>
        {
            await start.Task;
            return await action(index);
        }).ToArray();
        start.SetResult();
        return await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(30));
    }
}
