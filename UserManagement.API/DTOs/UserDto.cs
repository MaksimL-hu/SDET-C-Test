using System.ComponentModel.DataAnnotations;

namespace UserManagement.API.DTOs;

public sealed class UserDto
{
    [Required(ErrorMessage = "CountryIsoCode обязателен.")]
    [RegularExpression(@"\A[A-Za-z]{2}\z",
        ErrorMessage = "CountryIsoCode должен содержать ровно две буквы латиницы без пробелов.")]
    public string CountryIsoCode { get; init; } = string.Empty;

    [Required(ErrorMessage = "Locale обязателен.")]
    [RegularExpression(@"\A[a-z]{2}-[A-Z]{2}\z",
        ErrorMessage = "Locale должен иметь формат xx-XX, например ru-RU или en-US.")]
    public string Locale { get; init; } = string.Empty;
}
