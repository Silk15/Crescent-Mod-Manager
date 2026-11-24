using Avalonia.Controls;
using Avalonia.Media;

namespace Crescent.UI
{
    public class CrescentComboBox : ComboBox
    {
        protected override Type StyleKeyOverride => typeof(ComboBox);

        public CrescentComboBox()
        {
            Background = Colors.Button;
            BorderBrush = Colors.Separator;

            Resources["ComboBoxBackgroundPointerOver"] = Colors.Hover;
            Resources["ComboBoxBorderBrushPointerOver"] = Colors.Separator;

            Resources["ComboBoxBackgroundPressed"] = Colors.Button;
            Resources["ComboBoxBorderBrushPressed"] = Colors.Accent;

            Resources["ComboBoxBackgroundFocused"] = Colors.Button;
            Resources["ComboBoxBorderBrushFocused"] = Colors.Accent;

            Resources["ComboBoxDropDownBackground"] = Colors.Button;
            Resources["ComboBoxDropDownBorderBrush"] = Colors.Separator;

            Resources["ComboBoxItemBackgroundPointerOver"] = Colors.Hover;
            Resources["ComboBoxItemBackgroundSelected"] = Colors.Accent;
            Resources["ComboBoxItemBackgroundSelectedPointerOver"] = Colors.Accent;
        }
    }
}