namespace MDEdit.Services;

/// <summary>
/// Service for file operations
/// </summary>
public interface IFileService
{
    /// <summary>
    /// Opens a file dialog and returns the selected file path
    /// </summary>
    /// <returns>The selected file path, or null if cancelled</returns>
    string? OpenFile();

    /// <summary>
    /// Opens a save file dialog and returns the selected file path
    /// </summary>
    /// <returns>The selected file path, or null if cancelled</returns>
    string? SaveFileAs();

    /// <summary>
    /// Reads the contents of a file
    /// </summary>
    /// <param name="filePath">The path to the file</param>
    /// <returns>The file contents</returns>
    string ReadFile(string filePath);

    /// <summary>
    /// Writes content to a file
    /// </summary>
    /// <param name="filePath">The path to the file</param>
    /// <param name="content">The content to write</param>
    void WriteFile(string filePath, string content);
}
