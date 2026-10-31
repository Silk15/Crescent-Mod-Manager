using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Crescent.UI
{
    public class ConflictDialog : ResponseDialog
    {
        public ConflictDialog(string windowTitle, string promptText) : base(windowTitle, 180)
        {
            StackPanel panel = GetBasePanel(promptText);
            StackPanel buttonPanel = GetButtonPanel();

            CrescentButton overwriteAllButton = new(new SolidColorBrush(Avalonia.Media.Colors.DarkRed))
            {
                Content = "Overwrite All",
                Width = 100
            };
            
            overwriteAllButton.onClick += () =>
            {
                InputText = "all";
                Close();
            };

            CrescentButton overwriteButton = new(Colors.Red)
            {
                Content = "Overwrite",
                Width = 90
            };
            
            overwriteButton.onClick += () =>
            {
                InputText = "yes";
                Close();
            };

            CrescentButton skipButton = new(ButtonStyle.Translucent)
            {
                Content = "Skip",
                Width = 90
            };
            
            skipButton.onClick += () =>
            {
                InputText = string.Empty;
                Close();
            };

            buttonPanel.Children.Add(overwriteAllButton);
            buttonPanel.Children.Add(overwriteButton);
            buttonPanel.Children.Add(skipButton);
            panel.Children.Add(buttonPanel);
            Content = panel;
        }
    }
}