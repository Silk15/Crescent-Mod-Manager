using System.Collections.Concurrent;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Crescent.APIs.NexusMods;
using Crescent.UI.Controls.Tabs.Elements;
using Newtonsoft.Json.Linq;
using Silk.Utilities;

namespace Crescent.UI.Controls.Tabs
{
    public static class ModMetadata
    {
        private static readonly ConcurrentDictionary<string, Bitmap> ThumbnailCache = new(StringComparer.OrdinalIgnoreCase);

        public static bool TryGetThumbnail(string apiName, string nexusId, out Bitmap? bitmap)
        {
            return ThumbnailCache.TryGetValue($"{apiName}:{nexusId}", out bitmap);
        }

        public static async Task LoadAsync(RenderedMod mod)
        {
            if (mod.isLoadingMetadata || string.IsNullOrEmpty(mod.pluginData.StagingPath) || !Directory.Exists(mod.pluginData.StagingPath) || string.IsNullOrEmpty(mod.pluginData.API.Name) || string.IsNullOrEmpty(mod.modData.url.nexusId)) return;
            mod.isLoadingMetadata = true;
            try
            {
                JObject? details = await NexusUtils.GetModDetailsJsonAsync(mod.pluginData.API.Name, mod.modData.url.nexusId);
                if (details == null) return;

                mod.hasLoadedMetadata = true;
                bool shouldSavePlugin = false;

                string? fetchedName = details["name"]?.ToString();
                if (!string.IsNullOrWhiteSpace(fetchedName) && string.IsNullOrEmpty(mod.modData.nexusName))
                {
                    mod.modData.nexusName = fetchedName.Trim();
                    mod.cleanName = mod.modData.nexusName;
                    shouldSavePlugin = true;
                }

                string rawSummary = details["summary"]?.ToString() ?? string.Empty;
                string cleanedSummary = NexusUtils.TrimNexusFormatting(rawSummary);
                mod.descriptionText = string.IsNullOrWhiteSpace(cleanedSummary) ? "No description available." : cleanedSummary;

                string releaseDate = NexusUtils.FormatNexusDate(details["created_timestamp"], details["created_time"]);
                string updatedDate = NexusUtils.FormatNexusDate(details["updated_timestamp"], details["updated_time"]);
                mod.releaseDateText = $"Released: {releaseDate}";
                mod.updatedDateText = $"Updated: {updatedDate}";

                if (mod.controlsCreated)
                    Dispatcher.UIThread.Post(mod.ApplyMetadataToControls);

                if (shouldSavePlugin) await mod.pluginData.SaveAsync();

                string? pictureUrl = details["picture_url"]?.ToString();
                if (!string.IsNullOrEmpty(pictureUrl))
                    await LoadThumbnailAsync(mod, pictureUrl);
            }
            finally
            {
                mod.isLoadingMetadata = false;
            }
        }

        private static async Task LoadThumbnailAsync(RenderedMod mod, string pictureUrl)
        {
            string cacheKey = $"{mod.pluginData.API.Name}:{mod.modData.url.nexusId}";
            if (ThumbnailCache.TryGetValue(cacheKey, out Bitmap? cachedBitmap))
            {
                if (mod.controlsCreated)
                    Dispatcher.UIThread.Post(() => mod.SetThumbnail(cachedBitmap));
                return;
            }

            string? cachedImagePath = await NexusUtils.GetCachedThumbnailPathAsync(mod.pluginData.API.Name, mod.modData.url.nexusId, pictureUrl);
            if (string.IsNullOrEmpty(cachedImagePath) || !File.Exists(cachedImagePath)) return;

            try
            {
                Bitmap bitmap = await Task.Run(() =>
                {
                    using FileStream fileStream = new(cachedImagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    return Bitmap.DecodeToWidth(fileStream, 480, BitmapInterpolationMode.HighQuality);
                });

                ThumbnailCache[cacheKey] = bitmap;
                if (mod.controlsCreated)
                    Dispatcher.UIThread.Post(() => mod.SetThumbnail(bitmap));
            }
            catch (Exception exception)
            {
                Logger.Error(exception.ToString());
            }
        }
    }
}