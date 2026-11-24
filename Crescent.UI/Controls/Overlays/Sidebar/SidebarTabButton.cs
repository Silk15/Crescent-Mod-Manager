using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Transformation;

namespace Crescent.UI.Controls.Sidebar
{
    public class SidebarTabButton : CrescentButton
    {
        public TextBlock titleText;
        public Border activeIndicator;

        public override float HoverScale => 1.025f;
        public override float PressedScale => 0.975f;

        public SidebarTabButton(string tabTitle) : base(ButtonStyle.Translucent)
        {
            Scale = false;
            Height = double.NaN;
            HorizontalAlignment = HorizontalAlignment.Stretch;
            VerticalAlignment = VerticalAlignment.Stretch;
            HorizontalContentAlignment = HorizontalAlignment.Stretch;
            VerticalContentAlignment = VerticalAlignment.Stretch;
            CornerRadius = new(4, 4, 0, 0);
            Margin = new(4, 7, 4, 0);
            Padding = new(0);
            ClipToBounds = true;
            RenderTransformOrigin = new RelativePoint(0.5, 1.0, RelativeUnit.Relative);

            titleText = new()
            {
                Text = tabTitle,
                FontWeight = FontWeight.SemiBold,
                FontSize = 14,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            activeIndicator = new()
            {
                Height = 2,
                Background = Brushes.Transparent,
                VerticalAlignment = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            Grid cellGrid = new();
            cellGrid.Children.Add(titleText);
            cellGrid.Children.Add(activeIndicator);

            Content = cellGrid;
        }

        public void SetActive(bool isActive)
        {
            titleText.Opacity = isActive ? 1.0 : 0.65;
            activeIndicator.Background = isActive ? Colors.Accent : Brushes.Transparent;
        }

        protected override void OnPointerEntered(PointerEventArgs evt)
        {
            base.OnPointerEntered(evt);
            RenderTransform = TransformOperations.Parse($"scale({HoverScale}, 1.0)");
        }

        protected override void OnPointerExited(PointerEventArgs evt)
        {
            base.OnPointerExited(evt);
            RenderTransform = TransformOperations.Parse("scale(1.0, 1.0)");
        }

        protected override void OnPointerPressed(PointerPressedEventArgs evt)
        {
            base.OnPointerPressed(evt);
            if (evt.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                RenderTransform = TransformOperations.Parse($"scale({PressedScale}, 1.0)");
                BorderThickness = new Thickness(1, 1, 1, 0);
            }
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs evt)
        {
            base.OnPointerReleased(evt);
            if (evt.InitialPressMouseButton == MouseButton.Left)
            {
                float value = IsPointerOver ? HoverScale : 1.0f;
                RenderTransform = TransformOperations.Parse($"scale({value}, 1.0)");
                BorderThickness = new Thickness(0);
            }
        }
    }
}