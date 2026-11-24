using Avalonia.Media.Fonts;

namespace Crescent.UI
{
    public class Font : EmbeddedFontCollection
    {
        public Font() : base(new("fonts:Crescent", UriKind.Absolute), new Uri("avares://Crescent.UI/Assets/Fonts/Nunito-VariableFont_wght.ttf", UriKind.Absolute))
        {
            
        }
    }
}