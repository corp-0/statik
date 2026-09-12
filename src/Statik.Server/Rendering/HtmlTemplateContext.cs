using System.Text.Encodings.Web;
using Scriban;
using Scriban.Parsing;

namespace Statik.Server.Rendering;

public class HtmlTemplateContext : TemplateContext
{
    public override TemplateContext Write(SourceSpan span, object? value)
    {
        var text = ObjectToString(value);

        if (text is not null)
        {
            // Literal template HTML uses a different overload.
            base.Write(HtmlEncoder.Default.Encode(text));
        }

        return this;
    }
}
