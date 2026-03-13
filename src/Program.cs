using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using MDEdit.Services;

namespace MDEdit;

public class Program
{
  [DllImport("kernel32.dll")]
  private static extern bool AttachConsole(int dwProcessId);

  private const int ATTACH_PARENT_PROCESS = -1;

  [STAThread]
  public static int Main(string[] args)
  {
    if ( args.Length >= 3 && (args[0] == "--export" || args[0] == "--e") )
    {
      AttachConsole(ATTACH_PARENT_PROCESS);
      return RunExport(args[1], args[2]);
    }

    if ( args.Length > 0 && (args[0] == "--help" || args[0] == "-h") )
    {
      AttachConsole(ATTACH_PARENT_PROCESS);
      PrintHelp();
      return 0;
    }

    // No arguments or unrecognized - launch GUI
    var app = new Application();
    app.Run(new MainWindow());
    return 0;
  }

  private static int RunExport(string inputPath, string outputPath)
  {
    try
    {
      if ( !File.Exists(inputPath) )
      {
        Console.Error.WriteLine($"Error: Input file not found: {inputPath}");
        return 1;
      }

      string markdown = File.ReadAllText(inputPath);
      DocxExportService.Export(markdown, outputPath);
      Console.WriteLine($"Successfully exported to: {outputPath}");
      return 0;
    }
    catch ( Exception ex )
    {
      Console.Error.WriteLine($"Error: {ex.Message}");
      return 1;
    }
  }

  private static void PrintHelp()
  {
    Console.WriteLine("MDEdit - Markdown Editor");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  MDEdit.exe                                 Launch GUI editor");
    Console.WriteLine("  MDEdit.exe --export input.md output.docx   Export markdown to DOCX");
    Console.WriteLine("  MDEdit.exe --help                          Show this help message");
  }
}
