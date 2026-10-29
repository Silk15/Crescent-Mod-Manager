using Avalonia.Controls;

namespace Crescent.UI
{
    public abstract class Card : Border
    {
        public event Action onClick;
    }
}