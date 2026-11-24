using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Silk.Utilities;

namespace Crescent.UI.Controls.Toolbar
{
    public class DiscordButton : CrescentButton
    {
        public DiscordButton() : base(ButtonStyle.Transparent)
        {
            Stream stream = AssetLoader.Open(new Uri("avares://Crescent.UI/Assets/discord.png"));
            Image image = new()
            {
                Source = new Bitmap(stream),
            };
            
            Width = 24;
            Height = 24;
            Content = image;
            RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.HighQuality); 
            onClick += () => "https://discord.gg/Dy5KQF89bp".TryOpen();
        }
    }
}