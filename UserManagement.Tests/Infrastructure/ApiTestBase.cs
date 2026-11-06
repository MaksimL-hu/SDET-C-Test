using System.Net;
using System.Net.Http.Json;
using UserManagement.API.DTOs;
using UserManagement.API.Models;

namespace UserManagement.Tests.Infrastructure;

public abstract class ApiTestBase : IAsyncLifetime
{
    protected UserApiFactory Factory { get; } = new();

    protected HttpClient Client { get; }

    protected ApiTestBase() => Client = Factory.CreateClient();

    public async Task InitializeAsync() => Assert.Empty(await GetAllAsync());

    public async Task DisposeAsync()
    {
        Client.Dispose();
        await Factory.DisposeAsync();
    }

    protected async Task<User> CreateUserAsync(string country = "RU", string locale = "ru-RU")
    {
        using var response = await Client.PostAsJsonAsync("/api/users", new UserDto
        {
            CountryIsoCode = country,
            Locale = locale
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await UserAssertions.ReadUserAsync(response);
    }

    protected async Task<User> GetUserAsync(Guid id)
    {
        using var response = await Client.GetAsync($"/api/users/{id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await UserAssertions.ReadUserAsync(response);
    }

    protected async Task<User[]> GetAllAsync()
    {
        using var response = await Client.GetAsync("/api/users");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await UserAssertions.ReadUsersAsync(response);
    }

    protected static UserDto ValidDto(string country = "US", string locale = "en-US") => new()
    {
        CountryIsoCode = country,
        Locale = locale
    };
}
