using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Transformation;

namespace Crescent.UI.Controls
{
    public class CrescentButton : Button
    {
        public ButtonStyle ButtonStyle { get; set; }
        public IBrush? CustomColor { get; set; }
        
        public virtual float HoverScale { get; set; } = 1.05f;
        public virtual float PressedScale { get; set; } = 0.95f;
        
        public virtual bool UseBorder { get; set; } = true;
        public bool Scale { get; set; } = true;

        public event Action onClick;
        
        public CrescentButton(IBrush customColor) : this(ButtonStyle.Custom)
        {
            CustomColor = customColor;
            Background = customColor;
        }

        public CrescentButton(ButtonStyle buttonStyle)
        {
            HorizontalContentAlignment = HorizontalAlignment.Center;
            VerticalContentAlignment = VerticalAlignment.Center;
            FontWeight = FontWeight.SemiBold;
            Height = 35;
            MinWidth = 5;
            ButtonStyle = buttonStyle;
            CornerRadius = new CornerRadius(4);
            if (BorderBrush == null) BorderBrush = Colors.Accent;
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

        public IBrush? GetBackground() => ButtonStyle switch
        {
            ButtonStyle.Transparent => Brushes.Transparent,
            ButtonStyle.Translucent => Colors.Button,
            ButtonStyle.Custom => CustomColor,
            _ => Colors.Button
        };

        #region Button Events

        protected override void OnPointerEntered(PointerEventArgs evt)
        {
            base.OnPointerEntered(evt);
            if (Scale) RenderTransform = TransformOperations.Parse($"scale({HoverScale})");
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
                if (Scale) RenderTransform = TransformOperations.Parse($"scale({PressedScale})");
                Background = Colors.Click;

                if (ButtonStyle == ButtonStyle.Translucent) 
                    BorderThickness = new Thickness(1);
            }
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs evt)
        {
            base.OnPointerReleased(evt);
            if (evt.InitialPressMouseButton == MouseButton.Left)
            {
                float value = IsPointerOver ? HoverScale : 1.0f;
                if (Scale) RenderTransform = TransformOperations.Parse($"scale({value})");
                Background = IsPointerOver ? Colors.Hover : null;

                if (ButtonStyle == ButtonStyle.Translucent) 
                    BorderThickness = new Thickness(0);
            }
            
            if (IsPointerOver) onClick?.Invoke();
        }
        
        #endregion
    }
}