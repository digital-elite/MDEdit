using Markdig;

namespace MDEdit.Services;

/// <summary>
/// Implementation of markdown processing service using Markdig
/// </summary>
public class MarkdownService : IMarkdownService
{
    private readonly MarkdownPipeline _pipeline;

    public MarkdownService()
    {
        // Configure the markdown pipeline with advanced features
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();
    }

    public string ConvertToHtml(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
            return string.Empty;

        var html = Markdown.ToHtml(markdown, _pipeline);

        // Wrap in a complete HTML document with styling
        return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;
            line-height: 1.6;
            padding: 20px;
            max-width: 900px;
            margin: 0 auto;
            color: #333;
        }}
        code {{
            background-color: #f4f4f4;
            padding: 2px 6px;
            border-radius: 3px;
            font-family: 'Courier New', monospace;
        }}
        pre {{
            background-color: #f4f4f4;
            padding: 12px;
            border-radius: 5px;
            overflow-x: auto;
        }}
        pre code {{
            background-color: transparent;
            padding: 0;
        }}
        blockquote {{
            border-left: 4px solid #ddd;
            padding-left: 16px;
            margin-left: 0;
            color: #666;
        }}
        table {{
            border-collapse: collapse;
            width: 100%;
            margin: 16px 0;
        }}
        th, td {{
            border: 1px solid #ddd;
            padding: 8px;
            text-align: left;
        }}
        th {{
            background-color: #f4f4f4;
        }}
        img {{
            max-width: 100%;
            height: auto;
        }}
        h1, h2, h3, h4, h5, h6 {{
            margin-top: 24px;
            margin-bottom: 16px;
        }}
    </style>
</head>
<body>
{html}
</body>
</html>";
    }
}
