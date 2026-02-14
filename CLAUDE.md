# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

MDEdit is a WPF markdown editor and viewer built with .NET 8.0. The application features a split-view interface with raw markdown editing on the left and live HTML preview on the right.

## Build and Run Commands

```bash
# Build the solution
dotnet build MDEdit.slnx

# Build the specific project
dotnet build MDEdit/MDEdit.csproj

# Run the application
dotnet run --project MDEdit/MDEdit.csproj

# Clean build artifacts
dotnet clean MDEdit/MDEdit.csproj
```

## Architecture

### MVVM with Dependency Injection

The application follows **MVVM (Model-View-ViewModel)** pattern with **Dependency Injection** configured in `App.xaml.cs:16-39`. The DI container is set up in `OnStartup` and all services are registered as singletons, with ViewModels as transient.

**Critical**: MainWindow's DataContext is injected via the service provider, not set in XAML. The App.xaml has `StartupUri` removed to allow manual window creation with DI.

### Service Layer Architecture

All services follow the **Interface Segregation Principle** with interface/implementation pairs:

- **IFileService/FileService**: File I/O operations and dialog management
- **IMarkdownService/MarkdownService**: Markdown-to-HTML conversion using Markdig with `UseAdvancedExtensions()`
- **IRecentFilesService/RecentFilesService**: Recent file tracking with JSON persistence to `%AppData%/MDEdit/recent-files.json`

Services are registered in `App.xaml.cs:24-26`.

### ViewModel to View Binding

**MainViewModel** (`ViewModels/MainViewModel.cs`) is the single ViewModel, managing:
- File operations through `ICommand` properties (OpenCommand, SaveCommand, etc.)
- Two-way data flow between MarkdownText and HtmlPreview
- `IsDirty` tracking for unsaved changes
- Recent files via `ObservableCollection<string>`

**MainWindow.xaml.cs** bridges the ViewModel and UI components:
- `MarkdownEditor` (AvalonEdit) text changes sync to `ViewModel.MarkdownText` via `MarkdownEditor_TextChanged:50-58`
- `ViewModel.HtmlPreview` changes update WebView2 via `ViewModel_PropertyChanged:60-72`
- `_isUpdatingText` flag prevents circular updates between view and viewmodel

### Live Preview Implementation

The preview update flow:
1. User types in AvalonEdit → `MarkdownEditor_TextChanged` fires
2. Updates `MainViewModel.MarkdownText` property
3. `MarkdownText` setter calls `UpdatePreview()` (line 58 in MainViewModel.cs)
4. `UpdatePreview()` calls `_markdownService.ConvertToHtml()`
5. Sets `HtmlPreview` property, firing `PropertyChanged`
6. `MainWindow` catches PropertyChanged → calls `PreviewBrowser.NavigateToString()` with rendered HTML

This creates real-time preview as users type.

### Syntax Highlighting

AvalonEdit syntax highlighting is loaded from `Resources/Markdown.xshd` (XSHD format). The file must be copied to output directory (configured in `MDEdit.csproj:19-23`). Loading happens in `MainWindow.LoadSyntaxHighlighting:122-133` during window initialization.

If the XSHD file is missing, the app continues without syntax highlighting (silent failure).

### Application Startup Flow

1. `App.OnStartup` creates DI container
2. Registers all services and MainViewModel
3. Manually creates MainWindow and injects MainViewModel as DataContext
4. `MainWindow_Loaded` event:
   - Loads syntax highlighting from Resources/Markdown.xshd
   - Initializes WebView2 control (async)
   - Wires up event handlers for text sync and preview updates
   - Populates recent files menu
   - Sets up keyboard shortcuts (Ctrl+O, Ctrl+S, Ctrl+Shift+S)

## Key Components

### Infrastructure

- **ViewModelBase** (`Infrastructure/ViewModelBase.cs`): Base class implementing `INotifyPropertyChanged` with `SetProperty<T>` helper
- **RelayCommand** (`Commands/RelayCommand.cs`): Generic and non-generic ICommand implementations for MVVM command binding

### Resources

- **Markdown.xshd**: XML-based syntax highlighting definition for AvalonEdit. Uses regex rules for headings, emphasis, code blocks, links, images, and lists.

## Important Implementation Details

### File Operation Flow

When opening/saving files:
1. File dialogs use `OpenFileDialog`/`SaveFileDialog` from `IFileService`
2. Filter is set to `*.md` files
3. On successful save/open, file path is added to recent files via `IRecentFilesService.AddRecentFile()`
4. Recent files are limited to 10 entries (defined in `RecentFilesService:12`)
5. Non-existent files are filtered out when loading recent files

### Dirty State Management

- `IsDirty` flag tracks unsaved changes
- Set to `true` when `MarkdownText` changes
- Set to `false` after successful save or when closing file
- `ConfirmDiscardChanges()` prompts user on File > Close or Exit with unsaved work
- Save command is disabled (`CanSave()`) when not dirty or no current file path

### Recent Files Menu

Recent files menu is dynamically populated in `MainWindow.UpdateRecentFilesMenu:82-99`. Menu items are created programmatically with `OpenRecentCommand` binding. The menu visibility is controlled by XAML DataTriggers based on `RecentFiles.Count`.

## Dependencies

- **Markdig 0.44.0**: Markdown processing with advanced extensions enabled
- **Microsoft.Web.WebView2**: Embedded Chromium for HTML preview
- **AvalonEdit 6.3.1.120**: Text editor component with syntax highlighting
- **Microsoft.Extensions.DependencyInjection 10.0.2**: DI container

## Adding New Services

When adding a new service:
1. Create interface in `Services/I{ServiceName}.cs`
2. Create implementation in `Services/{ServiceName}.cs`
3. Register in `App.xaml.cs` OnStartup method
4. Inject via constructor in ViewModels or other services
