using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Crescent.Plugins;

namespace Crescent.UI
{
    public static class ToastManager
    {
        private static DispatcherTimer? progressTimer;
        private static ToastOverlay? toastOverlay;
        private static ToastItem? currentToast;
        
        private static DateTime lastCompletedTime = DateTime.MinValue;
        private static string lastCompletedMessage = string.Empty;

        public static bool Initialised => toastOverlay != null;

        public static ToastOverlay Initialize()
        {
            toastOverlay = new();
            return toastOverlay;
        }

        public static void Show(string title, string message, ToastType toastType = ToastType.Info, int duration = 4000, string detail = "")
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (toastOverlay == null) return;
                string dedupeKey = $"{title}|{message}|{detail}";
                if (dedupeKey == lastCompletedMessage && (DateTime.UtcNow - lastCompletedTime).TotalMilliseconds < 750) return;

                lastCompletedMessage = dedupeKey;
                lastCompletedTime = DateTime.UtcNow;

                ToastItem toastItem = new(title, message, detail, toastType);
                toastItem.dismissButton.Click += (_, _) => DismissToast(toastItem);

                while (toastOverlay.Children.Count >= 4)
                {
                    Control? oldest = toastOverlay.Children.FirstOrDefault(control => control != currentToast);
                    if (oldest == null) break;
                    toastOverlay.Children.Remove(oldest);
                }

                toastOverlay.Children.Add(toastItem);

                if (duration > 0 && toastType != ToastType.Progress)
                    DispatcherTimer.RunOnce(() => DismissToast(toastItem), TimeSpan.FromMilliseconds(duration));
            });
        }

        public static void ShowProgress(string title, string message, string detail = "", double percentage = 0, bool isIndeterminate = false)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (toastOverlay == null) return;
                if (currentToast == null)
                {
                    currentToast = new(title, message, detail, ToastType.Progress);
                    currentToast.dismissButton.Click += (_, _) => CloseProgress();
                    toastOverlay.Children.Add(currentToast);
                }

                currentToast.SetAccent(ToastType.Progress);
                currentToast.titleBlock.Text = title;
                currentToast.messageBlock.Text = message;
                currentToast.detailBlock.Text = detail;
                currentToast.detailBlock.IsVisible = !string.IsNullOrEmpty(detail);
                currentToast.progressBar.IsVisible = true;
                currentToast.progressBar.IsIndeterminate = isIndeterminate;
                currentToast.progressBar.Value = percentage;

                ResetProgress();
            });
        }

        public static void UpdateProgress(double current, double total, string modName, string targetFile = "")
        {
            double percentage = total > 0 ? current / total * 100 : 0;
            string cleanModName = ModData.StripArchiveExtension(modName);
            string statusText = $"{cleanModName} ({current}/{total})";
            string detailText = string.IsNullOrEmpty(targetFile) ? string.Empty : Path.GetFileName(targetFile);
            ShowProgress("Deploying Mods", statusText, detailText, percentage, false);
        }

        public static void CompleteProgress(string title, string message, ToastType toastType = ToastType.Success, int duration = 4500, string detail = "")
        {
            Dispatcher.UIThread.Post(() =>
            {
                StopProgress();
                if (currentToast != null && toastOverlay != null)
                {
                    toastOverlay.Children.Remove(currentToast);
                    currentToast = null;
                }

                Show(title, message, toastType, duration, detail);
            });
        }

        public static void OnProgressDialogClosed()
        {
            Dispatcher.UIThread.Post(() =>
            {
                StopProgress();
                if (currentToast == null || toastOverlay == null) return;
                toastOverlay.Children.Remove(currentToast);
                currentToast = null;
            });
        }

        public static void CloseProgress()
        {
            Dispatcher.UIThread.Post(() =>
            {
                StopProgress();
                if (currentToast == null || toastOverlay == null) return;
                toastOverlay.Children.Remove(currentToast);
                currentToast = null;
            });
        }

        private static void ResetProgress()
        {
            progressTimer?.Stop();
            progressTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            
            progressTimer.Tick += (_, _) =>
            {
                progressTimer?.Stop();
                if (currentToast != null && toastOverlay != null)
                {
                    toastOverlay.Children.Remove(currentToast);
                    currentToast = null;
                }
            };
            
            progressTimer.Start();
        }

        private static void StopProgress()
        {
            progressTimer?.Stop();
            progressTimer = null;
        }

        private static void DismissToast(ToastItem toastItem)
        {
            if (toastOverlay == null) return;
            if (toastOverlay.Children.Contains(toastItem)) toastOverlay.Children.Remove(toastItem);
            if (currentToast == toastItem)
            {
                StopProgress();
                currentToast = null;
            }
        }
    }
}