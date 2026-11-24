using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Crescent.UI.Controls.Toasts
{
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
}