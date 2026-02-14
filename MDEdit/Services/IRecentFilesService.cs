using System.Collections.Generic;

namespace MDEdit.Services;

/// <summary>
/// Service for managing recently opened files
/// </summary>
public interface IRecentFilesService
{
    /// <summary>
    /// Gets the list of recently opened files
    /// </summary>
    IReadOnlyList<string> RecentFiles { get; }

    /// <summary>
    /// Adds a file to the recent files list
    /// </summary>
    /// <param name="filePath">The file path to add</param>
    void AddRecentFile(string filePath);

    /// <summary>
    /// Clears all recent files
    /// </summary>
    void ClearRecentFiles();
}
