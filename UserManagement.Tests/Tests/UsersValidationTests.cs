using System.Net;
using System.Net.Http.Json;
using System.Text;
using UserManagement.API.Models;
using UserManagement.Tests.Infrastructure;

namespace UserManagement.Tests.Tests;

public sealed class UsersValidationTests : ApiTestBase
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("R")]
    [InlineData("RUS")]
    [InlineData("12")]
    [InlineData("R1")]
    [InlineData("R!")]
    [InlineData("--")]
    [InlineData("РУ")]
    [InlineData("ÜS")]
    [InlineData(" RU")]
    [InlineData("RU ")]
    [InlineData("RU\n")]
    public async Task InvalidCountry_IsRejectedByPostAndPut_WithoutChangingState(string? country)
    {
        await AssertInvalidBodyAsync(new { countryIsoCode = country, locale = "ru-RU" }, "CountryIsoCode");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("r-RU")]
    [InlineData("rus-RU")]
    [InlineData("ru-R")]
    [InlineData("ru-RUS")]
    [InlineData("RU-ru")]
    [InlineData("RU-RU")]
    [InlineData("ru-ru")]
    [InlineData("Ru-RU")]
    [InlineData("ru_RU")]
    [InlineData("ruRU")]
    [InlineData("12-34")]
    [InlineData("r!-R!")]
    [InlineData("ру-РУ")]
    [InlineData(" ru-RU")]
    [InlineData("ru-RU ")]
    [InlineData("ru-RU\n")]
    public async Task InvalidLocale_IsRejectedByPostAndPut_WithoutChangingState(string? locale)
    {
        await AssertInvalidBodyAsync(new { countryIsoCode = "RU", locale }, "Locale");
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("123")]
    [InlineData(" ")]
    [InlineData("12345678-1234-1234-1234-12345678901")]
    [InlineData("gggggggg-1234-1234-1234-123456789012")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("00000000000000000000000000000000")]
    public async Task InvalidGuid_IsRejectedByGetAndPut(string id)
    {
        var existing = await CreateUserAsync();
        var url = $"/api/users/{Uri.EscapeDataString(id)}";
        using var getResponse = await Client.GetAsync(url);
        await UserAssertions.ValidationErrorAsync(getResponse, "userId");
        using var putResponse = await Client.PutAsJsonAsync(url, ValidDto());
        await UserAssertions.ValidationErrorAsync(putResponse, "userId");
        await AssertUnchangedAsync(existing);
    }

    [Fact]
    public async Task BothInvalidFields_ReturnBothErrors()
    {
        await AssertInvalidBodyAsync(new { countryIsoCode = "123", locale = "ru_ru" }, "CountryIsoCode", "Locale");
    }

    [Theory]
    [InlineData("{}", "CountryIsoCode", "Locale")]
    [InlineData("{\"locale\":\"ru-RU\"}", "CountryIsoCode", "CountryIsoCode")]
    [InlineData("{\"countryIsoCode\":\"RU\"}", "Locale", "Locale")]
    public async Task MissingFields_AreRejected(string json, string firstField, string secondField)
    {
        await AssertInvalidJsonAsync(json, firstField, secondField);
    }

    [Theory]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("\"text\"")]
    [InlineData("123")]
    [InlineData("{\"countryIsoCode\":12,\"locale\":\"ru-RU\"}")]
    [InlineData("{\"countryIsoCode\":[],\"locale\":\"ru-RU\"}")]
    [InlineData("{\"countryIsoCode\":{},\"locale\":\"ru-RU\"}")]
    [InlineData("{\"countryIsoCode\":\"RU\",\"locale\":123}")]
    [InlineData("{\"countryIsoCode\":\"RU\",\"locale\":[]}")]
    [InlineData("{\"countryIsoCode\":\"RU\",\"locale\":{}}")]
    public async Task MalformedOrWrongTypeJson_IsRejected(string json)
    {
        await AssertInvalidJsonAsync(json);
    }

    [Theory]
    [InlineData("userId", "11111111-1111-1111-1111-111111111111")]
    [InlineData("dateCreated", "2000-01-01T00:00:00Z")]
    [InlineData("dateModified", "2000-01-01T00:00:00Z")]
    [InlineData("unexpected", "value")]
    public async Task ClientCannotSupplyServerOwnedOrUnknownFields(string field, string value)
    {
        await AssertInvalidBodyAsync(new Dictionary<string, object>
        {
            ["countryIsoCode"] = "RU",
            ["locale"] = "ru-RU",
            [field] = value
        });
    }

    [Fact]
    public async Task MissingRequestBody_IsRejectedByPostAndPut()
    {
        var existing = await CreateUserAsync();
        foreach (var method in new[] { HttpMethod.Post, HttpMethod.Put })
        {
            using var request = new HttpRequestMessage(method,
                method == HttpMethod.Post ? "/api/users" : $"/api/users/{existing.UserId}");
            using var response = await Client.SendAsync(request);
            await UserAssertions.ValidationErrorAsync(response);
            await AssertUnchangedAsync(existing);
        }
    }

    [Fact]
    public async Task ValidRequestAfterValidationFailure_StillSucceeds()
    {
        using var invalidResponse = await Client.PostAsJsonAsync("/api/users", new { countryIsoCode = "!", locale = "bad" });
        await UserAssertions.ValidationErrorAsync(invalidResponse);
        var created = await CreateUserAsync();
        using var updateResponse = await Client.PutAsJsonAsync($"/api/users/{created.UserId}", ValidDto());
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await UserAssertions.ReadUserAsync(updateResponse);
        Assert.Equal("US", updated.CountryIsoCode);
        Assert.Equal("en-US", updated.Locale);
        UserAssertions.Equal(updated, await GetUserAsync(created.UserId));
    }

    private async Task AssertInvalidBodyAsync(object body, params string[] fields)
    {
        var existing = await CreateUserAsync();
        Factory.Clock.Advance(TimeSpan.FromMinutes(1));
        using var postResponse = await Client.PostAsJsonAsync("/api/users", body);
        await UserAssertions.ValidationErrorAsync(postResponse, fields);
        await AssertUnchangedAsync(existing);
        using var putResponse = await Client.PutAsJsonAsync($"/api/users/{existing.UserId}", body);
        await UserAssertions.ValidationErrorAsync(putResponse, fields);
        await AssertUnchangedAsync(existing);
    }

    private async Task AssertInvalidJsonAsync(string json, params string[] fields)
    {
        var existing = await CreateUserAsync();
        Factory.Clock.Advance(TimeSpan.FromMinutes(1));
        foreach (var method in new[] { HttpMethod.Post, HttpMethod.Put })
        {
            using var request = new HttpRequestMessage(method,
                method == HttpMethod.Post ? "/api/users" : $"/api/users/{existing.UserId}")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            using var response = await Client.SendAsync(request);
            await UserAssertions.ValidationErrorAsync(response, fields);
            await AssertUnchangedAsync(existing);
        }
    }

    private async Task AssertUnchangedAsync(User existing)
    {
        UserAssertions.Equal(existing, await GetUserAsync(existing.UserId));
        UserAssertions.Equal(existing, Assert.Single(await GetAllAsync()));
    }
}
