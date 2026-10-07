using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Statik.Server.Data;
using Statik.Server.Models;
using Statik.Server.Hosting;
using Statik.Server.Identity;
using System.ComponentModel.DataAnnotations;

namespace Statik.Server.Endpoints;

public static class ManagementEndpoints
{
    public static void MapManagementEndpoints(this IEndpointRouteBuilder endpoints, SiteOrigins sites)
    {
        var api = endpoints.MapGroup("/api")
            .RequireHost(sites.App.Host);

        api.AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            http.Response.Headers.CacheControl = "no-store";
            if (!HttpMethods.IsGet(http.Request.Method))
            {
                var antiforgery = http.RequestServices.GetRequiredService<IAntiforgery>();
                try
                {
                    await antiforgery.ValidateRequestAsync(http);
                }
                catch (AntiforgeryValidationException)
                {
                    return Results.Problem("Reload the page and try again.", statusCode: 400);
                }
            }
            try
            {
                return await next(context);
            }
            catch (AccountEmailDeliveryException)
            {
                return Results.Problem("Email delivery is unavailable. Try resending confirmation or requesting a password reset later.", statusCode: 503);
            }
        });

        api.MapGet("/auth/csrf", (IAntiforgery antiforgery, HttpContext context) =>
            Results.Ok(new { token = antiforgery.GetAndStoreTokens(context).RequestToken }));
        if (endpoints.ServiceProvider.GetRequiredService<IHostEnvironment>().IsDevelopment())
        {
            api.MapDevelopmentUserEndpoints();
        }
        var auth = api.MapGroup("/auth").RequireRateLimiting("authentication");
        auth.MapPost("/signup", SignUp);
        auth.MapPost("/login", Login);
        auth.MapPost("/confirm-email", ConfirmEmail);
        auth.MapPost("/resend-confirmation", ResendConfirmation);
        auth.MapPost("/forgot-password", ForgotPassword);
        auth.MapPost("/reset-password", ResetPassword);
        api.MapPost("/auth/logout", async (SignInManager<User> signIn) =>
        {
            await signIn.SignOutAsync();
            return Results.NoContent();
        }).RequireAuthorization();

        var me = api.MapGroup("/me").RequireAuthorization();
        me.MapGet("", async (UserManager<User> manager, HttpContext context) =>
        {
            var user = await manager.GetUserAsync(context.User);
            return user is null ? Results.Unauthorized() : Results.Ok(Profile(user));
        });
        me.MapGet("/page", async (UserManager<User> manager, StatikDbContext db, HttpContext context) =>
        {
            var page = await db.UserPages.FindAsync(manager.GetUserId(context.User));
            return page is null ? Results.NotFound() : Results.Ok(new { page.BodyTemplate, page.Css });
        });
        me.MapPut("/page", SavePage);
        me.MapDelete("/page", async (UserManager<User> manager, StatikDbContext db, HttpContext context) =>
        {
            var page = await db.UserPages.FindAsync(manager.GetUserId(context.User));
            if (page is not null)
            {
                db.UserPages.Remove(page);
                await db.SaveChangesAsync(context.RequestAborted);
            }
            return Results.NoContent();
        });
    }

    private static async Task<IResult> SignUp(SignUpRequest request, UserManager<User> manager,
        AccountEmail email, HttpContext context)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.DisplayName)
            || request.DisplayName.Length > 25 || string.IsNullOrEmpty(request.Password) || !ValidEmail(request.Email))
        {
            return Results.Problem("Enter a username, a display name of 1-25 characters, a valid email, and a password.", statusCode: 400);
        }

        if (await manager.FindByEmailAsync(request.Email!.Trim()) is not null)
        {
            return Results.NoContent();
        }
        var user = new User { UserName = request.Username, DisplayName = request.DisplayName.Trim(), Email = request.Email.Trim() };
        var result = await manager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return Results.ValidationProblem(result.Errors.GroupBy(error => error.Code)
                .ToDictionary(group => group.Key, group => group.Select(error => error.Description).ToArray()));
        }
        await email.SendConfirmationAsync(user, context.RequestAborted);
        return Results.NoContent();
    }

    private static async Task<IResult> Login(LoginRequest request, SignInManager<User> signIn, UserManager<User> manager)
    {
        if (!ValidEmail(request.Email) || string.IsNullOrEmpty(request.Password))
        {
            return Results.Problem("Enter your email and password.", statusCode: 400);
        }
        var user = await manager.FindByEmailAsync(request.Email!.Trim());
        var result = user is null ? SignInResult.Failed : await signIn.PasswordSignInAsync(user, request.Password,
            isPersistent: false, lockoutOnFailure: true);
        return result.Succeeded
            ? Results.NoContent()
            : Results.Problem("Unable to log in. Check your credentials, confirm your email, or try again later.", statusCode: 401);
    }

    private static bool ValidEmail(string? email) => !string.IsNullOrWhiteSpace(email)
        && email.Length <= 256 && new EmailAddressAttribute().IsValid(email.Trim());

    private static async Task<IResult> ConfirmEmail(TokenRequest request, UserManager<User> manager)
    {
        if (!ValidEmail(request.Email) || string.IsNullOrEmpty(request.Token))
        {
            return Results.Problem("This confirmation link is invalid or expired. Request a new one.", statusCode: 400);
        }
        var user = await manager.FindByEmailAsync(request.Email!.Trim());
        if (user is null || !(await manager.ConfirmEmailAsync(user, request.Token)).Succeeded)
        {
            return Results.Problem("This confirmation link is invalid or expired. Request a new one.", statusCode: 400);
        }
        return Results.NoContent();
    }

    private static async Task<IResult> ResendConfirmation(EmailRequest request, UserManager<User> manager,
        AccountEmail email, HttpContext context)
    {
        if (!ValidEmail(request.Email))
        {
            return Results.Problem("Enter a valid email address.", statusCode: 400);
        }
        var user = await manager.FindByEmailAsync(request.Email!.Trim());
        if (user is not null && !user.EmailConfirmed)
        {
            await email.SendConfirmationAsync(user, context.RequestAborted);
        }
        return Results.NoContent();
    }

    private static async Task<IResult> ForgotPassword(EmailRequest request, UserManager<User> manager,
        AccountEmail email, HttpContext context)
    {
        if (!ValidEmail(request.Email))
        {
            return Results.Problem("Enter a valid email address.", statusCode: 400);
        }
        var user = await manager.FindByEmailAsync(request.Email!.Trim());
        if (user is not null && user.EmailConfirmed)
        {
            await email.SendPasswordResetAsync(user, context.RequestAborted);
        }
        return Results.NoContent();
    }

    private static async Task<IResult> ResetPassword(ResetPasswordRequest request, UserManager<User> manager)
    {
        if (!ValidEmail(request.Email) || string.IsNullOrEmpty(request.Token) || string.IsNullOrEmpty(request.Password))
        {
            return Results.Problem("Provide a valid reset link and a new password.", statusCode: 400);
        }
        var user = await manager.FindByEmailAsync(request.Email!.Trim());
        if (user is null || !user.EmailConfirmed)
        {
            return Results.Problem("This reset link is invalid or expired.", statusCode: 400);
        }
        var result = await manager.ResetPasswordAsync(user, request.Token, request.Password);
        return result.Succeeded ? Results.NoContent() : Results.ValidationProblem(result.Errors.GroupBy(error => error.Code)
            .ToDictionary(group => group.Key, group => group.Select(error => error.Description).ToArray()));
    }

    private static async Task<IResult> SavePage(PageRequest request, UserManager<User> manager,
        StatikDbContext db, HttpContext context)
    {
        if (request.BodyTemplate is null || request.Css is null
            || request.BodyTemplate.Length > 1_000_000 || request.Css.Length > 1_000_000)
        {
            return Results.Problem("HTML and CSS are required and may each contain at most 1,000,000 characters.", statusCode: 400);
        }
        var user = await manager.GetUserAsync(context.User);
        if (user is null)
        {
            return Results.Unauthorized();
        }
        var page = await db.UserPages.FindAsync(user.Id);
        if (page is null)
        {
            page = new UserPage { UserId = user.Id, Owner = user, BodyTemplate = request.BodyTemplate, Css = request.Css };
            db.UserPages.Add(page);
        }
        else
        {
            page.BodyTemplate = request.BodyTemplate;
            page.Css = request.Css;
        }
        await db.SaveChangesAsync(context.RequestAborted);
        return Results.Ok(new { page.BodyTemplate, page.Css });
    }

    private static object Profile(User user) => new { username = user.UserName, user.DisplayName };

    public record SignUpRequest(string? Username, string? DisplayName, string? Email, string? Password);
    public record LoginRequest(string? Email, string? Password);
    public record EmailRequest(string? Email);
    public record TokenRequest(string? Email, string? Token);
    public record ResetPasswordRequest(string? Email, string? Token, string? Password);
    public record PageRequest(string? BodyTemplate, string? Css);
}
