using Avalonia.Controls;
using Avalonia.Layout;

namespace Crescent.UI
{
    public class ConfirmationDialog : ResponseDialog
    {
        public TextBox? TextBox { get; }

        public ConfirmationDialog(string windowTitle, string promptText, bool showTextBox = true, string defaultValue = "") : base(windowTitle)
        {
            StackPanel panel = GetBasePanel(promptText);
            StackPanel buttonPanel = GetButtonPanel();
            
            if (showTextBox)
            {
                TextBox = new TextBox
                {
                    Text = defaultValue,
                    VerticalAlignment = VerticalAlignment.Center
                };
                panel.Children.Add(TextBox);
            }

            CrescentButton okButton = new(Colors.Green)
            {
                Content = "Ok",
                Width = 80
            };
            
            okButton.onClick += () =>
            {
                InputText = TextBox != null ? TextBox.Text ?? string.Empty : defaultValue;
                Close();
            };

            CrescentButton cancelButton = new(ButtonStyle.Translucent)
            {
                Content = "Cancel",
                Width = 80
            };
            
            cancelButton.onClick += () => Close();
            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            panel.Children.Add(buttonPanel);
            Content = panel;
        }
    }
}