using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Crescent.UI.Controls.Sidebar
{
    public class ProfileButton : CrescentButton
    {
        public ProfileButton(string profileName, bool isActive) : base(ButtonStyle.Translucent)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch;
            HorizontalContentAlignment = HorizontalAlignment.Stretch;
            VerticalContentAlignment = VerticalAlignment.Stretch;
            CornerRadius = new(4);
            Padding = new(0);
            ClipToBounds = true;

            TextBlock titleText = new()
            {
                Text = profileName,
                FontSize = 13,
                FontWeight = FontWeight.SemiBold,
                Foreground = Brushes.White,
                Opacity = isActive ? 1.0 : 0.65,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new(10, 0)
            };

            Border activeIndicator = new()
            {
                Height = 2,
                Background = isActive ? Colors.Accent : Brushes.Transparent,
                VerticalAlignment = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            Grid cellGrid = new();
            cellGrid.Children.Add(titleText);
            cellGrid.Children.Add(activeIndicator);

            Content = cellGrid;
        }
    }
}