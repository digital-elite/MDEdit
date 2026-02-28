# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build Commands

```bash
# Full build and publish (cleans, builds, publishes self-contained exe + MSI)
build.cmd

# Build the application only
dotnet build -c Release

# Run the application (GUI mode)
dotnet run --project src/MDEdit.csproj

# Publish self-contained executable
dotnet publish src/MDEdit.csproj -c Release -r win-x64 --self-contained -o publish
```

## Architecture

MDEdit is a Windows WPF desktop application (.NET 8.0, C# 12) providing a Markdown editor with live preview and DOCX export.

### Entry Point & Execution Modes

`Program.cs` handles both CLI and GUI modes:
- `--export <input.md> <output.docx>` - Command-line batch conversion
- `--help` / `-h` - Help display
- Default - Launches WPF GUI via `MainWindow`

### Core Components

**UI Layer (`MainWindow.xaml/.cs`)**
- Split view: TextBox (left) for Markdown editing, WebView2 (right) for HTML preview
- 300ms debounce timer prevents excessive preview updates during typing
- Standard Windows file dialogs for I/O operations

**Service Layer (stateless utility classes)**
- `MarkdownService.cs` - Converts Markdown to styled HTML using Markdig with advanced extensions
- `DocxExportService.cs` - Converts Markdown to DOCX using OpenXml SDK, traversing Markdig AST

### Data Flow

1. User types in TextBox → TextChanged event
2. 300ms debounce timer triggers
3. MarkdownService converts Markdown → HTML
4. WebView2 renders HTML preview

### Key Dependencies

- **Markdig** - Markdown parsing and HTML conversion
- **DocumentFormat.OpenXml** - DOCX file generation
- **Microsoft.Web.WebView2** - HTML preview rendering (Edge-based)
- **WiX Toolset v5** - MSI installer creation

## Target Platform

Windows 10/11 (x64) only. Requires .NET 8.0 Runtime and WebView2 Runtime.
