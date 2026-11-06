using System.Net.Http.Json;
using System.Text.Json;
using UserManagement.API.Models;

namespace UserManagement.Tests.Infrastructure;

public static class UserAssertions
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<User> ReadUserAsync(HttpResponseMessage response)
    {
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return ReadUser(json.RootElement);
    }

    public static async Task<User[]> ReadUsersAsync(HttpResponseMessage response)
    {
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Array, json.RootElement.ValueKind);
        return json.RootElement.EnumerateArray().Select(ReadUser).ToArray();
    }

    private static User ReadUser(JsonElement element)
    {
        Assert.Equal(JsonValueKind.Object, element.ValueKind);
        Assert.Equal(
            new[] { "countryIsoCode", "dateCreated", "dateModified", "locale", "userId" },
            element.EnumerateObject().Select(property => property.Name).Order().ToArray());

        var id = element.GetProperty("userId");
        Assert.Equal(JsonValueKind.String, id.ValueKind);
        Assert.True(Guid.TryParseExact(id.GetString(), "D", out var userId));
        Assert.NotEqual(Guid.Empty, userId);

        var country = element.GetProperty("countryIsoCode");
        Assert.Equal(JsonValueKind.String, country.ValueKind);
        Assert.Matches(@"\A[A-Z]{2}\z", country.GetString()!);
        var locale = element.GetProperty("locale");
        Assert.Equal(JsonValueKind.String, locale.ValueKind);
        Assert.Matches(@"\A[a-z]{2}-[A-Z]{2}\z", locale.GetString()!);

        AssertUtcDate(element.GetProperty("dateCreated"));
        var modified = element.GetProperty("dateModified");
        if (modified.ValueKind != JsonValueKind.Null)
        {
            AssertUtcDate(modified);
        }

        var user = element.Deserialize<User>(JsonOptions);
        Assert.NotNull(user);
        Assert.True(user.DateModified is null || user.DateModified >= user.DateCreated);
        return user;
    }

    public static void Equal(User expected, User actual)
    {
        Assert.Equal(expected.UserId, actual.UserId);
        Assert.Equal(expected.CountryIsoCode, actual.CountryIsoCode);
        Assert.Equal(expected.Locale, actual.Locale);
        Assert.Equal(expected.DateCreated, actual.DateCreated);
        Assert.Equal(expected.DateModified, actual.DateModified);
    }

    public static async Task ValidationErrorAsync(HttpResponseMessage response, params string[] fields)
    {
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(400, problem.Status);
        Assert.False(string.IsNullOrWhiteSpace(problem.Title));
        Assert.NotEmpty(problem.Errors);
        Assert.All(problem.Errors.Values, errors =>
        {
            Assert.NotEmpty(errors);
            Assert.All(errors, error => Assert.False(string.IsNullOrWhiteSpace(error)));
        });
        foreach (var field in fields)
        {
            Assert.Contains(problem.Errors.Keys, key => key.Equals(field, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static void AssertUtcDate(JsonElement element)
    {
        Assert.Equal(JsonValueKind.String, element.ValueKind);
        Assert.EndsWith("Z", element.GetString());
        Assert.True(element.TryGetDateTime(out var date));
        Assert.Equal(DateTimeKind.Utc, date.Kind);
        Assert.NotEqual(default, date);
    }
}
