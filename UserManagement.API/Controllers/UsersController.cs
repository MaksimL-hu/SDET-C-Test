using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using UserManagement.API.DTOs;
using UserManagement.API.Interfaces;
using UserManagement.API.Models;

namespace UserManagement.API.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UsersController(IUserRepository repository, TimeProvider timeProvider) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<User>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public ActionResult<User> Create(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow), Required(ErrorMessage = "Тело запроса обязательно.")]
        UserDto dto)
    {
        var user = repository.Add(new User
        {
            UserId = Guid.NewGuid(),
            CountryIsoCode = dto.CountryIsoCode.ToUpperInvariant(),
            Locale = dto.Locale,
            DateCreated = timeProvider.GetUtcNow().UtcDateTime
        });

        return CreatedAtAction(nameof(GetById), new { userId = user.UserId }, user);
    }

    [HttpGet]
    [ProducesResponseType<IEnumerable<User>>(StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<User>> GetAll() => Ok(repository.GetAll());

    [HttpGet("{userId}")]
    [ProducesResponseType<User>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public ActionResult<User> GetById(string userId)
    {
        if (!TryValidateUserId(userId, out var id))
            return ValidationProblem(ModelState);

        var user = repository.GetById(id);
        return user is null ? UserNotFound(id) : Ok(user);
    }

    [HttpPut("{userId}")]
    [ProducesResponseType<User>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public ActionResult<User> Update(string userId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow), Required(ErrorMessage = "Тело запроса обязательно.")]
        UserDto dto)
    {
        if (!TryValidateUserId(userId, out var id))
            return ValidationProblem(ModelState);

        var existing = repository.GetById(id);
        if (existing is null)
            return UserNotFound(id);

        var updated = new User
        {
            UserId = existing.UserId,
            DateCreated = existing.DateCreated,
            DateModified = timeProvider.GetUtcNow().UtcDateTime,
            CountryIsoCode = dto.CountryIsoCode.ToUpperInvariant(),
            Locale = dto.Locale
        };

        return repository.Update(updated) ? Ok(updated) : UserNotFound(id);
    }

    private bool TryValidateUserId(string userId, out Guid id)
    {
        if (Guid.TryParse(userId, out id) && id != Guid.Empty)
            return true;

        ModelState.AddModelError(nameof(userId), "UserId должен быть корректным GUID, отличным от Guid.Empty.");
        return false;
    }

    private ObjectResult UserNotFound(Guid userId) => Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "Пользователь не найден.",
        detail: $"Пользователь с идентификатором {userId} не существует.");
}
