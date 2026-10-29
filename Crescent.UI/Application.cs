using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Crescent.Utilities;
using Crescent.Plugins;

namespace Crescent.UI
{
    public class Application : Avalonia.Application
    {
        public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<Application>().UsePlatformDetect().LogToTrace();

        [InitialiseOnLoad]
        public static void Initialise() => Runtime.onCrescentLoaded += strings => BuildAvaloniaApp().StartWithClassicDesktopLifetime(strings, ShutdownMode.OnMainWindowClose);
        
        public override void Initialize() => Styles.Add(new FluentTheme());

        public override async void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) await OpenLoadingWindow(desktop);
            base.OnFrameworkInitializationCompleted();
        }

        public async Task OpenLoadingWindow(IClassicDesktopStyleApplicationLifetime desktop)
        {
            LoadingWindow loadingWindow = new();
            desktop.MainWindow = loadingWindow;
            loadingWindow.Show();
            
            await Runtime.LoadCrescentAsync(process => loadingWindow.UpdateProcess(process), task => loadingWindow.UpdateStatus(task));
            AssetLoader.InvalidateAssemblyCache();
            OpenMainWindow(desktop);
            loadingWindow.Close();
        }

        public void OpenMainWindow(IClassicDesktopStyleApplicationLifetime desktop)
        {
            MainWindow mainWindow = new();
            desktop.MainWindow = mainWindow;

            ProgressWindow? activeProgressWindow = null;

            GameData.OnRequestOverlapConfirmation = async (string title, string message) =>
            {
                string inputResult = string.Empty;
                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    if (desktop.MainWindow is not Window parentWindow) return;
                    InputDialog dialog = new(title, message, true, "yes");
                    await dialog.ShowDialog(parentWindow);
                    inputResult = dialog.InputText ?? string.Empty;
                });
                return inputResult;
            };

            GameData.OnRequestProgressShow = async () =>
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    try
                    {
                        activeProgressWindow = new();
                        activeProgressWindow.Show();
                    }
                    catch (Exception exception)
                    {
                        Logger.Log(exception.ToString());
                    }
                });
            };

            GameData.OnRequestProgressUpdate = async (int processedFiles, int totalFiles, string modName, string targetFile) =>
            {
                if (activeProgressWindow == null) return;
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    try
                    {
                        activeProgressWindow.UpdateProgress(processedFiles, totalFiles, modName, targetFile);
                    }
                    catch (Exception exception)
                    {
                        Logger.Log(exception.ToString());
                    }
                }, DispatcherPriority.Background);
            };

            GameData.OnRequestProgressClose = async () =>
            {
                if (activeProgressWindow == null) return;
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    try
                    {
                        activeProgressWindow?.Close();
                        activeProgressWindow = null;
                    }
                    catch (Exception exception)
                    {
                        Logger.Log(exception.ToString());
                    }
                });
            };

            mainWindow.Show();
            mainWindow.Closed += (sender, args) =>
            {
                foreach (GameData configuredGame in GameData.InstalledGames)
                {
                    try
                    {
                        configuredGame.Unload();
                        for (int index = 0; index < configuredGame.installedMods.Count; index++)
                            configuredGame.OnModUnloaded(configuredGame.installedMods[index]);
                    }
                    catch (Exception exception)
                    {
                        Logger.Error(exception.ToString());
                    }
                }
                Environment.Exit(0);
            };
        }
    }
}