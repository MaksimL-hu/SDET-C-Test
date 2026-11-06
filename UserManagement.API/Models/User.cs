namespace UserManagement.API.Models;

public sealed class User
{
    public required Guid UserId { get; init; }

    public required string CountryIsoCode { get; init; }

    public required DateTime DateCreated { get; init; }

    public DateTime? DateModified { get; init; }

    public required string Locale { get; init; }
}
