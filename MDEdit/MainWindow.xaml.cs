using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using MDEdit.ViewModels;
using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xml;

namespace MDEdit;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private MainViewModel? _viewModel;
    private bool _isUpdatingText;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Initialize syntax highlighting
        LoadSyntaxHighlighting();

        // Initialize WebView2
        await PreviewBrowser.EnsureCoreWebView2Async();

        // Wire up the ViewModel
        _viewModel = DataContext as MainViewModel;
        if (_viewModel != null)
        {
            // Subscribe to property changes for live preview
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;

            // Wire up AvalonEdit text changes
            MarkdownEditor.TextChanged += MarkdownEditor_TextChanged;

            // Set initial text
            MarkdownEditor.Text = _viewModel.MarkdownText;

            // Wire up recent files menu
            UpdateRecentFilesMenu();
            _viewModel.RecentFiles.CollectionChanged += (s, args) => UpdateRecentFilesMenu();
        }

        // Setup keyboard shortcuts
        SetupKeyboardShortcuts();
    }

    private void MarkdownEditor_TextChanged(object? sender, EventArgs e)
    {
        if (_viewModel != null && !_isUpdatingText)
        {
            _isUpdatingText = true;
            _viewModel.MarkdownText = MarkdownEditor.Text;
            _isUpdatingText = false;
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.HtmlPreview))
        {
            UpdatePreview();
        }
        else if (e.PropertyName == nameof(MainViewModel.MarkdownText) && !_isUpdatingText)
        {
            _isUpdatingText = true;
            MarkdownEditor.Text = _viewModel?.MarkdownText ?? string.Empty;
            _isUpdatingText = false;
        }
    }

    private void UpdatePreview()
    {
        if (_viewModel != null)
        {
            PreviewBrowser.NavigateToString(_viewModel.HtmlPreview);
        }
    }

    private void UpdateRecentFilesMenu()
    {
        if (_viewModel == null)
            return;

        RecentFilesMenuItem.Items.Clear();

        foreach (var filePath in _viewModel.RecentFiles)
        {
            var menuItem = new MenuItem
            {
                Header = filePath,
                Command = _viewModel.OpenRecentCommand,
                CommandParameter = filePath
            };
            RecentFilesMenuItem.Items.Add(menuItem);
        }
    }

    private void SetupKeyboardShortcuts()
    {
        // Ctrl+O - Open
        InputBindings.Add(new KeyBinding(
            _viewModel!.OpenCommand,
            Key.O,
            ModifierKeys.Control));

        // Ctrl+S - Save
        InputBindings.Add(new KeyBinding(
            _viewModel.SaveCommand,
            Key.S,
            ModifierKeys.Control));

        // Ctrl+Shift+S - Save As
        InputBindings.Add(new KeyBinding(
            _viewModel.SaveAsCommand,
            Key.S,
            ModifierKeys.Control | ModifierKeys.Shift));
    }

    private void LoadSyntaxHighlighting()
    {
        try
        {
            var xshdPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Markdown.xshd");

            if (File.Exists(xshdPath))
            {
                using var reader = new XmlTextReader(xshdPath);
                MarkdownEditor.SyntaxHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
            }
        }
        catch
        {
            // If syntax highlighting fails to load, continue without it
        }
    }
}