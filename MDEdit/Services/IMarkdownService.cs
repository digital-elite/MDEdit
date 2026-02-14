namespace MDEdit.Services;

/// <summary>
/// Service for markdown processing
/// </summary>
public interface IMarkdownService
{
    /// <summary>
    /// Converts markdown text to HTML
    /// </summary>
    /// <param name="markdown">The markdown text</param>
    /// <returns>The rendered HTML</returns>
    string ConvertToHtml(string markdown);
}
