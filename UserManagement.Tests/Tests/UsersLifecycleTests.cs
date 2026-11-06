using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using UserManagement.Tests.Infrastructure;

namespace UserManagement.Tests.Tests;

public sealed class UsersLifecycleTests : ApiTestBase
{
    [Fact]
    public async Task FullLifecycle_CreateReadUpdateRead_PreservesServerOwnedFields()
    {
        var createdAt = Factory.Clock.GetUtcNow().UtcDateTime;
        using var createResponse = await Client.PostAsJsonAsync("/api/users", ValidDto("ru", "ru-RU"));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await UserAssertions.ReadUserAsync(createResponse);
        Assert.Equal("RU", created.CountryIsoCode);
        Assert.Equal("ru-RU", created.Locale);
        Assert.Equal(createdAt, created.DateCreated);
        Assert.Null(created.DateModified);
        Assert.NotNull(createResponse.Headers.Location);
        Assert.EndsWith($"/api/users/{created.UserId}", createResponse.Headers.Location.ToString());

        using var getResponse = await Client.GetAsync(createResponse.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        UserAssertions.Equal(created, await UserAssertions.ReadUserAsync(getResponse));

        Factory.Clock.Advance(TimeSpan.FromMinutes(5));
        var modifiedAt = Factory.Clock.GetUtcNow().UtcDateTime;
        using var updateResponse = await Client.PutAsJsonAsync($"/api/users/{created.UserId}", ValidDto("uS", "en-US"));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await UserAssertions.ReadUserAsync(updateResponse);
        Assert.Equal(created.UserId, updated.UserId);
        Assert.Equal(created.DateCreated, updated.DateCreated);
        Assert.Equal(modifiedAt, updated.DateModified);
        Assert.Equal("US", updated.CountryIsoCode);
        Assert.Equal("en-US", updated.Locale);
        UserAssertions.Equal(updated, await GetUserAsync(created.UserId));
        UserAssertions.Equal(updated, Assert.Single(await GetAllAsync()));
    }

    [Theory]
    [InlineData("ru", "ru-RU", "RU")]
    [InlineData("uS", "en-US", "US")]
    [InlineData("GB", "en-GB", "GB")]
    [InlineData("zz", "aa-BB", "ZZ")]
    [InlineData("RU", "en-US", "RU")]
    public async Task Create_NormalizesOnlyCountry(string country, string locale, string expectedCountry)
    {
        var user = await CreateUserAsync(country, locale);
        Assert.Equal(expectedCountry, user.CountryIsoCode);
        Assert.Equal(locale, user.Locale);
        Assert.Equal(Factory.Clock.GetUtcNow().UtcDateTime, user.DateCreated);
        Assert.Null(user.DateModified);
        UserAssertions.Equal(user, await GetUserAsync(user.UserId));
    }

    [Fact]
    public async Task GetAll_EmptyRepository_ReturnsEmptyJsonArray() => Assert.Empty(await GetAllAsync());

    [Fact]
    public async Task IdenticalCreates_GetDifferentIdentifiers_AndBothAppearInList()
    {
        var first = await CreateUserAsync();
        var second = await CreateUserAsync();
        Assert.NotEqual(first.UserId, second.UserId);
        var users = await GetAllAsync();
        Assert.Equal(2, users.Length);
        UserAssertions.Equal(first, Assert.Single(users, user => user.UserId == first.UserId));
        UserAssertions.Equal(second, Assert.Single(users, user => user.UserId == second.UserId));
    }

    [Fact]
    public async Task RepeatedUpdate_UpdatesTimestamp_AndDoesNotChangeOtherUsers()
    {
        var first = await CreateUserAsync();
        var other = await CreateUserAsync("DE", "de-DE");
        for (var attempt = 0; attempt < 2; attempt++)
        {
            Factory.Clock.Advance(TimeSpan.FromMinutes(1));
            using var response = await Client.PutAsJsonAsync($"/api/users/{first.UserId}", ValidDto("ru", "ru-RU"));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var updated = await UserAssertions.ReadUserAsync(response);
            Assert.Equal(first.UserId, updated.UserId);
            Assert.Equal(first.DateCreated, updated.DateCreated);
            Assert.Equal(Factory.Clock.GetUtcNow().UtcDateTime, updated.DateModified);
            Assert.Equal("RU", updated.CountryIsoCode);
            Assert.Equal("ru-RU", updated.Locale);
            UserAssertions.Equal(updated, await GetUserAsync(first.UserId));
        }

        UserAssertions.Equal(other, await GetUserAsync(other.UserId));
        Assert.Equal(2, (await GetAllAsync()).Length);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    public async Task UnknownGuid_Returns404WithoutAddingUsers(string method)
    {
        var existing = await CreateUserAsync();
        var unknownId = Guid.NewGuid();
        using var request = new HttpRequestMessage(new HttpMethod(method), $"/api/users/{unknownId}");
        if (method == "PUT")
        {
            request.Content = JsonContent.Create(ValidDto());
        }

        using var response = await Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(404, problem.Status);
        Assert.Contains(unknownId.ToString(), problem.Detail);
        UserAssertions.Equal(existing, Assert.Single(await GetAllAsync()));
    }

    [Theory]
    [InlineData("N")]
    [InlineData("D")]
    [InlineData("B")]
    [InlineData("P")]
    public async Task Get_RecognizesValidGuidFormats(string format)
    {
        var created = await CreateUserAsync();
        using var response = await Client.GetAsync($"/api/users/{Uri.EscapeDataString(created.UserId.ToString(format).ToUpperInvariant())}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        UserAssertions.Equal(created, await UserAssertions.ReadUserAsync(response));
    }

    [Fact]
    public async Task SeparateApplicationInstances_DoNotShareRepositoryState()
    {
        var first = await CreateUserAsync();
        await using var otherFactory = new UserApiFactory();
        using var otherClient = otherFactory.CreateClient();
        using var listResponse = await otherClient.GetAsync("/api/users");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Empty(await UserAssertions.ReadUsersAsync(listResponse));
        using var missingResponse = await otherClient.GetAsync($"/api/users/{first.UserId}");
        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);
        UserAssertions.Equal(first, Assert.Single(await GetAllAsync()));
    }
}
