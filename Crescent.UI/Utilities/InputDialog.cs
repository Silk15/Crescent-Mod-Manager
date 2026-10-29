using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Crescent.UI
{
    public class InputDialog : Window
    {
        public TextBox? textBox;
        
        public string InputText { get; private set; } = "";

        public InputDialog(string windowTitle, string promptText, bool showTextBox, string defaultValue = "")
        {
            bool isOverlap = windowTitle.Contains("Overlap", StringComparison.OrdinalIgnoreCase);
            Title = windowTitle;
            Width = 420;
            Height = showTextBox ? 180 : isOverlap ? 180 : 150;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush(Color.FromArgb(255, 37, 37, 38));
            Foreground = Brushes.White;
            CanResize = false;

            StackPanel panel = new()
            {
                Margin = new Thickness(20),
                Spacing = 12,
                VerticalAlignment = VerticalAlignment.Center
            };

            panel.Children.Add(new TextBlock
            {
                Text = promptText,
                Foreground = Brushes.White,
                FontWeight = FontWeight.SemiBold,
                TextWrapping = TextWrapping.Wrap
            });
            
            StackPanel buttonPanel = new()
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            if (isOverlap)
            {
                CrescentButton overwriteAllButton = new(ButtonStyle.Translucent)
                {
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Content = "Overwrite All",
                    Width = 100,
                    Foreground = Brushes.DarkRed
                };
                overwriteAllButton.Click += (sender, eventArgs) =>
                {
                    InputText = "all";
                    Close();
                };
                
                CrescentButton overwriteButton = new(ButtonStyle.Translucent)
                {
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Content = "Overwrite",
                    Width = 90,
                    Foreground = Brushes.IndianRed
                };
                overwriteButton.Click += (sender, eventArgs) =>
                {
                    InputText = "yes";
                    Close();
                };

                CrescentButton skipButton = new(ButtonStyle.Translucent)
                {
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Content = "Skip",
                    Width = 90,
                    Foreground = Brushes.LightGray
                };
                skipButton.Click += (sender, eventArgs) =>
                {
                    InputText = "";
                    Close();
                };

                buttonPanel.Children.Add(overwriteAllButton);
                buttonPanel.Children.Add(overwriteButton);
                buttonPanel.Children.Add(skipButton);
            }
            else
            {
                if (showTextBox)
                {
                    if (!string.IsNullOrEmpty(defaultValue) || promptText.Contains("select", StringComparison.OrdinalIgnoreCase) == false)
                    {
                        textBox = new TextBox
                        {
                            Text = defaultValue,
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        panel.Children.Add(textBox);
                    }

                    CrescentButton okButton = new(ButtonStyle.Translucent)
                    {
                        HorizontalContentAlignment = HorizontalAlignment.Center,
                        VerticalContentAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Content = "OK",
                        Width = 80
                    };
                    okButton.Click += (sender, eventArgs) =>
                    {
                        InputText = textBox != null ? textBox.Text ?? "" : defaultValue;
                        Close();
                    };

                    CrescentButton cancelButton = new(ButtonStyle.Translucent)
                    {
                        HorizontalContentAlignment = HorizontalAlignment.Center,
                        VerticalContentAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Content = "Cancel",
                        Width = 80
                    };
                    cancelButton.Click += (sender, eventArgs) => Close();

                    buttonPanel.Children.Add(okButton);
                    buttonPanel.Children.Add(cancelButton);
                }
            }

            panel.Children.Add(buttonPanel);
            Content = panel;
        }
    }
}