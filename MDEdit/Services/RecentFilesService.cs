using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace MDEdit.Services;

/// <summary>
/// Implementation of recent files service with persistence
/// </summary>
public class RecentFilesService : IRecentFilesService
{
    private const int MaxRecentFiles = 10;
    private readonly string _settingsPath;
    private readonly List<string> _recentFiles;

    public IReadOnlyList<string> RecentFiles => _recentFiles.AsReadOnly();

    public RecentFilesService()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MDEdit");

        Directory.CreateDirectory(appDataPath);
        _settingsPath = Path.Combine(appDataPath, "recent-files.json");
        _recentFiles = LoadRecentFiles();
    }

    public void AddRecentFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return;

        // Remove if already exists
        _recentFiles.Remove(filePath);

        // Add to the beginning
        _recentFiles.Insert(0, filePath);

        // Keep only the maximum number of recent files
        if (_recentFiles.Count > MaxRecentFiles)
        {
            _recentFiles.RemoveRange(MaxRecentFiles, _recentFiles.Count - MaxRecentFiles);
        }

        SaveRecentFiles();
    }

    public void ClearRecentFiles()
    {
        _recentFiles.Clear();
        SaveRecentFiles();
    }

    private List<string> LoadRecentFiles()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                var files = JsonSerializer.Deserialize<List<string>>(json);

                // Filter out files that no longer exist
                return files?.Where(File.Exists).ToList() ?? new List<string>();
            }
        }
        catch
        {
            // If loading fails, return empty list
        }

        return new List<string>();
    }

    private void SaveRecentFiles()
    {
        try
        {
            var json = JsonSerializer.Serialize(_recentFiles, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(_settingsPath, json);
        }
        catch
        {
            // Silently fail if saving fails
        }
    }
}
