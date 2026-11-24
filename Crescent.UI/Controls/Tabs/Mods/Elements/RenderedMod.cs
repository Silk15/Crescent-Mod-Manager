using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Crescent.Plugins;
using Crescent.UI.ContextMenus;
using Crescent.Utilities;
using Silk.Utilities;

namespace Crescent.UI.Controls.Tabs.Elements
{
    public partial class RenderedMod : Border
    {
        public PluginData pluginData;
        public ModData modData;
        public ModsTab modsTab;
        public string cleanName;
        public ModGridType? appliedGridType;
        public Panel? cachedLayoutPanel;
        public Dictionary<ModGridType, double> measuredHeights = new();

        public string descriptionText = "No description available.";
        public string releaseDateText = "-";
        public string updatedDateText = "-";
        public bool isExpanded;

        // Are any of us real..
        public bool isReal;
        public bool controlsCreated;
        public bool isLoadingMetadata;
        public bool hasLoadedMetadata;

        public ThumbnailBorder thumbnailBorder = null!;
        public Image thumbnailImage = null!;
        public ToggleSwitch modToggle = null!;
        public TextBlock nameBlock = null!;
        public Control updateControl = null!;
        public TextBlock versionBlock = null!;
        public Button manualButton = null!;
        public Button nexusButton = null!;
        public Button foldoutButton = null!;
        public Button menuButton = null!;
        public TextBlock descriptionBlock = null!;
        public TextBlock releaseDateBlock = null!;
        public TextBlock updatedDateBlock = null!;
        public Control? foldoutContainer;

        public IModLayout CurrentLayout => ModLayouts.Get(appliedGridType ?? pluginData.modGridType);

        public string Version
        {
            get => modData.version;
            set
            {
                modData.version = value;
                if (controlsCreated)
                    versionBlock.Text = string.IsNullOrEmpty(value) ? "-" : $"v{value}";
            }
        }

        public bool IsChecked
        {
            get => controlsCreated ? (modToggle.IsChecked ?? false) : pluginData.SelectedProfile.IsModEnabled(modData);
            set
            {
                if (controlsCreated) modToggle.IsChecked = value;
            }
        }

        public bool IsManual
        {
            get => modData.isManual;
            set
            {
                modData.isManual = value;
                if (!controlsCreated) return;
                if (manualButton is CrescentButton crescentButton) crescentButton.CustomColor = modData.isManual ? Colors.Red : Colors.Accent;
                manualButton.Content = modData.isManual ? "Manual" : "Managed";
                manualButton.Foreground = modData.isManual ? Colors.Red : Colors.Accent;
                manualButton.BorderBrush = modData.isManual ? Colors.Red : Colors.Accent;
            }
        }

        public RenderedMod(PluginData pluginData, ModData modData, ModsTab modsTab)
        {
            this.pluginData = pluginData;
            this.modData = modData;
            this.modsTab = modsTab;

            cleanName = !string.IsNullOrEmpty(modData.nexusName) ? modData.nexusName : modData.DisplayName.Cleaned();

            Background = Colors.Overlay;
            CornerRadius = new CornerRadius(4);
        }

        public double GetEstimatedHeight(ModGridType gridType) => measuredHeights.TryGetValue(gridType, out double height) ? height : ModLayouts.Get(gridType).EstimatedHeight;

        private void GetMeasuredHeight(ModGridType gridType)
        {
            if (Bounds.Height > 0 && !isExpanded)
                measuredHeights[gridType] = Bounds.Height;
        }

        private void SetControls()
        {
            if (controlsCreated) return;
            controlsCreated = true;

            ContextMenu = ModContextMenu.Get(pluginData, modData, modsTab);
            thumbnailImage = GetThumbnailImage();
            thumbnailBorder = GetThumbnailBorder();
            modToggle = GetModToggle();
            nameBlock = GetNameTextBlock();
            updateControl = GetUpdateControl();
            versionBlock = GetVersionTextBlock();
            manualButton = GetManualButton();
            nexusButton = GetNexusButton();
            foldoutButton = GetFoldoutButton();
            menuButton = GetMenuButton();
            descriptionBlock = GetDescriptionTextBlock();
            releaseDateBlock = GetReleaseDateTextBlock();
            updatedDateBlock = GetUpdatedDateTextBlock();
        }

        public void SetThumbnail(Bitmap bitmap)
        {
            thumbnailImage.Source = bitmap;
            thumbnailBorder.IsVisible = CurrentLayout.CanShowThumbnail(true);
        }

        public void ApplyMetadataToControls()
        {
            nameBlock.Text = cleanName;
            ToolTip.SetTip(nameBlock, cleanName);
            descriptionBlock.Text = descriptionText;
            SetReleaseDateValue(releaseDateText);
            SetUpdatedDateValue(updatedDateText);
        }

        public void SetLayout(ModGridType gridType, bool realizeImmediately)
        {
            if (appliedGridType != gridType)
            {
                cachedLayoutPanel = null;
                appliedGridType = null;
            }

            if (realizeImmediately) Realise(gridType);
            else Virtualise(gridType);
        }

        public void Realise(ModGridType gridType)
        {
            SetControls();

            if (thumbnailImage.Source == null && ModMetadata.TryGetThumbnail(pluginData.API.Name, modData.url.nexusId, out Bitmap? cachedBitmap) && cachedBitmap != null)
                thumbnailImage.Source = cachedBitmap;

            IModLayout layout = ModLayouts.Get(gridType);
            if (appliedGridType != gridType || cachedLayoutPanel == null)
            {
                appliedGridType = gridType;
                Margin = layout.Margin;
                cachedLayoutPanel = layout.BuildMod(this);
            }

            thumbnailBorder.IsVisible = layout.CanShowThumbnail(thumbnailImage.Source != null);

            if (!isReal || Child != cachedLayoutPanel)
            {
                Height = double.NaN;
                Child = cachedLayoutPanel;
                isReal = true;
            }

            if (!hasLoadedMetadata && !isLoadingMetadata && !string.IsNullOrEmpty(modData.url.nexusId))
                ModMetadata.LoadAsync(this).FireAndForget();
        }

        public void Virtualise(ModGridType gridType)
        {
            if (isReal)
                GetMeasuredHeight(gridType);

            IModLayout layout = ModLayouts.Get(gridType);
            isReal = false;
            Child = null;
            Margin = layout.Margin;
            Height = GetEstimatedHeight(gridType);
        }

        public void ApplyLayout(ModGridType gridType) => Realise(gridType);

        public void UpdateControl()
        {
            if (!controlsCreated) return;
            Control oldControl = updateControl;
            updateControl = GetUpdateControl();
            CurrentLayout.ReplaceUpdateControl(this, oldControl, updateControl);
        }

        public void RefreshDeployedState()
        {
            if (!controlsCreated) return;
            bool isEnabled = pluginData.SelectedProfile.IsModEnabled(modData);
            if (modToggle.IsChecked != isEnabled) modToggle.IsChecked = isEnabled;
            nameBlock.Foreground = isEnabled ? Brushes.White : Brushes.Gray;
            IsManual = modData.isManual;
        }

        public void RefreshMod()
        {
            cleanName = !string.IsNullOrEmpty(modData.nexusName) ? modData.nexusName : modData.DisplayName.Cleaned();
            if (!controlsCreated) return;

            Version = modData.version;
            nameBlock.Text = cleanName;
            ToolTip.SetTip(nameBlock, cleanName);
            nexusButton.IsVisible = !string.IsNullOrEmpty(modData.url.nexusId);
            thumbnailBorder.IsVisible = CurrentLayout.CanShowThumbnail(thumbnailImage.Source != null);
            RefreshDeployedState();
            UpdateControl();

            if (isReal && !hasLoadedMetadata && !isLoadingMetadata && !string.IsNullOrEmpty(modData.url.nexusId))
                ModMetadata.LoadAsync(this).FireAndForget();
        }
    }
}