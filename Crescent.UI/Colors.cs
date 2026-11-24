using Avalonia;
using Avalonia.Media;

namespace Crescent.UI
{
    public static class Colors
    {
        public static readonly SolidColorBrush MainPanelStart = new(Color.FromArgb(255, 40, 40, 40));
        public static readonly SolidColorBrush MainPanelEnd = new(Color.FromArgb(255, 24, 24, 24));
        public static readonly SolidColorBrush Background = new(Color.FromArgb(160, 20, 20, 20));
        
        public static readonly SolidColorBrush Hover = new(Color.FromArgb(180, 60, 60, 60));
        public static readonly SolidColorBrush Click = new(Color.FromArgb(225, 60, 60, 60));
        
        public static readonly SolidColorBrush Separator = new(Color.FromArgb(60, 255, 255, 255));
        public static readonly SolidColorBrush Overlay = new(Color.FromArgb(150, 32, 32, 32));
        public static readonly SolidColorBrush Button = new(Color.FromArgb(90, 60, 60, 60));

        public static readonly SolidColorBrush Red = new(Avalonia.Media.Colors.IndianRed);
        public static readonly SolidColorBrush Accent = new(Color.Parse("#2E71BE"));
        
        public static readonly LinearGradientBrush MainGradient = new()
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops = new GradientStops
            {
                new GradientStop(MainPanelStart.Color, 0),
                new GradientStop(MainPanelEnd.Color, 1)
            }
        };
    }
}