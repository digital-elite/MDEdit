using Microsoft.Win32;
using System.IO;

namespace MDEdit.Services;

/// <summary>
/// Implementation of file operations service
/// </summary>
public class FileService : IFileService
{
    private const string MarkdownFilter = "Markdown Files (*.md)|*.md|All Files (*.*)|*.*";

    public string? OpenFile()
    {
        var dialog = new OpenFileDialog
        {
            Filter = MarkdownFilter,
            Title = "Open Markdown File"
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? SaveFileAs()
    {
        var dialog = new SaveFileDialog
        {
            Filter = MarkdownFilter,
            Title = "Save Markdown File",
            DefaultExt = ".md"
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string ReadFile(string filePath)
    {
        return File.ReadAllText(filePath);
    }

    public void WriteFile(string filePath, string content)
    {
        File.WriteAllText(filePath, content);
    }
}
