using Avalonia.Controls;

namespace Crescent.UI
{
    public class SettingsPanel : StackPanel 
    {
        public SettingsPanel()
        {
            Children.Add(new TextBlock()
            {
                Text = "Discord"
            });
            Children.Add(new ToggleSwitch
            {
                Content = new TextBlock
                {
                    Text = "Enable Discord Integration"
                }
            });
        }
    }
}