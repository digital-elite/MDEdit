# MDEdit

A lightweight Markdown editor for Windows with live preview and DOCX export support.

## Features

- **Split View Editor**: Markdown source on the left, rendered preview on the right
- **Live Preview**: Real-time HTML preview as you type
- **DOCX Export**: Export your markdown documents to Microsoft Word format
- **Command-Line Support**: Batch convert markdown files to DOCX

## Screenshot

```
┌─────────────────────────────────────────────────────────────────┐
│ File                                                            │
├────────────────────────────┬────────────────────────────────────┤
│ # Welcome to MDEdit        │  Welcome to MDEdit                 │
│                            │                                    │
│ This is **bold** and       │  This is bold and italic text.     │
│ *italic* text.             │                                    │
│                            │  • List item 1                     │
│ - List item 1              │  • List item 2                     │
│ - List item 2              │                                    │
└────────────────────────────┴────────────────────────────────────┘
```

## Requirements

- Windows 10/11 (x64)
- .NET 8.0 Runtime
- WebView2 Runtime (usually pre-installed on Windows 10/11)

## Installation

### From Installer

1. Download `MDEdit.Installer.msi` from the releases
2. Run the installer
3. Launch MDEdit from the Start Menu

### From Source

```bash
# Clone the repository
git clone https://github.com/yourusername/MDEdit.git
cd MDEdit

# Build the application
dotnet build -c Release

# Run the application
dotnet run --project src/MDEdit.csproj
```

## Usage

### GUI Mode

Simply launch `MDEdit.exe` to open the editor. Use the File menu to:

- **Open** (Ctrl+O): Open a markdown file
- **Save** (Ctrl+S): Save the current file
- **Save As**: Save to a new location
- **Export to DOCX**: Convert to Word document

### Command-Line Mode

```bash
# Export markdown to DOCX
MDEdit.exe --export input.md output.docx

# Show help
MDEdit.exe --help
```

## Building the Installer

The project uses WiX Toolset v5 for creating the MSI installer.

```bash
# Build both the application and installer
dotnet build -c Release

# The MSI will be at: installer/bin/Release/MDEdit.Installer.msi
```

## Project Structure

```
MDEdit/
├── MDEdit.slnx                      # Solution file
├── src/
│   ├── MDEdit.csproj                # Main application project
│   ├── Program.cs                   # Entry point with CLI handling
│   ├── MainWindow.xaml              # WPF UI layout
│   ├── MainWindow.xaml.cs           # UI logic
│   └── Services/
│       ├── MarkdownService.cs       # Markdown to HTML conversion
│       └── DocxExportService.cs     # Markdown to DOCX export
└── installer/
    ├── MDEdit.Installer.wixproj     # WiX installer project
    └── Package.wxs                  # Installer definition
```

## Dependencies

- [Markdig](https://github.com/xoofx/markdig) - Markdown parsing and HTML conversion
- [DocumentFormat.OpenXml](https://github.com/dotnet/Open-XML-SDK) - DOCX generation
- [Microsoft.Web.WebView2](https://developer.microsoft.com/en-us/microsoft-edge/webview2/) - HTML preview rendering

## License

MIT License
