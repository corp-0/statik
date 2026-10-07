namespace Statik.Server.Hosting;

public class SiteOrigins
{
    public required Uri Public { get; init; }
    public required Uri App { get; init; }

    public static SiteOrigins FromConfiguration(IConfiguration configuration)
    {
        var origins = configuration.GetSection("Sites").Get<SiteOrigins>()
            ?? throw new InvalidOperationException("Sites:Public and Sites:App are required.");
        foreach (var origin in new[] { origins.Public, origins.App })
        {
            if (origin is null || !origin.IsAbsoluteUri || origin.Scheme != "https"
                || origin.AbsolutePath != "/" || origin.Query != "" || origin.Fragment != "" || origin.UserInfo != "")
            {
                throw new InvalidOperationException("Site origins must be HTTPS URLs without paths, queries, or credentials.");
            }
        }
        if (origins.Public.Host == origins.App.Host)
        {
            throw new InvalidOperationException("Public pages and the application must use different hosts.");
        }
        return origins;
    }
}
