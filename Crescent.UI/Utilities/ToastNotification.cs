using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Crescent.Plugins;

namespace Crescent.UI
{
    public enum ToastType
    {
        Info,
        Success,
        Warning,
        Error,
        Progress
    }

    public class ToastItem : Border
    {
        public const double FixedToastWidth = 360;

        public TextBlock titleBlock;
        public TextBlock messageBlock;
        public TextBlock detailBlock;
        public ProgressBar progressBar;
        public Border accentBar;
        public Button dismissButton;

        public ToastItem(string title, string message, string detail, ToastType toastType)
        {
            Width = FixedToastWidth;
            MinWidth = FixedToastWidth;
            MaxWidth = FixedToastWidth;
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops = new GradientStops
                {
                    new GradientStop(Colors.MainPanelStart.Color, 0),
                    new GradientStop(Colors.MainPanelEnd.Color, 1)
                }
            };
            BorderBrush = Colors.Separator;
            BorderThickness = new Thickness(1);
            CornerRadius = new CornerRadius(8);
            ClipToBounds = true;
            Margin = new Thickness(0, 8, 0, 0);
            HorizontalAlignment = HorizontalAlignment.Right;

            Grid rootGrid = new();
            rootGrid.ColumnDefinitions.Add(new ColumnDefinition(4, GridUnitType.Pixel));
            rootGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));

            accentBar = new()
            {
                Background = GetAccentBrush(toastType),
                VerticalAlignment = VerticalAlignment.Stretch
            };
            Grid.SetColumn(accentBar, 0);
            rootGrid.Children.Add(accentBar);

            StackPanel contentStack = new()
            {
                Margin = new Thickness(14, 12, 14, 12),
                Spacing = 5,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            Grid headerGrid = new();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            titleBlock = new()
            {
                Text = title,
                FontWeight = FontWeight.Bold,
                FontSize = 14,
                Foreground = Brushes.White,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(titleBlock, 0);
            headerGrid.Children.Add(titleBlock);

            dismissButton = new()
            {
                Content = "✕",
                Width = 22,
                Height = 22,
                Padding = new Thickness(0),
                Background = Brushes.Transparent,
                Foreground = Brushes.Gray,
                FontSize = 11,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(dismissButton, 1);
            headerGrid.Children.Add(dismissButton);

            messageBlock = new()
            {
                Text = message,
                FontSize = 12,
                Foreground = Brushes.LightGray,
                TextTrimming = TextTrimming.CharacterEllipsis,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            detailBlock = new()
            {
                Text = detail,
                FontSize = 11,
                Foreground = Brushes.Gray,
                TextTrimming = TextTrimming.CharacterEllipsis,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                IsVisible = !string.IsNullOrEmpty(detail)
            };

            progressBar = new()
            {
                Height = 6,
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Foreground = Brushes.CornflowerBlue,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                IsVisible = toastType == ToastType.Progress
            };

            contentStack.Children.Add(headerGrid);
            contentStack.Children.Add(messageBlock);
            contentStack.Children.Add(detailBlock);
            contentStack.Children.Add(progressBar);

            Grid.SetColumn(contentStack, 1);
            rootGrid.Children.Add(contentStack);

            Child = rootGrid;
        }

        public void SetAccent(ToastType toastType) => accentBar.Background = GetAccentBrush(toastType);

        private static IBrush GetAccentBrush(ToastType toastType) => toastType switch
        {
            ToastType.Success => Colors.Green,
            ToastType.Warning => Brushes.Orange,
            ToastType.Error => Colors.Red,
            ToastType.Progress => Brushes.CornflowerBlue,
            _ => Brushes.CornflowerBlue
        };
    }

    public class ToastOverlay : StackPanel
    {
        public ToastOverlay()
        {
            Width = ToastItem.FixedToastWidth;
            HorizontalAlignment = HorizontalAlignment.Right;
            VerticalAlignment = VerticalAlignment.Bottom;
            Margin = new Thickness(0, 0, 24, 24);
            Spacing = 8;
            ZIndex = 1000;
        }
    }

    public static class ToastManager
    {
        private static ToastOverlay? overlay;
        private static ToastItem? activeProgressToast;
        private static DispatcherTimer? progressWatchdogTimer;
        private static string lastCompletedMessage = string.Empty;
        private static DateTime lastCompletedTime = DateTime.MinValue;

        public static bool IsInitialized => overlay != null;

        public static ToastOverlay Initialize()
        {
            overlay = new();
            return overlay;
        }

        public static void Show(string title, string message, ToastType toastType = ToastType.Info, int durationMs = 4000, string detail = "")
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (overlay == null) return;

                string dedupeKey = $"{title}|{message}|{detail}";
                if (dedupeKey == lastCompletedMessage && (DateTime.UtcNow - lastCompletedTime).TotalMilliseconds < 750)
                    return;

                lastCompletedMessage = dedupeKey;
                lastCompletedTime = DateTime.UtcNow;

                ToastItem toastItem = new(title, message, detail, toastType);
                toastItem.dismissButton.Click += (_, _) => DismissToast(toastItem);

                while (overlay.Children.Count >= 4)
                {
                    Control? oldest = overlay.Children.FirstOrDefault(control => control != activeProgressToast);
                    if (oldest == null) break;
                    overlay.Children.Remove(oldest);
                }

                overlay.Children.Add(toastItem);

                if (durationMs > 0 && toastType != ToastType.Progress)
                    DispatcherTimer.RunOnce(() => DismissToast(toastItem), TimeSpan.FromMilliseconds(durationMs));
            });
        }

        public static void ShowProgress(string title, string message, string detail = "", double percentage = 0, bool isIndeterminate = false)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (overlay == null) return;

                if (activeProgressToast == null)
                {
                    activeProgressToast = new(title, message, detail, ToastType.Progress);
                    activeProgressToast.dismissButton.Click += (_, _) => CloseProgress();
                    overlay.Children.Add(activeProgressToast);
                }

                activeProgressToast.SetAccent(ToastType.Progress);
                activeProgressToast.titleBlock.Text = title;
                activeProgressToast.messageBlock.Text = message;
                activeProgressToast.detailBlock.Text = detail;
                activeProgressToast.detailBlock.IsVisible = !string.IsNullOrEmpty(detail);
                activeProgressToast.progressBar.IsVisible = true;
                activeProgressToast.progressBar.IsIndeterminate = isIndeterminate;
                activeProgressToast.progressBar.Value = percentage;

                ResetProgressWatchdog();
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

        public static void CompleteProgress(string title, string message, ToastType toastType = ToastType.Success, int durationMs = 4500, string detail = "")
        {
            Dispatcher.UIThread.Post(() =>
            {
                StopProgressWatchdog();
                if (activeProgressToast != null && overlay != null)
                {
                    overlay.Children.Remove(activeProgressToast);
                    activeProgressToast = null;
                }

                Show(title, message, toastType, durationMs, detail);
            });
        }

        public static void OnProgressDialogClosed()
        {
            Dispatcher.UIThread.Post(() =>
            {
                StopProgressWatchdog();
                if (activeProgressToast == null || overlay == null) return;
                overlay.Children.Remove(activeProgressToast);
                activeProgressToast = null;
            });
        }

        public static void CloseProgress()
        {
            Dispatcher.UIThread.Post(() =>
            {
                StopProgressWatchdog();
                if (activeProgressToast == null || overlay == null) return;
                overlay.Children.Remove(activeProgressToast);
                activeProgressToast = null;
            });
        }

        private static void ResetProgressWatchdog()
        {
            progressWatchdogTimer?.Stop();
            progressWatchdogTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            progressWatchdogTimer.Tick += (_, _) =>
            {
                progressWatchdogTimer?.Stop();
                if (activeProgressToast != null && overlay != null)
                {
                    overlay.Children.Remove(activeProgressToast);
                    activeProgressToast = null;
                }
            };
            progressWatchdogTimer.Start();
        }

        private static void StopProgressWatchdog()
        {
            progressWatchdogTimer?.Stop();
            progressWatchdogTimer = null;
        }

        private static void DismissToast(ToastItem toastItem)
        {
            if (overlay == null) return;
            if (overlay.Children.Contains(toastItem)) overlay.Children.Remove(toastItem);
            if (activeProgressToast == toastItem)
            {
                StopProgressWatchdog();
                activeProgressToast = null;
            }
        }
    }
}