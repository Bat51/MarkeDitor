using System;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace MarkeDitor;

public partial class App : Application
{
    public static string? InitialFilePath { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void RegisterServices()
    {
        base.RegisterServices();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            try
            {
                var path = Path.Combine(AppContext.BaseDirectory, "crash.log");
                File.WriteAllText(path, $"{DateTime.Now}\n{e.ExceptionObject}");
            }
            catch { /* swallow */ }
        };

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            InitialFilePath = TryGetFilePathFromArgs(desktop.Args);
            desktop.MainWindow = new MainWindow(InitialFilePath);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static string? TryGetFilePathFromArgs(string[]? args)
    {
        if (args == null) return null;
        foreach (var arg in args)
        {
            if (string.IsNullOrWhiteSpace(arg)) continue;
            if (arg.StartsWith("-")) continue;
            // On Windows '/' prefixes a CLI switch; on Linux it's an absolute
            // path (which is exactly what file managers pass on double-click).
            if (OperatingSystem.IsWindows() && arg.StartsWith("/")) continue;
            try
            {
                if (File.Exists(arg)) return Path.GetFullPath(arg);
            }
            catch { /* skip */ }
        }
        return null;
    }
}
