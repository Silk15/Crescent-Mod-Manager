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
        public static void Initialise() => Runtime.onCrescentLoaded += async args =>
        {
            Logger.Log(string.Join(", ", args));
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown);
        };

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

            ProgressDialog? activeProgressWindow = null;
            PluginData.OnOverlapConfirm = async (title, message) =>
            {
                string inputResult = string.Empty;
                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    if (desktop.MainWindow is not Window parentWindow) return;
                    ConfirmationDialog confirmationDialog = new(title, message, false);

                    await confirmationDialog.ShowDialog(parentWindow);
                    inputResult = confirmationDialog.InputText;
                });

                return inputResult;
            };

            PluginData.OnConflictResolve = async conflicts =>
            {
                bool resolutionResult = false;
                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    if (desktop.MainWindow is not Window parentWindow) return;
                    ConflictReportDialog reportDialog = new(conflicts);

                    await reportDialog.ShowDialog(parentWindow);
                    resolutionResult = reportDialog.conflictsResolved;
                });

                return resolutionResult;
            };

            PluginData.OnProgressWindowOpen = async () =>
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
                        Logger.Error(exception.ToString());
                    }
                });
            };

            PluginData.OnProgressWindowUpdate = async (int processedFiles, int totalFiles, string modName, string targetFile) =>
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    try
                    {
                        if (activeProgressWindow != null) activeProgressWindow.UpdateProgress(processedFiles, totalFiles, modName, targetFile);
                        if (mainWindow.sidebarOverlay != null) mainWindow.sidebarOverlay.UpdateProgress(processedFiles, totalFiles);
                    }
                    catch (Exception exception)
                    {
                        Logger.Error(exception.ToString());
                    }
                }, DispatcherPriority.Background);
            };

            PluginData.OnProgressWindowClose = async () =>
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    try
                    {
                        activeProgressWindow?.Close();
                        activeProgressWindow = null;

                        if (mainWindow.sidebarOverlay != null && SidebarOverlay.deploymentProgressBar != null)
                            SidebarOverlay.deploymentProgressBar.IsVisible = false;
                    }
                    catch (Exception exception)
                    {
                        Logger.Error(exception.ToString());
                    }
                });
            };

            mainWindow.Closing += (sender, args) =>
            {
                args.Cancel = true;
                mainWindow.Hide();
            };

            TrayIcon.SetIcons(this, new TrayIcons
            {
                TaskbarContextMenu.Get(mainWindow)
            });
            
            mainWindow.Show();
        }
        
        public static void ExitApplication()
        {
            foreach (PluginData configuredGame in PluginData.InstalledPlugins)
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
        }
    }
}