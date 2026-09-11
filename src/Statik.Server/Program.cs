var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapFallback("/api/{**path}", () => Results.NotFound());
app.MapFallbackToFile("index.html")
    .RequireHost("app.statik.club", "localhost", "127.0.0.1");

app.Run();
