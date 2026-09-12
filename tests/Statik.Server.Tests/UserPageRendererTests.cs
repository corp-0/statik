using AngleSharp.Html.Parser;
using Statik.Server.Models;
using Statik.Server.Rendering;

namespace Statik.Server.Tests;

public class UserPageRendererTests
{
    private readonly UserPageRenderer _renderer = new();
    private readonly User _user = new()
    {
        UserName = "gilles",
        DisplayName = "Gilles & friends",
        About = "<img src=x onerror=alert(1)>"
    };

    [Fact]
    public void PreservesLayoutAndEscapesProfileValues()
    {
        var html = _renderer.Render(Page("""
            <main class="page" id="home">
                <h1>{{ profile.display_name }}</h1><p>{{ profile.about }}</p>
                {{ for i in 1..3 }}<a href="/friend">Friend {{ i }}</a>{{ end }}
                <img src="https://example.com/photo.png" alt="Photo">
                <details open><summary>About</summary>Hello</details>
                <div style="color: red" aria-label="Hello">Styled</div>
            </main>
            """), _user);
        var document = new HtmlParser().ParseDocument(html);

        Assert.Equal(_user.DisplayName, document.Title);
        Assert.Equal(_user.DisplayName, document.QuerySelector("h1")!.TextContent);
        Assert.Equal(_user.About, document.QuerySelector("p")!.TextContent);
        Assert.Empty(document.QuerySelectorAll("p img"));
        Assert.Equal(3, document.QuerySelectorAll("a[href='/friend']").Length);
        Assert.NotNull(document.QuerySelector("main.page#home"));
        Assert.NotNull(document.QuerySelector("img[src='https://example.com/photo.png']"));
        Assert.NotNull(document.QuerySelector("details[open]"));
        Assert.Contains("color", document.QuerySelector("div[aria-label]")!.GetAttribute("style"));
    }

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<img src=x onerror=alert(1)><p onclick=alert(1)>hello</p>")]
    [InlineData("<a href='jav&#x61;script:alert(1)'>click</a>")]
    [InlineData("<a href='java&#10;script:alert(1)'>click</a>")]
    [InlineData("<a href='data:text/html,<script>alert(1)</script>'>click</a>")]
    [InlineData("<svg><a xlink:href='javascript:alert(1)'>click</a></svg>")]
    [InlineData("<math><mtext><table><mglyph><style><!--</style><img title='--><img src=x onerror=alert(1)>'>")]
    [InlineData("<iframe srcdoc='<script>alert(1)</script>'></iframe><object data='/x'></object><embed src='/x'>")]
    [InlineData("<base href='https://example.com'><meta http-equiv='refresh' content='0;url=https://example.com'><link rel='stylesheet' href='/x'>")]
    [InlineData("<form action='/api/me'><input name='password'><button>Submit</button></form>")]
    [InlineData("<style>@import 'https://example.com/x.css';</style>")]
    [InlineData("<{{ 'script' }}>alert(1)</{{ 'script' }}>")]
    public void RemovesExecutableMarkupAndDocumentControls(string body)
    {
        var document = new HtmlParser().ParseDocument(_renderer.Render(Page(body), _user));

        Assert.Empty(document.QuerySelectorAll("script, iframe, object, embed, svg, math, base, link, form, input, button"));
        Assert.Empty(document.QuerySelectorAll("meta[http-equiv], body style"));
        Assert.Single(document.QuerySelectorAll("style"));
        foreach (var element in document.All)
        {
            Assert.DoesNotContain(element.Attributes, attribute =>
                attribute.Name.StartsWith("on", StringComparison.OrdinalIgnoreCase));
            foreach (var name in new[] { "href", "src" })
            {
                var value = element.GetAttribute(name) ?? "";
                Assert.DoesNotContain("javascript:", value, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("data:", value, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public void SanitizesUrlsProducedByTemplateExpressions()
    {
        _user.About = "javascript:alert(1)";
        var document = new HtmlParser().ParseDocument(_renderer.Render(
            Page("<a href='{{ profile.about }}'>click</a>"), _user));

        Assert.Null(document.QuerySelector("a")!.GetAttribute("href"));
    }

    [Fact]
    public void SanitizesInlineCss()
    {
        var document = new HtmlParser().ParseDocument(_renderer.Render(
            Page("<p style='color:red; background-image:url(javascript:alert(1))'>Hello</p>"), _user));

        var style = document.QuerySelector("p")!.GetAttribute("style")!;
        Assert.Contains("color", style);
        Assert.DoesNotContain("javascript", style);
    }

    [Theory]
    [InlineData("</style><script>alert(1)</script><style>")]
    [InlineData("</StYlE ><img src=x onerror=alert(1)>")]
    [InlineData("/* </style><meta http-equiv=refresh content=0> */")]
    public void CssCannotEscapeTheStyleElement(string css)
    {
        var page = Page("<main>Safe body</main>");
        page.Css = css;
        var document = new HtmlParser().ParseDocument(_renderer.Render(page, _user));

        Assert.Single(document.QuerySelectorAll("style"));
        Assert.Empty(document.QuerySelectorAll("script, img, meta[http-equiv]"));
        Assert.Equal("Safe body", document.QuerySelector("main")!.TextContent);
        Assert.DoesNotContain("<", document.QuerySelector("style")!.TextContent);
    }

    [Fact]
    public void PreservesModernStylesheetSyntax()
    {
        var page = Page("<main class='page'>Hello</main>");
        page.Css = """
            .page { --accent: red; display: grid; container-type: inline-size; color: var(--accent); }
            @media (max-width: 700px) { .page { grid-template-columns: 1fr; } }
            @container (min-width: 400px) { .page { padding: 2rem; } }
            """;
        var document = new HtmlParser().ParseDocument(_renderer.Render(page, _user));

        Assert.Equal(page.Css, document.QuerySelector("style")!.TextContent);
    }

    [Theory]
    [InlineData("{{ if }}")]
    [InlineData("{{ profile.missing }}")]
    [InlineData("{{ profile.password_hash }}")]
    [InlineData("{{ include '/etc/passwd' }}")]
    [InlineData("{{ object.eval '1 + 1' }}")]
    [InlineData("{{ object.eval_template 'hello' }}")]
    [InlineData("{{ while true }}x{{ end }}")]
    [InlineData("{{ func recurse }}{{ recurse }}{{ end }}{{ recurse }}")]
    public void RejectsInvalidOrExcessiveTemplates(string body)
    {
        Assert.Throws<UserPageRenderException>(() => _renderer.Render(Page(body), _user));
    }

    [Fact]
    public void RejectsDeeplyNestedExpressions()
    {
        var body = "{{ " + new string('(', 100) + "1" + new string(')', 100) + " }}";
        Assert.Throws<UserPageRenderException>(() => _renderer.Render(Page(body), _user));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RejectsOversizedSource(bool oversizedBody)
    {
        var page = Page(oversizedBody ? new string('x', 1_000_001) : "ok");
        page.Css = oversizedBody ? "" : new string('x', 1_000_001);

        Assert.Throws<UserPageRenderException>(() => _renderer.Render(page, _user));
    }

    [Fact]
    public void AcceptsBothSourcesAtTheirLimits()
    {
        var page = Page("<main>" + new string('x', 999_987) + "</main>");
        page.Css = "/*" + new string('x', 999_996) + "*/";

        var document = new HtmlParser().ParseDocument(_renderer.Render(page, _user));

        Assert.Equal(new string('x', 999_987), document.QuerySelector("main")!.TextContent);
        Assert.Equal(page.Css, document.QuerySelector("style")!.TextContent);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AcceptsOutputAtTheLimit(bool expression)
    {
        var document = new HtmlParser().ParseDocument(_renderer.Render(Page(LargeOutputTemplate(expression)), _user));

        Assert.Equal(new string('x', 4_000_000), document.Body!.TextContent.Trim());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RejectsOutputAboveTheLimit(bool expression)
    {
        var error = Assert.Throws<UserPageRenderException>(() =>
            _renderer.Render(Page(LargeOutputTemplate(expression) + "x"), _user));

        Assert.Equal("Rendered page exceeds the size limit.", error.Message);
    }

    private static string LargeOutputTemplate(bool expression)
    {
        var text = new string('x', 4_000);
        var body = expression ? "{{ '" + text + "' }}" : text;
        return "{{ for i in 1..1000 }}" + body + "{{ end }}";
    }

    [Fact]
    public void HonorsCancellation()
    {
        Assert.Throws<UserPageRenderException>(() => _renderer.Render(
            Page("{{ for i in 1..100 }}hello{{ end }}"), _user, new CancellationToken(true)));
    }

    private UserPage Page(string body) => new()
    {
        UserId = _user.Id,
        Owner = _user,
        BodyTemplate = body,
        Css = ""
    };
}
