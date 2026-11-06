using System.Text.Json.Serialization;
using UserManagement.API.Infrastructure;
using UserManagement.API.Interfaces;
using UserManagement.API.Repositories;

namespace UserManagement.API;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddControllers()
            .AddJsonOptions(options =>
                options.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow);
        builder.Services.AddSingleton<IUserRepository, InMemoryUserRepository>();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<ApiExceptionHandler>();

        var app = builder.Build();

        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.MapControllers();

        app.Run();
    }
}
