using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Transformation;
using Crescent.Plugins;

namespace Crescent.UI
{
    public class GameCard : Card
    {
        public event Action<GameCard>? onClick;
        private Image? glowImage;
        private Border? shineOverlay;
        private Border? playtimeBadge;

        private readonly Rotate3DTransform rotateTransform;
        private readonly ScaleTransform scaleTransform;
        private readonly TranslateTransform translateTransform;

        public GameCard(GameData gameData)
        {
            Width = 300;
            Height = 450;
            Background = Colors.Button;
            CornerRadius = new CornerRadius(8);
            Margin = new Thickness(16, 16, 24, 24);
            ClipToBounds = false;
            RenderTransformOrigin = new RelativePoint(0.5, 1.0, RelativeUnit.Relative);

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

            Grid rootGrid = new()
            {
                ClipToBounds = false
            };
            
            string posterPath = Path.Combine(Runtime.PluginPath, gameData.AssemblyName, "Assets", gameData.GameLogo);
            Bitmap? bitmap = null;
            
            if (!string.IsNullOrEmpty(posterPath) && File.Exists(posterPath)) bitmap = new Bitmap(posterPath);
            
            rootGrid.Children.Add(GetGlow(bitmap));
            rootGrid.Children.Add(GetPoster(gameData, bitmap));
            rootGrid.Children.Add(GetPlaytimeBadge(gameData));

            PointerEntered += StartHover;
            PointerExited += StopHover;
            PointerPressed += StartPress;
            PointerReleased += StopPress;
            Child = rootGrid;
        }

        #region UI
        
        public Border GetPoster(GameData gameData, Bitmap? image)
        {

            Border imageCoverBorder = new()
            {
                Width = 300,
                Height = 450,
                Background = Colors.Button,
                CornerRadius = new CornerRadius(8),
                ClipToBounds = true
            };

            Grid posterGrid = new();
            if (image != null)
            {
                posterGrid.Children.Add(new Image
                {
                    Source = image,
                    Stretch = Stretch.UniformToFill
                });
            }
            else
            {
                posterGrid.Children.Add(new TextBlock
                {
                    Text = gameData?.Name ?? "Unknown",
                    FontWeight = FontWeight.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                });
            }

            shineOverlay = new()
            {
                Background = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                    GradientStops = new GradientStops
                    {
                        new GradientStop(Color.FromArgb(0, 255, 255, 255), 0.0),
                        new GradientStop(Color.FromArgb(35, 255, 255, 255), 0.4),
                        new GradientStop(Color.FromArgb(55, 255, 255, 255), 0.5),
                        new GradientStop(Color.FromArgb(35, 255, 255, 255), 0.6),
                        new GradientStop(Color.FromArgb(0, 255, 255, 255), 1.0)
                    }
                },
                Opacity = 0.0,
                IsHitTestVisible = false,
                Transitions = new Transitions
                {
                    new DoubleTransition
                    {
                        Property = OpacityProperty,
                        Duration = TimeSpan.FromMilliseconds(160)
                    }
                }
            };
            
            posterGrid.Children.Add(shineOverlay);
            imageCoverBorder.Child = posterGrid;
            return imageCoverBorder;
        }

        public Image GetGlow(Bitmap? image)
        {
            glowImage = new()
            {
                Width = 310,
                Height = 450,
                Stretch = Stretch.UniformToFill,
                Opacity = 0.0,
                Effect = new BlurEffect
                {
                    Radius = 40,
                },
                RenderTransform = TransformOperations.Parse("translateY(12px) scale(1.02)"),
                Transitions = new Transitions
                {
                    new DoubleTransition
                    {
                        Property = OpacityProperty,
                        Duration = TimeSpan.FromSeconds(0.4)
                    }
                }
            };
            
            if (image != null) glowImage.Source = image;
            return glowImage;
        }

        public Border GetPlaytimeBadge(GameData gameData)
        {
            playtimeBadge = new()
            {
                Background = new SolidColorBrush(Color.FromArgb(235, 22, 22, 22)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(18, 8),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, -14),
                ZIndex = 10,
                RenderTransform = TransformOperations.Parse("translateY(0px)"),
                Transitions = new Transitions
                {
                    new TransformOperationsTransition
                    {
                        Property = RenderTransformProperty,
                        Duration = TimeSpan.FromMilliseconds(160)
                    }
                }
            };
            
            TimeSpan timeSpan = TimeSpan.FromSeconds(gameData.playTime);

            string formattedTime;
            if (timeSpan.TotalHours >= 2) formattedTime = $"{timeSpan.TotalHours:0.#} hours"; 
            else formattedTime = $"{timeSpan.Minutes} minutes";

            playtimeBadge.Child = new TextBlock
            {
                Text = formattedTime,
                Foreground = Brushes.LightGray,
                FontSize = 11,
                FontWeight = FontWeight.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            
            return playtimeBadge;
        }
        
        #endregion

        #region Events

        public void StartHover(object? trigger, PointerEventArgs? eventArgs)
        {
            rotateTransform.AngleX = 1.5;
            scaleTransform.ScaleX = 1.05;
            scaleTransform.ScaleY = 1.05;
            translateTransform.Y = -4;

            Background = Colors.Hover;
            if (glowImage != null) glowImage.Opacity = 0.35;
            if (shineOverlay != null) shineOverlay.Opacity = 0.4;
            if (playtimeBadge != null) playtimeBadge.RenderTransform = TransformOperations.Parse("translateY(-4px)");
        }

        public void StopHover(object? trigger, PointerEventArgs? eventArgs)
        {
            rotateTransform.AngleX = 0;
            scaleTransform.ScaleX = 1.0;
            scaleTransform.ScaleY = 1.0;
            translateTransform.Y = 0;

            Background = Colors.Button;
            if (glowImage != null) glowImage.Opacity = 0.0;
            if (shineOverlay != null) shineOverlay.Opacity = 0.0;
            if (playtimeBadge != null) playtimeBadge.RenderTransform = TransformOperations.Parse("translateY(0px)");
        }

        private void StartPress(object? sender, PointerPressedEventArgs? eventArgs)
        {
            if (eventArgs?.GetCurrentPoint(this).Properties.IsLeftButtonPressed == true)
            {
                rotateTransform.AngleX = -1.5;
                scaleTransform.ScaleX = 0.98;
                scaleTransform.ScaleY = 0.98;
                translateTransform.Y = -1;

                Background = Colors.Click;
                if (playtimeBadge != null) playtimeBadge.RenderTransform = TransformOperations.Parse("translateY(-1px)");
            }
        }

        private void StopPress(object? sender, PointerReleasedEventArgs? eventArgs)
        {
            if (eventArgs?.InitialPressMouseButton == MouseButton.Left)
            {
                rotateTransform.AngleX = -3.5;
                scaleTransform.ScaleX = 1.01;
                scaleTransform.ScaleY = 1.01;
                translateTransform.Y = -4;

                Background = IsPointerOver ? Colors.Hover : Colors.Button;
                if (playtimeBadge != null) playtimeBadge.RenderTransform = TransformOperations.Parse(IsPointerOver ? "translateY(-4px)" : "translateY(0px)");
                onClick?.Invoke(this);
            }
        }
        
        #endregion
    }
}