using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Statik.Server.Data;
using Statik.Server.Models;

namespace Statik.Server.Endpoints;

public static class DevelopmentUserEndpoints
{
    public static void MapDevelopmentUserEndpoints(this RouteGroupBuilder api)
    {
        var users = api.MapGroup("/admin/users");
        users.MapGet("", async (UserManager<User> manager) => Results.Ok(await manager.Users.OrderBy(user => user.UserName)
            .Select(user => new { user.Id, user.UserName, user.DisplayName, user.About, user.Email, user.EmailConfirmed }).ToListAsync()));
        users.MapPost("", (UserRequest request, UserManager<User> manager, StatikDbContext db) => Save(null, request, manager, db));
        users.MapPut("/{id}", Save);
        users.MapDelete("/{id}", async (string id, UserManager<User> manager, StatikDbContext db) =>
        {
            var user = await manager.FindByIdAsync(id);
            if (user is null) return Results.NotFound();
            await db.UserPages.FindAsync(id);
            return Result(await manager.DeleteAsync(user));
        });
    }

    private static async Task<IResult> Save(string? id, UserRequest request, UserManager<User> manager, StatikDbContext db)
    {
        if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.DisplayName)
            || request.DisplayName.Length > 25 || request.About is null || request.About.Length > 200
            || string.IsNullOrWhiteSpace(request.Email) || request.Email.Length > 256
            || !new EmailAddressAttribute().IsValid(request.Email))
        {
            return Results.Problem("Provide a username, display name (1-25 characters), email, and about text (up to 200 characters).", statusCode: 400);
        }
        var user = id is null ? new User { DisplayName = request.DisplayName } : await manager.FindByIdAsync(id);
        if (user is null) return Results.NotFound();
        if (id is null && string.IsNullOrEmpty(request.Password))
        {
            return Results.Problem("A password is required for a new account.", statusCode: 400);
        }

        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync() : null;
        user.UserName = request.UserName;
        user.DisplayName = request.DisplayName;
        user.About = request.About;
        user.Email = request.Email.Trim();
        user.EmailConfirmed = request.EmailConfirmed;

        if (!string.IsNullOrEmpty(request.Password))
        {
            foreach (var validator in manager.PasswordValidators)
            {
                var validation = await validator.ValidateAsync(manager, user, request.Password);
                if (!validation.Succeeded) return Result(validation);
            }
        }
        var result = id is null ? await manager.CreateAsync(user, request.Password!) : await manager.UpdateAsync(user);
        if (!result.Succeeded) return Result(result);
        if (id is not null)
        {
            result = string.IsNullOrEmpty(request.Password)
                ? await manager.UpdateSecurityStampAsync(user)
                : await manager.ResetPasswordAsync(user, await manager.GeneratePasswordResetTokenAsync(user), request.Password);
            if (!result.Succeeded) return Result(result);
        }
        if (transaction is not null) await transaction.CommitAsync();
        return Results.NoContent();
    }

    private static IResult Result(IdentityResult result) => result.Succeeded ? Results.NoContent()
        : Results.ValidationProblem(result.Errors.GroupBy(error => error.Code)
            .ToDictionary(group => group.Key, group => group.Select(error => error.Description).ToArray()));

    public record UserRequest(string? UserName, string? DisplayName, string? About, string? Email, bool EmailConfirmed, string? Password);
}
