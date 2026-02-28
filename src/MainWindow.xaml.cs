using System.IO;
using System.Windows;
using System.Windows.Threading;
using MDEdit.Services;
using Microsoft.Win32;

namespace MDEdit;

public partial class MainWindow : Window
{
    private string? _currentFilePath;
    private DispatcherTimer? _previewTimer;
    private bool _isModified;

    public MainWindow()
    {
        InitializeComponent();
        InitializeWebView();
        SetupPreviewTimer();

        // Set initial sample markdown
        MarkdownEditor.Text = @"# Welcome to MDEdit

This is a **Markdown** editor with *live preview*.

## Features

- Split view with source on the left
- Rendered preview on the right
- Export to DOCX

### Code Example

```csharp
Console.WriteLine(""Hello, World!"");
```

> This is a blockquote

1. First item
2. Second item
3. Third item
";
    }

    private async void InitializeWebView()
    {
        await PreviewWebView.EnsureCoreWebView2Async();
        UpdatePreview();
    }

    private void SetupPreviewTimer()
    {
        _previewTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        _previewTimer.Tick += (s, e) =>
        {
            _previewTimer.Stop();
            UpdatePreview();
        };
    }

    private void MarkdownEditor_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        _isModified = true;
        _previewTimer?.Stop();
        _previewTimer?.Start();
    }

    private void UpdatePreview()
    {
        if (PreviewWebView.CoreWebView2 == null) return;

        string markdown = MarkdownEditor.Text;
        string html = MarkdownService.ConvertToHtml(markdown);
        PreviewWebView.NavigateToString(html);
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Markdown files (*.md)|*.md|All files (*.*)|*.*",
            Title = "Open Markdown File"
        };

        if (dialog.ShowDialog() == true)
        {
            _currentFilePath = dialog.FileName;
            MarkdownEditor.Text = File.ReadAllText(_currentFilePath);
            _isModified = false;
            Title = $"MDEdit - {Path.GetFileName(_currentFilePath)}";
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentFilePath))
        {
            SaveAs_Click(sender, e);
            return;
        }

        File.WriteAllText(_currentFilePath, MarkdownEditor.Text);
        _isModified = false;
    }

    private void SaveAs_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Markdown files (*.md)|*.md|All files (*.*)|*.*",
            Title = "Save Markdown File",
            DefaultExt = ".md"
        };

        if (dialog.ShowDialog() == true)
        {
            _currentFilePath = dialog.FileName;
            File.WriteAllText(_currentFilePath, MarkdownEditor.Text);
            _isModified = false;
            Title = $"MDEdit - {Path.GetFileName(_currentFilePath)}";
        }
    }

    private void ExportDocx_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Word Document (*.docx)|*.docx",
            Title = "Export to DOCX",
            DefaultExt = ".docx"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                DocxExportService.Export(MarkdownEditor.Text, dialog.FileName);
                MessageBox.Show($"Successfully exported to:\n{dialog.FileName}", "Export Complete",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
