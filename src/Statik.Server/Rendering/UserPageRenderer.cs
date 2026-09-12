using System.Text.Encodings.Web;
using Ganss.Xss;
using Scriban;
using Scriban.Parsing;
using Scriban.Runtime;
using Scriban.Syntax;
using Statik.Server.Models;

namespace Statik.Server.Rendering;

public class UserPageRenderer
{
    private const int MaxSourceLength = 1_000_000;
    private const int MaxOutputLength = 4_000_000;

    private readonly HtmlSanitizer _sanitizer = CreateSanitizer();

    public string Render(UserPage page, User user, CancellationToken cancellationToken = default)
    {
        if (page.BodyTemplate.Length > MaxSourceLength || page.Css.Length > MaxSourceLength)
        {
            throw new UserPageRenderException("Page source exceeds the size limit.");
        }

        var body = _sanitizer.Sanitize(RenderBody(page, user, cancellationToken));

        // CSS escapes preserve literal '<' characters without letting HTML close the style element.
        var css = page.Css.Replace("<", "\\3c ", StringComparison.Ordinal);

        return $"""
                <!doctype html>
                <html lang="en">
                <head>
                    <meta charset="utf-8">
                    <meta name="viewport" content="width=device-width, initial-scale=1">
                    <title>{HtmlEncoder.Default.Encode(user.DisplayName)}</title>
                    <style>{css}</style>
                </head>
                <body>
                    {body}
                </body>
                </html>
                """;
    }

    private static string RenderBody(UserPage page, User user, CancellationToken cancellationToken)
    {
        var template = Template.Parse(page.BodyTemplate, parserOptions: new ParserOptions
        {
            ExpressionDepthLimit = 64
        });

        if (template.HasErrors)
        {
            throw new UserPageRenderException("Invalid page template.");
        }

        var context = EnrichContext(user);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(1));

        var templateContext = new HtmlTemplateContext
        {
            StrictVariables = true,
            EnableRelaxedMemberAccess = false,
            LoopLimit = 1000,
            RecursiveLimit = 32,
            LimitToString = MaxOutputLength,
            RegexTimeOut = TimeSpan.FromMilliseconds(100),
            CancellationToken = timeout.Token
        };

        // Dynamic parsing would bypass the source and parser-depth limits above.
        var objectFunctions = (ScriptObject)templateContext.BuiltinObject["object"]!;
        objectFunctions.Remove("eval");
        objectFunctions.Remove("eval_template");

        templateContext.PushGlobal(context);

        try
        {
            var body = template.Render(templateContext);
            // Scriban appends an ellipsis when it truncates output; reject the partial document.
            if (body.Length > MaxOutputLength)
            {
                throw new UserPageRenderException("Rendered page exceeds the size limit.");
            }

            return body;
        }
        catch (ScriptRuntimeException exception)
        {
            throw new UserPageRenderException("Page template could not be evaluated.", exception);
        }
    }

    private static HtmlSanitizer CreateSanitizer()
    {
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedTags.Clear();
        sanitizer.AllowedTags.UnionWith([
            "a", "abbr", "address", "article", "aside", "b", "bdi", "bdo", "blockquote", "br",
            "caption", "center", "cite", "code", "col", "colgroup", "dd", "del", "details", "dfn",
            "div", "dl", "dt", "em", "figcaption", "figure", "footer", "h1", "h2", "h3", "h4",
            "h5", "h6", "header", "hr", "i", "img", "ins", "kbd", "li", "main", "mark", "nav",
            "ol", "p", "pre", "q", "s", "samp", "section", "small", "span", "strong", "sub",
            "summary", "sup", "table", "tbody", "td", "tfoot", "th", "thead", "time", "tr", "u",
            "ul", "var", "wbr"
        ]);
        sanitizer.AllowedAttributes.Clear();
        sanitizer.AllowedAttributes.UnionWith([
            "id", "class", "style", "title", "lang", "dir", "href", "src", "alt", "width",
            "height", "colspan", "rowspan", "scope", "datetime", "open", "start", "reversed",
            "aria-label", "aria-hidden", "role"
        ]);
        return sanitizer;
    }

    private static ScriptObject EnrichContext(User user)
    {
        var profile = new ScriptObject
        {
            ["username"] = user.UserName,
            ["display_name"] = user.DisplayName,
            ["about"] = user.About
        };

        var context = new ScriptObject
        {
            ["profile"] = profile,
        };
        return context;
    }
}
