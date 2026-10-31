using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Crescent.Plugins;

namespace Crescent.UI
{
    public class LibraryPanel : WrapPanel
    {
        public event Action<GameData>? onGameClicked;
        
        public LibraryPanel()
        {
            Orientation = Orientation.Horizontal;
            Margin = new Thickness(30, 25, 30, 30);
            HorizontalAlignment = HorizontalAlignment.Left;
            VerticalAlignment = VerticalAlignment.Top;

            foreach (GameData configuredGame in GameData.InstalledGames)
            {
                GameCard card = SetupGame(configuredGame);
                card.onClick += _ => onGameClicked?.Invoke(configuredGame);
                Children.Add(card);
            }
        }
        
        public GameCard SetupGame(GameData game) => new(game);
    }
}