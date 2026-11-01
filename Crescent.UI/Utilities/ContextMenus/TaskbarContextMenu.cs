using Avalonia.Controls;
using Crescent.Plugins;

namespace Crescent.UI
{
    public static class TaskbarContextMenu
    {
        public static TrayIcon Get(MainWindow mainWindow)
        {
            TrayIcon trayIcon = new()
            {
                ToolTipText = MainWindow.TitleText,
                Icon = mainWindow.Icon,
                Menu = new NativeMenu(),
                
            };
            
            trayIcon.Clicked += (s, e) =>
            {
                if (mainWindow.IsVisible) mainWindow.Hide();
                else
                {
                    mainWindow.Show();
                    mainWindow.WindowState = WindowState.Normal;
                    mainWindow.Activate();
                }
            };
            
            trayIcon.Menu.Items.Add(GetOpenItem(mainWindow));
            trayIcon.Menu.Items.Add(GetCloseItem(mainWindow));
            return trayIcon;
        }

        public static NativeMenuItem GetOpenItem(MainWindow mainWindow)
        {
            NativeMenuItem openItem = new("Open");
            openItem.Click += (_, _) =>
            {
                mainWindow.Show();
                mainWindow.WindowState = WindowState.Normal;
                mainWindow.Activate();
            };
            
            return openItem;
        }

        public static NativeMenuItem GetCloseItem(MainWindow mainWindow)
        {
            NativeMenuItem exitItem = new("Exit");
            exitItem.Click += (s, e) => Application.ExitApplication();
            return exitItem;
        }
    }
}