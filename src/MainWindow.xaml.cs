using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using MDEdit.Services;
using Microsoft.Win32;
using Microsoft.Web.WebView2.Core;

namespace MDEdit;

public partial class MainWindow : Window
{
  private string? _currentFilePath;
  private DispatcherTimer? _previewTimer;
  private bool _isModified;

  public MainWindow()
  {
    InitializeComponent();
    SetupPreviewTimer();
    Loaded += MainWindow_Loaded;

    // Set initial sample markdown
    MarkdownEditor.Text = "";/* @"# Welcome to MDEdit

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

## Table

| Name | Description |
|---|---|
| Apple | Fruit growing on trees |
| Orange | Orange fruit growing on trees with citrus smell |


";*/
  }

  private void MainWindow_Loaded(object sender, RoutedEventArgs e)
  {
    InitializeWebView();
  }

  private async void InitializeWebView()
  {
    try
    {
      // Set user data folder to AppData\Local to avoid permission issues
      string userDataFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MDEdit",
        "WebView2");

      // Ensure the directory exists
      Directory.CreateDirectory(userDataFolder);

      // Create environment with custom user data folder
      var environment = await CoreWebView2Environment.CreateAsync(
        browserExecutableFolder: null,
        userDataFolder: userDataFolder);

      // Initialize WebView2 with the custom environment
      await PreviewWebView.EnsureCoreWebView2Async(environment);
      UpdatePreview();
    }
    catch ( System.Runtime.InteropServices.COMException ex ) when ( ex.HResult == unchecked((int)0x80080005) )
    {
      MessageBox.Show(
        "Failed to initialize the preview panel.\n\n" +
        "WebView2 Runtime may not be installed or is corrupted.\n\n" +
        "Please download and install WebView2 Runtime from:\n" +
        "https://developer.microsoft.com/microsoft-edge/webview2/\n\n" +
        "The editor will continue without live preview.",
        "WebView2 Initialization Failed",
        MessageBoxButton.OK,
        MessageBoxImage.Warning);
    }
    catch ( Exception ex )
    {
      MessageBox.Show(
        $"Failed to initialize preview: {ex.Message}\n\n" +
        "The editor will continue without live preview.",
        "Preview Initialization Error",
        MessageBoxButton.OK,
        MessageBoxImage.Warning);
    }
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
    if ( PreviewWebView.CoreWebView2 == null ) return;

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

    if ( dialog.ShowDialog() == true )
    {
      _currentFilePath = dialog.FileName;
      MarkdownEditor.Text = File.ReadAllText(_currentFilePath);
      _isModified = false;
      Title = $"MDEdit - {Path.GetFileName(_currentFilePath)}";
    }
  }

  private void Save_Click(object sender, RoutedEventArgs e)
  {
    if ( string.IsNullOrEmpty(_currentFilePath) )
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

    if ( dialog.ShowDialog() == true )
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

    if ( dialog.ShowDialog() == true )
    {
      try
      {
        DocxExportService.Export(MarkdownEditor.Text, dialog.FileName);
        MessageBox.Show($"Successfully exported to:\n{dialog.FileName}", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
      }
      catch ( Exception ex )
      {
        MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
      }
    }
  }

  private void Exit_Click(object sender, RoutedEventArgs e)
  {
    if ( _isModified )
      if ( MessageBox.Show("Are you sure you want to leave without saving?", "Unsaved Changes", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes )
        Close();
  }

  private void About_Click(object sender, RoutedEventArgs e)
  {
    var version = Assembly.GetExecutingAssembly().GetName().Version;
    string versionString = version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "Unknown";

    MessageBox.Show(
      $"MDEdit - Markdown Editor\n\nVersion {versionString}\n\nA simple Markdown editor with live preview and DOCX export.",
      "About MDEdit",
      MessageBoxButton.OK,
      MessageBoxImage.Information);
  }
}
