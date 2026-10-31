using Avalonia.Controls.Shapes;
using Avalonia.Layout;

namespace Crescent.UI
{
    public class OverlaySeparator : Rectangle
    {
        public OverlaySeparator(Mode mode)
        {
            switch (mode)
            {
                case Mode.Horizontal:
                    Height = 1;
                    Fill = Colors.Separator;
                    VerticalAlignment = VerticalAlignment.Bottom;
                    HorizontalAlignment = HorizontalAlignment.Stretch;
                    break;
                
                case Mode.Vertical:
                    Width = 1;
                    Fill = Colors.Separator;
                    HorizontalAlignment = HorizontalAlignment.Left;
                    VerticalAlignment = VerticalAlignment.Stretch;
                    break;
            }
        }

        public enum Mode
        {
            Horizontal,
            Vertical
        }
    }
}