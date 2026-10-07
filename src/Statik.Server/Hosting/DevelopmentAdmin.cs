using System.Net;
using DotNetEd.CoreAdmin;
using Microsoft.AspNetCore.Identity;
using Statik.Server.Models;

namespace Statik.Server.Hosting;

public static class DevelopmentAdmin
{
    public static void AddDevelopmentAdmin(this IServiceCollection services)
    {
        services.AddCoreAdmin(new CoreAdminOptions
        {
            Title = "Statik development admin",
            IgnoreEntityTypes = [typeof(User), typeof(IdentityRole), typeof(IdentityUserRole<string>),
                typeof(IdentityUserClaim<string>), typeof(IdentityUserLogin<string>),
                typeof(IdentityUserToken<string>), typeof(IdentityRoleClaim<string>), typeof(IdentityUserPasskey<string>)]
        });
        services.Configure<Microsoft.AspNetCore.Mvc.MvcOptions>(options =>
            options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true);
    }

    public static void UseDevelopmentAdminGuard(this IApplicationBuilder app, SiteOrigins sites, bool development)
    {
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/admin") || context.Request.Path.StartsWithSegments("/api/admin"))
            {
                var address = context.Connection.RemoteIpAddress;
                if (!development || context.Request.Host.Host != sites.App.Host || address is null || !IPAddress.IsLoopback(address))
                {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                }
                context.Response.Headers.CacheControl = "no-store";
                context.Response.Headers.ContentSecurityPolicy = "frame-ancestors 'none'";
                // CoreAdmin skips antiforgery on some forms, so check the origin on every mutation.
                if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method)
                    && context.Request.Headers.Origin != sites.App.GetLeftPart(UriPartial.Authority))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return;
                }
            }
            await next(context);
        });
    }
}
