using MDEdit.Services;
using MDEdit.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Windows;

namespace MDEdit;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Configure dependency injection
        var services = new ServiceCollection();

        // Register services
        services.AddSingleton<IFileService, FileService>();
        services.AddSingleton<IMarkdownService, MarkdownService>();
        services.AddSingleton<IRecentFilesService, RecentFilesService>();

        // Register ViewModels
        services.AddTransient<MainViewModel>();

        // Build service provider
        _serviceProvider = services.BuildServiceProvider();

        // Create and show main window
        var mainWindow = new MainWindow
        {
            DataContext = _serviceProvider.GetRequiredService<MainViewModel>()
        };
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}

