using MDEdit.Commands;
using MDEdit.Infrastructure;
using MDEdit.Services;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace MDEdit.ViewModels;

/// <summary>
/// ViewModel for the main window
/// </summary>
public class MainViewModel : ViewModelBase
{
    private readonly IFileService _fileService;
    private readonly IMarkdownService _markdownService;
    private readonly IRecentFilesService _recentFilesService;

    private string _markdownText = string.Empty;
    private string _htmlPreview = string.Empty;
    private string? _currentFilePath;
    private bool _isDirty;
    private string _title = "MDEdit - Untitled";

    public MainViewModel(
        IFileService fileService,
        IMarkdownService markdownService,
        IRecentFilesService recentFilesService)
    {
        _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
        _markdownService = markdownService ?? throw new ArgumentNullException(nameof(markdownService));
        _recentFilesService = recentFilesService ?? throw new ArgumentNullException(nameof(recentFilesService));

        // Initialize commands
        OpenCommand = new RelayCommand(OnOpen);
        SaveCommand = new RelayCommand(OnSave, CanSave);
        SaveAsCommand = new RelayCommand(OnSaveAs);
        CloseCommand = new RelayCommand(OnClose, CanClose);
        ExitCommand = new RelayCommand(OnExit);
        OpenRecentCommand = new RelayCommand<string>(OnOpenRecent);

        // Initialize recent files
        UpdateRecentFiles();
    }

    #region Properties

    public string MarkdownText
    {
        get => _markdownText;
        set
        {
            if (SetProperty(ref _markdownText, value))
            {
                IsDirty = true;
                UpdatePreview();
            }
        }
    }

    public string HtmlPreview
    {
        get => _htmlPreview;
        private set => SetProperty(ref _htmlPreview, value);
    }

    public string? CurrentFilePath
    {
        get => _currentFilePath;
        private set
        {
            if (SetProperty(ref _currentFilePath, value))
            {
                UpdateTitle();
            }
        }
    }

    public bool IsDirty
    {
        get => _isDirty;
        private set
        {
            if (SetProperty(ref _isDirty, value))
            {
                UpdateTitle();
            }
        }
    }

    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    public ObservableCollection<string> RecentFiles { get; } = new();

    #endregion

    #region Commands

    public ICommand OpenCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand SaveAsCommand { get; }
    public ICommand CloseCommand { get; }
    public ICommand ExitCommand { get; }
    public ICommand OpenRecentCommand { get; }

    #endregion

    #region Command Handlers

    private void OnOpen()
    {
        if (!ConfirmDiscardChanges())
            return;

        var filePath = _fileService.OpenFile();
        if (filePath != null)
        {
            OpenFile(filePath);
        }
    }

    private bool CanSave() => IsDirty && !string.IsNullOrEmpty(CurrentFilePath);

    private void OnSave()
    {
        if (string.IsNullOrEmpty(CurrentFilePath))
        {
            OnSaveAs();
            return;
        }

        SaveFile(CurrentFilePath);
    }

    private void OnSaveAs()
    {
        var filePath = _fileService.SaveFileAs();
        if (filePath != null)
        {
            SaveFile(filePath);
        }
    }

    private bool CanClose() => !string.IsNullOrEmpty(CurrentFilePath);

    private void OnClose()
    {
        if (!ConfirmDiscardChanges())
            return;

        CloseFile();
    }

    private void OnExit()
    {
        if (!ConfirmDiscardChanges())
            return;

        Application.Current.Shutdown();
    }

    private void OnOpenRecent(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return;

        if (!File.Exists(filePath))
        {
            MessageBox.Show(
                $"File not found: {filePath}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        if (!ConfirmDiscardChanges())
            return;

        OpenFile(filePath);
    }

    #endregion

    #region Private Methods

    private void OpenFile(string filePath)
    {
        try
        {
            var content = _fileService.ReadFile(filePath);
            MarkdownText = content;
            CurrentFilePath = filePath;
            IsDirty = false;
            _recentFilesService.AddRecentFile(filePath);
            UpdateRecentFiles();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to open file: {ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void SaveFile(string filePath)
    {
        try
        {
            _fileService.WriteFile(filePath, MarkdownText);
            CurrentFilePath = filePath;
            IsDirty = false;
            _recentFilesService.AddRecentFile(filePath);
            UpdateRecentFiles();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to save file: {ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void CloseFile()
    {
        MarkdownText = string.Empty;
        CurrentFilePath = null;
        IsDirty = false;
    }

    private bool ConfirmDiscardChanges()
    {
        if (!IsDirty)
            return true;

        var result = MessageBox.Show(
            "You have unsaved changes. Do you want to save them?",
            "Unsaved Changes",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning);

        switch (result)
        {
            case MessageBoxResult.Yes:
                OnSave();
                return !IsDirty; // Return true only if save was successful
            case MessageBoxResult.No:
                return true;
            case MessageBoxResult.Cancel:
            default:
                return false;
        }
    }

    private void UpdatePreview()
    {
        HtmlPreview = _markdownService.ConvertToHtml(MarkdownText);
    }

    private void UpdateTitle()
    {
        var fileName = string.IsNullOrEmpty(CurrentFilePath)
            ? "Untitled"
            : Path.GetFileName(CurrentFilePath);

        Title = $"MDEdit - {fileName}{(IsDirty ? "*" : "")}";
    }

    private void UpdateRecentFiles()
    {
        RecentFiles.Clear();
        foreach (var file in _recentFilesService.RecentFiles)
        {
            RecentFiles.Add(file);
        }
    }

    #endregion
}
