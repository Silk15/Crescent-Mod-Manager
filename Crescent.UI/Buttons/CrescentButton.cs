using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Colors = Crescent.Utilities.Colors;

namespace Crescent.UI
{
    public class CrescentButton : Button
    {
        public ButtonStyle buttonStyle;
        public IBrush customColor;
        
        public float hoverScale = 1.05f;
        public float pressedScale = 0.95f;
        
        public CrescentButton(IBrush customColor) : this(ButtonStyle.Custom)
        {
            this.customColor = customColor;
            Background = customColor;
        }

        public CrescentButton(ButtonStyle buttonStyle)
        {
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

        public IBrush GetBackground() => buttonStyle switch
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
            RenderTransform = TransformOperations.Parse($"scale({hoverScale})");
            Background = Colors.Hover;
        }

        protected override void OnPointerExited(PointerEventArgs evt)
        {
            base.OnPointerExited(evt);
            RenderTransform = TransformOperations.Parse("scale(1.0)");
            Background = GetBackground();
        }

        protected override void OnPointerPressed(PointerPressedEventArgs evt)
        {
            base.OnPointerPressed(evt);
            if (evt.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                RenderTransform = TransformOperations.Parse($"scale({pressedScale})");
                Background = Colors.Click;
            }
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs evt)
        {
            base.OnPointerReleased(evt);
            if (evt.InitialPressMouseButton == MouseButton.Left)
            {
                float value = IsPointerOver ? hoverScale : 1.0f;
                RenderTransform = TransformOperations.Parse($"scale({value})");
                Background = IsPointerOver ? Colors.Hover : null;
            }
        }
        
        #endregion
    }
}