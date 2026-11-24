using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Crescent.UI.Controls
{
    public class CrescentMenuItem : MenuItem
    {
        public event Action? onClick;

        protected override void OnClick(RoutedEventArgs e)
        {
            base.OnClick(e);
            onClick?.Invoke();
        }
    }
}