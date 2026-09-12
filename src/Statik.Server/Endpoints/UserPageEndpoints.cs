using Microsoft.AspNetCore.Identity;
using Statik.Server.Data;
using Statik.Server.Models;
using Statik.Server.Rendering;

namespace Statik.Server.Endpoints;

public static class UserPageEndpoints
{
    public static void MapUserPageEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/{username}", GetUserPage)
            .RequireHost("statik.club", "localhost", "127.0.0.1");
    }

    private static async Task<IResult> GetUserPage(string username, UserManager<User> manager,
        StatikDbContext db, UserPageRenderer renderer, HttpContext context, ILogger<UserPageRenderer> logger)
    {
        context.Response.Headers.ContentSecurityPolicy =
            "default-src 'none'; script-src 'none'; style-src 'unsafe-inline'; " +
            "img-src 'self' https:; font-src 'self'; connect-src 'none'; " +
            "object-src 'none'; frame-src 'none'; base-uri 'none'; form-action 'none'; " +
            "frame-ancestors 'none'; sandbox allow-same-origin";
        context.Response.Headers.XContentTypeOptions = "nosniff";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";

        var user = await manager.FindByNameAsync(username);
        if (user == null)
        {
            return Results.NotFound();
        }

        var page = await db.UserPages.FindAsync(user.Id);
        if (page == null)
        {
            return Results.NotFound();
        }

        try
        {
            return Results.Content(renderer.Render(page, user, context.RequestAborted), "text/html; charset=utf-8");
        }
        catch (UserPageRenderException exception)
        {
            logger.LogWarning(exception, "Could not render page for user {UserId}", user.Id);
            context.Response.Headers.CacheControl = "no-store";
            return Results.Content("This page could not be displayed.", "text/plain; charset=utf-8",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
