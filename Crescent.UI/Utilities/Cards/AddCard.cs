using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Crescent.UI
{
    public class AddCard : Card
    {
        public TranslateTransform translateTransform;
        public Rotate3DTransform rotateTransform;
        public ScaleTransform scaleTransform;
       
        public event Action<AddCard>? onClick;

        public AddCard()
        {
            Width = 300;
            Height = 450;
            CornerRadius = new CornerRadius(8);
            Margin = new Thickness(16, 16, 24, 24);
            ClipToBounds = false;
            RenderTransformOrigin = new RelativePoint(0.5, 1.0, RelativeUnit.Relative);
            Background = Brushes.Transparent;

            rotateTransform = new Rotate3DTransform
            {
                AngleX = 0,
                CenterX = 150,
                CenterY = 450,
                Depth = 600,
                Transitions = new Transitions
                {
                    new DoubleTransition
                    {
                        Property = Rotate3DTransform.AngleXProperty,
                        Duration = TimeSpan.FromMilliseconds(160)
                    }
                }
            };

            scaleTransform = new ScaleTransform
            {
                ScaleX = 1.0,
                ScaleY = 1.0,
                Transitions = new Transitions
                {
                    new DoubleTransition
                    {
                        Property = ScaleTransform.ScaleXProperty,
                        Duration = TimeSpan.FromMilliseconds(160)
                    },
                    new DoubleTransition
                    {
                        Property = ScaleTransform.ScaleYProperty,
                        Duration = TimeSpan.FromMilliseconds(160)
                    }
                }
            };

            translateTransform = new TranslateTransform
            {
                X = 0,
                Y = 0,
                Transitions = new Transitions
                {
                    new DoubleTransition
                    {
                        Property = TranslateTransform.YProperty,
                        Duration = TimeSpan.FromMilliseconds(160)
                    }
                }
            };

            RenderTransform = new TransformGroup
            {
                Children =
                {
                    rotateTransform,
                    scaleTransform,
                    translateTransform
                }
            };

            PointerEntered += StartHover;
            PointerExited += StopHover;
            PointerPressed += StartPress;
            PointerReleased += StopPress;
            Child = GetPoster();
        }

        #region UI

        public Border GetPoster()
        {
            Bitmap image = new(AssetLoader.Open(new("avares://Crescent.UI/Assets/add_cover.png")));
            Border imageCoverBorder = new()
            {
                Width = 300,
                Height = 450
            };

            imageCoverBorder.Child = new Image
            {
                Source = image,
                Stretch = Stretch.UniformToFill
            };

            return imageCoverBorder;
        }

        #endregion

        #region Events

        public void StartHover(object? trigger, PointerEventArgs? eventArgs)
        {
            rotateTransform.AngleX = 1.5;
            scaleTransform.ScaleX = 1.05;
            scaleTransform.ScaleY = 1.05;
            translateTransform.Y = -4;
        }

        public void StopHover(object? trigger, PointerEventArgs? eventArgs)
        {
            rotateTransform.AngleX = 0;
            scaleTransform.ScaleX = 1.0;
            scaleTransform.ScaleY = 1.0;
            translateTransform.Y = 0;
        }

        private void StartPress(object? sender, PointerPressedEventArgs? eventArgs)
        {
            if (eventArgs?.GetCurrentPoint(this).Properties.IsLeftButtonPressed == true)
            {
                rotateTransform.AngleX = 1.5;
                scaleTransform.ScaleX = 1.0;
                scaleTransform.ScaleY = 1.0;
                translateTransform.Y = -4;
            }
        }

        private void StopPress(object? sender, PointerReleasedEventArgs? eventArgs)
        {
            if (eventArgs?.InitialPressMouseButton == MouseButton.Left)
            {
                if (IsPointerOver)
                {
                    rotateTransform.AngleX = 1.5;
                    scaleTransform.ScaleX = 1.05;
                    scaleTransform.ScaleY = 1.05;
                    translateTransform.Y = -4;
                }
                else
                {
                    rotateTransform.AngleX = 0;
                    scaleTransform.ScaleX = 1.0;
                    scaleTransform.ScaleY = 1.0;
                    translateTransform.Y = 0;
                }

                onClick?.Invoke(this);
            }
        }
        
        #endregion
    }
}