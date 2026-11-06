using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using UserManagement.Tests.Infrastructure;

namespace UserManagement.Tests.Tests;

public sealed class ProtocolAndResilienceTests : ApiTestBase
{
    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    public async Task UnsupportedContentType_Returns415_WithoutChangingState(string method)
    {
        var created = await CreateUserAsync();
        using var request = new HttpRequestMessage(new HttpMethod(method),
            method == "POST" ? "/api/users" : $"/api/users/{created.UserId}")
        {
            Content = new StringContent("plain text", Encoding.UTF8, "text/plain")
        };
        using var response = await Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        UserAssertions.Equal(created, await GetUserAsync(created.UserId));
        Assert.Single(await GetAllAsync());
    }

    [Fact]
    public async Task UnsupportedMethod_Returns405()
    {
        var created = await CreateUserAsync();
        using var response = await Client.DeleteAsync($"/api/users/{created.UserId}");
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Contains("GET", response.Content.Headers.Allow);
        Assert.Contains("PUT", response.Content.Headers.Allow);
        UserAssertions.Equal(created, await GetUserAsync(created.UserId));
    }

    [Fact]
    public async Task UnknownRoute_Returns404()
    {
        using var response = await Client.GetAsync("/api/unknown");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(await GetAllAsync());
    }

    [Fact]
    public async Task RepositoryException_ReturnsSafe500_AndNextRequestSucceeds()
    {
        await using var factory = new UserApiFactory(new FailOnceUserRepository());
        using var client = factory.CreateClient();
        using var failedResponse = await client.GetAsync("/api/users");
        Assert.Equal(HttpStatusCode.InternalServerError, failedResponse.StatusCode);
        Assert.Equal("application/problem+json", failedResponse.Content.Headers.ContentType?.MediaType);
        var problem = await failedResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(500, problem.Status);
        Assert.False(string.IsNullOrWhiteSpace(problem.Title));
        Assert.False(string.IsNullOrWhiteSpace(problem.Detail));
        Assert.True(problem.Extensions.ContainsKey("traceId"));
        var errorJson = await failedResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("InvalidOperationException", errorJson);
        Assert.DoesNotContain(FailOnceUserRepository.FailureMessage, errorJson);
        Assert.DoesNotContain(JsonEncodedText.Encode(FailOnceUserRepository.FailureMessage).ToString(), errorJson);
        Assert.DoesNotContain("stackTrace", errorJson, StringComparison.OrdinalIgnoreCase);

        using var createResponse = await client.PostAsJsonAsync("/api/users", ValidDto());
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await UserAssertions.ReadUserAsync(createResponse);
        using var listResponse = await client.GetAsync("/api/users");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        UserAssertions.Equal(created, Assert.Single(await UserAssertions.ReadUsersAsync(listResponse)));
    }
}
