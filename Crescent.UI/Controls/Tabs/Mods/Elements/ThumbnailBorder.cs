using Avalonia;
using Avalonia.Controls;

namespace Crescent.UI.Controls.Tabs.Elements
{
    public class ThumbnailBorder : Border
    {
        public bool UseAspectRatio { get; set; }

        protected override Size MeasureOverride(Size availableSize)
        {
            if (UseAspectRatio && !double.IsInfinity(availableSize.Width) && availableSize.Width > 0)
            {
                double height = Math.Round(availableSize.Width * 9.0 / 16.0);
                base.MeasureOverride(new Size(availableSize.Width, height));
                return new Size(availableSize.Width, height);
            }

            return base.MeasureOverride(availableSize);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (UseAspectRatio && finalSize.Width > 0)
            {
                double height = Math.Round(finalSize.Width * 9.0 / 16.0);
                return base.ArrangeOverride(new Size(finalSize.Width, height));
            }

            return base.ArrangeOverride(finalSize);
        }
    }
}