using Microsoft.AspNetCore.Identity;
using Statik.Server.Data;
using Statik.Server.Models;

namespace Statik.Server.Identity;

public static class ManagementAuthentication
{
    public static IServiceCollection AddManagementAuthentication(this IServiceCollection services)
    {
        services.AddIdentityCore<User>(options =>
            {
                options.User.AllowedUserNameCharacters = StatikUserValidator.AllowedUserNameCharacters;
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedEmail = true;
            })
            .AddEntityFrameworkStores<StatikDbContext>()
            .AddUserValidator<StatikUserValidator>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        services.AddOptions<IdentityOptions>().Configure<IHostEnvironment>((options, environment) =>
        {
            if (environment.IsDevelopment())
            {
                options.Password.RequiredLength = 1;
                options.Password.RequiredUniqueChars = 0;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
            }
        });

        services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "__Host-statik_auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });
        services.AddAuthorization();
        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-Token";
            options.Cookie.Name = "__Host-statik_csrf";
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        });
        return services;
    }
}
