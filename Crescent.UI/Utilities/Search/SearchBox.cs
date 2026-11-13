using Avalonia.Controls;
using Avalonia.Input;

namespace Crescent.UI.Search
{
    public class SearchBox : TextBox
    {
        #region Button Events

        protected override void OnPointerEntered(PointerEventArgs evt)
        {
            base.OnPointerEntered(evt);
            Background = Colors.Hover;
        }

        protected override void OnPointerExited(PointerEventArgs evt)
        {
            base.OnPointerExited(evt);
            Background = Colors.Button;
        }
        
        #endregion
    }
}