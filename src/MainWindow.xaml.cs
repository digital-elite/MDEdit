using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
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
  private string? _initialFilePath;
  private bool _previewShellLoaded;
  private bool _previewUpdating;
  private bool _previewUpdateQueued;
  private const char NEWLINE = '\n';

  private int _previewGeneration;
  private bool _suppressPreviewRefresh;

  private static readonly JsonSerializerOptions PreviewMessageOptions =
    new() { PropertyNameCaseInsensitive = true };

  /// <summary>A message posted by the preview's edit layer.</summary>
  private sealed record PreviewMessage(string? Type, int Generation, int Start, int End, string? Markdown);

  public MainWindow(string? initialFilePath = null)
  {
    _initialFilePath = initialFilePath;
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

      // Hide the context menu and dev tools; this is a preview pane, not a browser
      PreviewWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
      PreviewWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;

      // The shell document is loaded exactly once. Every later update replaces
      // its body in place, which keeps the scroll position and lets the preview
      // hold state of its own.
      PreviewWebView.NavigationStarting += PreviewWebView_NavigationStarting;
      PreviewWebView.NavigationCompleted += PreviewWebView_NavigationCompleted;
      PreviewWebView.WebMessageReceived += PreviewWebView_WebMessageReceived;
      PreviewWebView.NavigateToString(MarkdownService.GetHtmlShell());

      // Open initial file if provided. The preview is refreshed by
      // PreviewWebView_NavigationCompleted once the shell has finished loading.
      if (!string.IsNullOrEmpty(_initialFilePath))
      {
        OpenFile(_initialFilePath);
      }
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

    // A change this window just spliced in on the preview's behalf is already
    // on screen. Re-rendering it would throw away the caret for nothing.
    if ( _suppressPreviewRefresh ) return;

    _previewTimer?.Stop();
    _previewTimer?.Start();
  }

  private void PreviewWebView_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
  {
    // Once the shell is up it must stay up, so clicking a link in the preview
    // may not navigate the pane away. Send it to the default browser instead.
    if ( !_previewShellLoaded ) return;
    if ( e.Uri.StartsWith("about:blank", StringComparison.OrdinalIgnoreCase) ) return;

    e.Cancel = true;

    if ( Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri) &&
         (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) )
    {
      try
      {
        Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
      }
      catch ( Exception )
      {
        // No browser available, or the user cancelled the handler prompt.
      }
    }
  }

  private void PreviewWebView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
  {
    _previewShellLoaded = true;
    UpdatePreview();
  }

  private async void UpdatePreview()
  {
    if ( PreviewWebView.CoreWebView2 == null || !_previewShellLoaded ) return;

    // ExecuteScriptAsync calls can overlap, and applying a stale body after a
    // fresh one would leave the preview behind the editor. Coalesce instead:
    // an update that arrives mid-flight just asks the running one to loop again.
    if ( _previewUpdating )
    {
      _previewUpdateQueued = true;
      return;
    }

    _previewUpdating = true;
    try
    {
      do
      {
        _previewUpdateQueued = false;
        _previewGeneration++;
        string body = MarkdownService.ConvertToHtmlBodyAnnotated(MarkdownEditor.Text);
        string script = "window.mdedit.setContent(" + JsonSerializer.Serialize(body)
          + ", " + _previewGeneration.ToString(CultureInfo.InvariantCulture) + ");";
        await PreviewWebView.CoreWebView2.ExecuteScriptAsync(script);
      }
      while ( _previewUpdateQueued );
    }
    catch ( Exception )
    {
      // A failed preview refresh must never interrupt editing.
    }
    finally
    {
      _previewUpdating = false;
    }
  }

  private void PreviewWebView_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
  {
    PreviewMessage? message;
    try
    {
      message = JsonSerializer.Deserialize<PreviewMessage>(e.TryGetWebMessageAsString(), PreviewMessageOptions);
    }
    catch ( Exception )
    {
      // Not a message this window understands.
      return;
    }

    if ( message is null ) return;

    switch ( message.Type )
    {
      case "edit":
        ApplyPreviewEdit(message);
        break;

      case "resync":
        // Only worth doing while the render it was asked for is still current.
        if ( message.Generation == _previewGeneration ) UpdatePreview();
        break;
    }
  }

  /// <summary>
  /// Replaces the source range an edited preview block was rendered from with
  /// the markdown the block now holds.
  /// </summary>
  private void ApplyPreviewEdit(PreviewMessage message)
  {
    // The offsets describe the document as it looked at a particular render. If
    // anything has re-rendered since, they no longer refer to anything real.
    if ( message.Generation != _previewGeneration ) return;

    string text = MarkdownEditor.Text;
    int start = message.Start;
    int end = message.End;

    if ( start < 0 || end < start || end >= text.Length ) return;

    int length = end - start + 1;
    string replacement = message.Markdown ?? string.Empty;
    string original = text.Substring(start, length);

    if ( string.Equals(original, replacement, StringComparison.Ordinal) ) return;

    // A range that occupied a single line has to stay on one line. The block may
    // be a table cell or a list item, where a newline would end the structure
    // that the surrounding source still expects to be there.
    if ( original.IndexOf(NEWLINE) < 0 && replacement.IndexOf(NEWLINE) >= 0 ) return;

    _suppressPreviewRefresh = true;
    try
    {
      // Grouped as one undo unit, and applied through the selection rather than
      // by assigning Text, so that Ctrl+Z still steps through the edits.
      MarkdownEditor.BeginChange();
      MarkdownEditor.Select(start, length);
      MarkdownEditor.SelectedText = replacement;
      MarkdownEditor.Select(start + replacement.Length, 0);
      MarkdownEditor.EndChange();
    }
    finally
    {
      _suppressPreviewRefresh = false;
    }

    // Emptying a block removes it from the document, which the preview cannot
    // represent by shifting offsets around. Rebuild instead.
    if ( replacement.Length == 0 )
    {
      UpdatePreview();
      return;
    }

    // The source moved, so tell the preview where its blocks live now. This is
    // what keeps the caret alive: the DOM is never rebuilt, only re-measured.
    int newEnd = start + replacement.Length - 1;
    _ = ExecutePreviewScript(
      "window.mdedit.applyEdit("
      + start.ToString(CultureInfo.InvariantCulture) + ", "
      + end.ToString(CultureInfo.InvariantCulture) + ", "
      + newEnd.ToString(CultureInfo.InvariantCulture) + ");");
  }

  private async Task ExecutePreviewScript(string script)
  {
    if ( PreviewWebView.CoreWebView2 == null || !_previewShellLoaded ) return;

    try
    {
      await PreviewWebView.CoreWebView2.ExecuteScriptAsync(script);
    }
    catch ( Exception )
    {
      // A failed preview update must never interrupt editing.
    }
  }

  private bool CheckUnsavedChanges()
  {
    if (!_isModified)
      return true;

    var result = MessageBox.Show(
      "You have unsaved changes. Do you want to discard them?",
      "Unsaved Changes",
      MessageBoxButton.YesNo,
      MessageBoxImage.Warning);

    return result == MessageBoxResult.Yes;
  }

  private bool OpenFile(string filePath)
  {
    if (!File.Exists(filePath))
    {
      MessageBox.Show(
        $"File not found:\n{filePath}",
        "Error",
        MessageBoxButton.OK,
        MessageBoxImage.Error);
      return false;
    }

    if (!CheckUnsavedChanges())
      return false;

    try
    {
      string content = File.ReadAllText(filePath);
      _currentFilePath = filePath;
      MarkdownEditor.Text = content;
      _isModified = false;
      Title = $"MDEdit - {Path.GetFileName(filePath)}";
      UpdatePreview();
      return true;
    }
    catch (Exception ex)
    {
      MessageBox.Show(
        $"Failed to open file:\n{ex.Message}",
        "Error",
        MessageBoxButton.OK,
        MessageBoxImage.Error);
      return false;
    }
  }

  private void Window_DragOver(object sender, DragEventArgs e)
  {
    if (e.Data.GetDataPresent(DataFormats.FileDrop))
    {
      var files = (string[])e.Data.GetData(DataFormats.FileDrop);
      if (files.Any(f => f.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
                         f.EndsWith(".markdown", StringComparison.OrdinalIgnoreCase)))
      {
        e.Effects = DragDropEffects.Copy;
      }
      else
      {
        e.Effects = DragDropEffects.None;
      }
    }
    else
    {
      e.Effects = DragDropEffects.None;
    }
    e.Handled = true;
  }

  private void Window_Drop(object sender, DragEventArgs e)
  {
    try
    {
      if (e.Data.GetDataPresent(DataFormats.FileDrop))
      {
        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        var mdFile = files.FirstOrDefault(f =>
          f.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
          f.EndsWith(".markdown", StringComparison.OrdinalIgnoreCase));

        if (mdFile != null)
        {
          OpenFile(mdFile);
        }
      }
    }
    catch (Exception ex)
    {
      MessageBox.Show(
        $"Failed to open dropped file:\n{ex.Message}",
        "Error",
        MessageBoxButton.OK,
        MessageBoxImage.Error);
    }
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
      OpenFile(dialog.FileName);
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
