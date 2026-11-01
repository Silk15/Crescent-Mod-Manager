using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Transformation;

namespace Crescent.UI
{
    public class CrescentButton : Button
    {
        public ButtonStyle buttonStyle;
        public IBrush? customColor;
        
        public float hoverScale = 1.05f;
        public float pressedScale = 0.95f;

        public event Action? onClick;

        public bool Scale { get; set; } = true;
        
        public CrescentButton(IBrush customColor) : this(ButtonStyle.Custom)
        {
            this.customColor = customColor;
            Background = customColor;
        }

        public CrescentButton(ButtonStyle buttonStyle)
        {
            HorizontalContentAlignment = HorizontalAlignment.Center;
            VerticalContentAlignment = VerticalAlignment.Center;
            FontWeight = FontWeight.SemiBold;
            Height = 35;
            this.buttonStyle = buttonStyle;
            CornerRadius = new CornerRadius(8);
            Height = 40;
            
            Transitions = new Transitions
            {
                new TransformOperationsTransition
                {
                    Property = RenderTransformProperty,
                    Duration = TimeSpan.FromSeconds(0.1)
                }
            };

            Background = GetBackground();
        }

        public IBrush? GetBackground() => buttonStyle switch
        {
            ButtonStyle.Transparent => Brushes.Transparent,
            ButtonStyle.Translucent => Colors.Button,
            ButtonStyle.Custom => customColor,
            _ => Colors.Button
        };

        #region Button Events

        protected override void OnPointerEntered(PointerEventArgs evt)
        {
            base.OnPointerEntered(evt);
            if (Scale) RenderTransform = TransformOperations.Parse($"scale({hoverScale})");
            Background = Colors.Hover;
        }

        protected override void OnPointerExited(PointerEventArgs evt)
        {
            base.OnPointerExited(evt);
            if (Scale) RenderTransform = TransformOperations.Parse("scale(1.0)");
            Background = GetBackground();
        }

        protected override void OnPointerPressed(PointerPressedEventArgs evt)
        {
            base.OnPointerPressed(evt);
            if (evt.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                if (Scale) RenderTransform = TransformOperations.Parse($"scale({pressedScale})");
                Background = Colors.Click;
            }
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs evt)
        {
            base.OnPointerReleased(evt);
            if (evt.InitialPressMouseButton == MouseButton.Left)
            {
                float value = IsPointerOver ? hoverScale : 1.0f;
                if (Scale) RenderTransform = TransformOperations.Parse($"scale({value})");
                Background = IsPointerOver ? Colors.Hover : null;
            }
            
            onClick?.Invoke();
        }
        
        #endregion
    }
}