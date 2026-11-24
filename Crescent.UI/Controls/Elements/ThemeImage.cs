using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Crescent.Plugins;

namespace Crescent.UI.Controls
{
    public class ThemeImage : Image
    {
        private PluginData pluginData;
        private Func<Theme, string> assetSelector;

        public ThemeImage(PluginData pluginData, Func<Theme, string> assetSelector)
        {
            this.pluginData = pluginData;
            this.assetSelector = assetSelector;
            Stretch = Stretch.UniformToFill;
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs _)
        {
            base.OnAttachedToVisualTree(_);
            if (pluginData.theme == null) return;
            pluginData.theme.onThemeAssetsUpdated += RefreshImage;
            RefreshImage();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs _)
        {
            base.OnDetachedFromVisualTree(_);
            if (pluginData.theme == null) return;
            pluginData.theme.onThemeAssetsUpdated -= RefreshImage;
            DisposeCurrentBitmap();
        }

        private void RefreshImage()
        {
            DisposeCurrentBitmap();
            string assetName = assetSelector(pluginData.theme);
            if (string.IsNullOrEmpty(assetName)) return;

            string? path = null;
            if (!string.IsNullOrEmpty(pluginData.StagingPath))
            {
                string customPath = Path.Combine(pluginData.StagingPath, ".themes", assetName);
                if (File.Exists(customPath)) path = customPath;
            }

            path ??= Path.Combine(Runtime.PluginPath, pluginData.folderName, "Assets", assetName);
            if (File.Exists(path))
            {
                using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                Source = new Bitmap(stream);
            }
        }

        private void DisposeCurrentBitmap()
        {
            if (Source is IDisposable disposable)
            {
                Source = null;
                disposable.Dispose();
            }
        }
    }
}