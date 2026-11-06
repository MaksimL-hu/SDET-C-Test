# User Management API

REST API на ASP.NET Core (.NET 10) для создания, просмотра и обновления пользователей. Данные хранятся в памяти и удаляются при остановке сервиса.

## Запуск

Нужны .NET 10 SDK и Visual Studio 2026 с компонентом «ASP.NET и веб-разработка».

1. Откройте `UserManagement.sln` и дождитесь восстановления пакетов NuGet.
2. Назначьте `UserManagement.API` запускаемым проектом.
3. Выберите профиль `http` и нажмите **Ctrl+F5**.

API доступен по адресу: [http://localhost:5080/api/users](http://localhost:5080/api/users).

Также можно запустить сервис из корня решения:

```powershell
dotnet run --project UserManagement.API --launch-profile http
```

## API

| Метод | Адрес | Действие |
| --- | --- | --- |
| POST | `/api/users` | Создать пользователя |
| GET | `/api/users` | Получить всех пользователей |
| GET | `/api/users/{userId}` | Получить пользователя по ID |
| PUT | `/api/users/{userId}` | Обновить пользователя |

Тело POST и PUT:

```json
{
  "countryIsoCode": "ru",
  "locale": "ru-RU"
}
```

Код страны — две латинские буквы, локаль — в формате `xx-XX`. ID должен быть непустым GUID. Ошибки валидации возвращают `400`, отсутствующий пользователь — `404`. ID и даты задаёт сервер.

Примеры запросов находятся в [UserManagement.API.http](UserManagement.API/UserManagement.API.http).

## Тесты

Запуск из корня решения:

```powershell
dotnet test
```

В Visual Studio: **Тест → Обозреватель тестов → Выполнить все**.

В `UserManagement.Tests` используются xUnit и WebApplicationFactory. Тесты проверяют полный цикл работы с пользователем, валидацию, параллельные запросы и обработку ошибок. Каждый тест запускается с отдельным хранилищем.

Полный чек-лист проверок — в [test-cases.md](test-cases.md).
