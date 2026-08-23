using System.Globalization;
using System.IO;
using System.Text;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Renderers.Html;
using Markdig.Syntax;

namespace MDEdit.Services;

public static class MarkdownService
{
    /// <summary>
    /// Attributes carrying the character range in the markdown source that an
    /// editable element was rendered from. Both offsets are inclusive.
    /// </summary>
    public const string SourceStartAttribute = "data-md-start";

    public const string SourceEndAttribute = "data-md-end";

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UsePreciseSourceLocation()
        .Build();

    /// <summary>
    /// Converts markdown to a complete, standalone HTML document.
    /// </summary>
    public static string ConvertToHtml(string markdown)
    {
        return WrapInHtmlTemplate(ConvertToHtmlBody(markdown));
    }

    /// <summary>
    /// Converts markdown to an HTML fragment only, without the surrounding
    /// document or styles. Used to patch the body of an already loaded preview.
    /// </summary>
    public static string ConvertToHtmlBody(string markdown)
    {
        return Markdown.ToHtml(markdown, Pipeline);
    }

    /// <summary>
    /// Converts markdown to an HTML fragment in which every element the user is
    /// allowed to edit in the preview carries the source range it came from.
    /// Elements without those attributes are read-only by definition.
    /// </summary>
    public static string ConvertToHtmlBodyAnnotated(string markdown)
    {
        var document = Markdown.Parse(markdown, Pipeline);
        Annotate(document, markdown);
        return Markdown.ToHtml(document, Pipeline);
    }

    /// <summary>
    /// Returns the styled but empty HTML document that the preview loads once.
    /// Its body is then replaced in place on every update.
    /// </summary>
    public static string GetHtmlShell()
    {
        return WrapInHtmlTemplate(string.Empty, includePreviewScript: true);
    }

    /// <summary>
    /// The preview's own script, loaded from the embedded resource on first
    /// use. It belongs to the shell alone: a standalone HTML document produced
    /// by <see cref="ConvertToHtml"/> has no editor to drive.
    /// </summary>
    private static readonly string[] PreviewScriptResources =
    {
        // The converter first: the edit layer builds on it.
        "MDEdit.Preview.inline-markdown.js",
        "MDEdit.Preview.editor.js"
    };

    private static readonly Lazy<string> PreviewScript = new(() =>
    {
        var builder = new StringBuilder();

        foreach ( string resource in PreviewScriptResources )
        {
            using var stream = typeof(MarkdownService).Assembly.GetManifestResourceStream(resource)
                ?? throw new InvalidOperationException($"Embedded resource '{resource}' was not found.");
            using var reader = new StreamReader(stream);
            builder.AppendLine(reader.ReadToEnd());
        }

        return builder.ToString();
    });

    /// <summary>
    /// Tags the editable blocks with their source range. Only leaf blocks whose
    /// content is purely inline qualify: a splice replaces inline markdown and
    /// nothing else, so anything with structure of its own (code fences, raw
    /// HTML, thematic breaks) is deliberately left unannotated.
    /// </summary>
    private static void Annotate(MarkdownDocument document, string markdown)
    {
        foreach ( var block in document.Descendants<Block>() )
        {
            if ( block is HeadingBlock heading )
            {
                // A setext heading's source range spans its underline as well,
                // so editing one means regenerating the underline. Out of scope.
                if ( heading.HeaderChar != '#' ) continue;
            }
            else if ( block is not ParagraphBlock )
            {
                continue;
            }

            if ( !TryGetContentSpan(block, markdown, out int start, out int end) ) continue;

            var target = ResolveAttributeTarget(block);
            var attributes = target.GetAttributes();
            attributes.AddProperty(SourceStartAttribute, start.ToString(CultureInfo.InvariantCulture));
            attributes.AddProperty(SourceEndAttribute, end.ToString(CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    /// Returns the object that will actually become an element in the rendered
    /// HTML. Markdig drops the &lt;p&gt; wrapper for tight list items and for
    /// table cells, so a paragraph in either position never becomes an element
    /// of its own and its attributes would be silently discarded. Hoisting the
    /// span onto the ancestor that does get rendered keeps it addressable, and
    /// the span still describes only the paragraph's own content.
    /// </summary>
    private static MarkdownObject ResolveAttributeTarget(Block block)
    {
        if ( block is not ParagraphBlock ) return block;

        if ( block.Parent is TableCell cell ) return cell;

        if ( block.Parent is ListItemBlock item && item.Parent is ListBlock { IsLoose: false } )
            return item;

        return block;
    }

    /// <summary>
    /// Resolves the source range covering a block's inline content only. The
    /// block's own span is unusable for splicing because it swallows the
    /// markers that define the block: "# " on a heading, and the padding
    /// spaces inside a table cell.
    /// </summary>
    private static bool TryGetContentSpan(Block block, string markdown, out int start, out int end)
    {
        var span = block is LeafBlock { Inline: not null } leaf && !leaf.Inline.Span.IsEmpty
            ? leaf.Inline.Span
            : block.Span;

        start = span.Start;
        end = span.End;

        // Several container blocks report an empty or unset span; splicing over
        // one of those would corrupt the document.
        if ( start < 0 || end < start || end >= markdown.Length ) return false;

        while ( start <= end && char.IsWhiteSpace(markdown[start]) ) start++;
        while ( end >= start && char.IsWhiteSpace(markdown[end]) ) end--;

        if ( end < start ) return false;

        // Inside a container block, a span that runs across lines also covers
        // that container's continuation markers: the indent of a wrapped list
        // item, or the '>' opening the second line of a quote. Those markers
        // are structure, not content, and the preview would hand them back as
        // plain text, so such a block stays read-only.
        if ( block.Parent is not MarkdownDocument &&
             markdown.AsSpan(start, end - start + 1).IndexOf('\n') >= 0 )
        {
            return false;
        }

        return true;
    }

    private static string WrapInHtmlTemplate(string body, bool includePreviewScript = false)
    {
        return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <style>
        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, sans-serif;
            line-height: 1.6;
            padding: 20px;
            max-width: 900px;
            margin: 0 auto;
            color: #333;
        }}
        h1, h2, h3, h4, h5, h6 {{
            margin-top: 1.5em;
            margin-bottom: 0.5em;
            color: #111;
        }}
        h1 {{ font-size: 2em; border-bottom: 1px solid #eee; padding-bottom: 0.3em; }}
        h2 {{ font-size: 1.5em; border-bottom: 1px solid #eee; padding-bottom: 0.3em; }}
        h3 {{ font-size: 1.25em; }}
        code {{
            background-color: #f4f4f4;
            padding: 0.2em 0.4em;
            border-radius: 3px;
            font-family: Consolas, Monaco, 'Courier New', monospace;
            font-size: 0.9em;
        }}
        pre {{
            background-color: #f4f4f4;
            padding: 16px;
            border-radius: 6px;
            overflow-x: auto;
        }}
        pre code {{
            background-color: transparent;
            padding: 0;
        }}
        blockquote {{
            border-left: 4px solid #ddd;
            margin: 0;
            padding-left: 16px;
            color: #666;
        }}
        ul, ol {{
            padding-left: 2em;
        }}
        li {{
            margin: 0.25em 0;
        }}
        table {{
            border-collapse: collapse;
            width: 100%;
            margin: 1em 0;
        }}
        th, td {{
            border: 1px solid #ddd;
            padding: 8px 12px;
            text-align: left;
        }}
        th {{
            background-color: #f4f4f4;
        }}
        a {{
            color: #0366d6;
            text-decoration: none;
        }}
        a:hover {{
            text-decoration: underline;
        }}
        img {{
            max-width: 100%;
        }}
        [data-md-start]:focus {{
            outline: 2px solid #0366d6;
            outline-offset: 3px;
            border-radius: 2px;
        }}
        hr {{
            border: none;
            border-top: 1px solid #eee;
            margin: 2em 0;
        }}
    </style>
</head>
<body>
{body}
</body>
{(includePreviewScript ? $"<script>{PreviewScript.Value}</script>" : string.Empty)}
</html>";
    }
}
