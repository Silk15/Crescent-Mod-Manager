using Avalonia.Controls;
using Avalonia.Layout;
using Crescent.UI.Controls;

namespace Crescent.UI.Dialogs.Responses
{
    public class ConfirmationDialog : ResponseDialog
    {
        public CrescentTextBox? CrescentTextBox { get; }

        public ConfirmationDialog(string windowTitle, string promptText, bool showCrescentTextBox = true, string defaultValue = "") : base(windowTitle)
        {
            StackPanel panel = GetBasePanel(promptText);
            StackPanel buttonPanel = GetButtonPanel();
            
            if (showCrescentTextBox)
            {
                CrescentTextBox = new CrescentTextBox
                {
                    Text = defaultValue,
                    VerticalAlignment = VerticalAlignment.Center
                };
                panel.Children.Add(CrescentTextBox);
            }

            CrescentButton okButton = new(Colors.Accent)
            {
                Content = "Ok",
                Width = 80
            };
            
            okButton.onClick += () =>
            {
                InputText = CrescentTextBox != null ? CrescentTextBox.Text ?? string.Empty : defaultValue;
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