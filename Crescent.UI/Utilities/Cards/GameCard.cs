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
        public event Action<GameCard> onClick;
        
        public GameCard(GameData game)
        {
            Width = 300;
            Height = 505;
            Background = Colors.Button;
            CornerRadius = new CornerRadius(8);
            Margin = new Thickness(0, 0, 20, 20);
            Transitions = new Transitions
            {
                new TransformOperationsTransition
                {
                    Property = RenderTransformProperty,
                    Duration = TimeSpan.FromSeconds(0.1)
                }
            };

            Border imageCoverBorder = new()
            {
                Height = 450,
                Background = Colors.Button,
                CornerRadius = new CornerRadius(8, 8, 0, 0),
                ClipToBounds = true
            };

            GetPoster(game, ref imageCoverBorder);
            TextBlock gameTitle = new()
            {
                Text = game.Name,
                FontWeight = FontWeight.Bold,
                Margin = new Thickness(12, 10, 12, 2),
                
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            StackPanel posterPanel = new();
            posterPanel.Children.Add(imageCoverBorder);
            posterPanel.Children.Add(gameTitle);
            posterPanel.Children.Add(new Separator
            {
                Width = 250,
                Height = 2,
                Background = Colors.Separator
            });

            PointerEntered += StartHover;
            PointerExited += StopHover;

            PointerPressed += StartPress;
            PointerReleased += StopPress;

            Child = posterPanel;
        }

        public void GetPoster(GameData game, ref Border imageCoverBorder)
        {
            string posterPath = Path.Combine(Runtime.PluginPath, game.AssemblyName, "Assets", game.GameLogo);
            if (File.Exists(posterPath))
            {
                Image gamePosterImage = new()
                {
                    Source = new Bitmap(posterPath),
                    Stretch = Stretch.Uniform
                };

                imageCoverBorder.Child = gamePosterImage;
            }
            else
            {
                TextBlock posterTitleOverlay = new()
                {
                    Text = game.Name,
                    FontWeight = FontWeight.Bold,
                    
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                imageCoverBorder.Child = posterTitleOverlay;
            }
        }

        #region Click Events

        public void StartHover(object? trigger, PointerEventArgs eventArgs)
        {
            RenderTransform = TransformOperations.Parse("scale(1.05)");
            Background = Colors.Hover;
        }

        public void StopHover(object? trigger, PointerEventArgs eventArgs)
        {
            RenderTransform = TransformOperations.Parse("scale(1.0)");
            Background = Colors.Button;
        }

        private void StartPress(object? sender, PointerPressedEventArgs eventArgs)
        {
            if (eventArgs.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                RenderTransform = TransformOperations.Parse("scale(0.95)");
                Background = Colors.Click;
            }
        }

        private void StopPress(object? sender, PointerReleasedEventArgs eventArgs)
        {
            if (eventArgs.InitialPressMouseButton == MouseButton.Left)
            {
                float value = IsPointerOver ? 1.05f : 1.0f;
                RenderTransform = TransformOperations.Parse($"scale({value})");
                Background = IsPointerOver ? Colors.Hover : Colors.Button;
                onClick?.Invoke(this);
            }
        }
        
        #endregion
    }
}