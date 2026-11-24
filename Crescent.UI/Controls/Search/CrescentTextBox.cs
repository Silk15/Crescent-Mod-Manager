using Avalonia.Controls;
using Avalonia.Media;

namespace Crescent.UI
{
    public class CrescentTextBox : TextBox
    {
        protected override Type StyleKeyOverride => typeof(TextBox);
        
        public CrescentTextBox()
        {
            Background = Colors.Button;
            BorderBrush = Colors.Separator;
            
            Resources["TextControlBackgroundPointerOver"] = Colors.Hover;
            Resources["TextControlBorderBrushPointerOver"] = Colors.Separator;
            
            Resources["TextControlBackgroundFocused"] = Colors.Button;
            Resources["TextControlBorderBrushFocused"] = Colors.Accent;
        }
    }
}