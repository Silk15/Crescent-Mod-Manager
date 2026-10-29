using Avalonia.Media;

namespace Crescent.Utilities
{
    public static class Colors
    {
        public static readonly SolidColorBrush MainPanelStart = new(Color.FromArgb(255, 40, 40, 40));
        public static readonly SolidColorBrush MainPanelEnd = new(Color.FromArgb(255, 24, 24, 24));

        public static readonly SolidColorBrush Button = new(Color.FromArgb(90, 60, 60, 60));
        public static readonly SolidColorBrush Hover = new(Color.FromArgb(180, 60, 60, 60));
        public static readonly SolidColorBrush Click = new(Color.FromArgb(225, 60, 60, 60));

        public static readonly SolidColorBrush Overlay = new(Color.FromArgb(150, 32, 32, 32));
        public static readonly SolidColorBrush Separator = new(Color.FromArgb(60, 255, 255, 255));

        public static readonly SolidColorBrush Red = new(Avalonia.Media.Colors.IndianRed);
        public static readonly SolidColorBrush Green = new(Avalonia.Media.Colors.SeaGreen);
    }
}