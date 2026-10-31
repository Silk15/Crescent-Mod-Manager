using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Crescent.Plugins;

namespace Crescent.UI
{
    public class ConflictReportDialog : Dialog
    {
        public Dictionary<string, List<ModData>> conflicts;
        public bool conflictsResolved;

        public ConflictReportDialog(Dictionary<string, List<ModData>> conflicts)
        {
            this.conflicts = conflicts;
            Title = "Mod File Conflicts Detected!";
            Width = 850;
            Height = 600;
            
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = Colors.MainGradient;
            
            Foreground = Brushes.White;
            CanResize = false;
            
            StackPanel mainPanel = new()
            {
                Margin = new Thickness(20),
                Spacing = 15,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            TextBlock titleBlock = new()
            {
                Text = "File Overwrite & Conflict Resolution",
                FontSize = 22,
                FontWeight = FontWeight.Bold,
                
                TextAlignment = TextAlignment.Center
            };
            
            TextBlock infoBlock = new()
            {
                Text = "The following files are modified by multiple enabled mods. Please review the conflicts below and choose how to proceed.",
                FontSize = 13,
                Foreground = Brushes.LightGray,
                TextWrapping = TextWrapping.Wrap
            };
            
            mainPanel.Children.Add(titleBlock);
            mainPanel.Children.Add(infoBlock);
            mainPanel.Children.Add(GetConflictsScroll());
            mainPanel.Children.Add(GetButtonPanel());
            Content = mainPanel;
        }

        public StackPanel GetConflictsScroll()
        {
            StackPanel conflictListStack = new()
            {
                Spacing = 8
            };

            ScrollViewer conflictScrollViewer = new()
            {
                Content = conflictListStack,
                Height = 400,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            foreach (KeyValuePair<string, List<ModData>> pair in conflicts)
            {
                Border conflictBorder = new()
                {
                    Background = Colors.Button,
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(10)
                };

                StackPanel conflictItemStack = new()
                {
                    Spacing = 5
                };

                conflictItemStack.Children.Add(new TextBlock
                {
                    Text = $"File: {pair.Key}",
                    FontWeight = FontWeight.Bold,
                    Foreground = Brushes.Orange
                });

                StackPanel modsStack = new()
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 10
                };

                modsStack.Children.Add(new TextBlock
                {
                    Text = "Conflicting Mods: ",
                    Foreground = Brushes.LightGray
                });

                for (int index = 0; index < pair.Value.Count; index++)
                {
                    ModData mod = pair.Value[index];
                    modsStack.Children.Add(new TextBlock
                    {
                        Text = mod.DisplayName,
                        
                        FontWeight = FontWeight.SemiBold
                    });
                }

                conflictItemStack.Children.Add(modsStack);
                conflictBorder.Child = conflictItemStack;
                conflictListStack.Children.Add(conflictBorder);
            }

            conflictScrollViewer.Content = conflictListStack;
            return conflictListStack;
        }

        public StackPanel GetButtonPanel()
        {
            StackPanel buttonPanel = new()
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            CrescentButton confirmButton = new(Colors.Green)
            {
                Content = "Proceed with Deployment",
                Width = 170
            };

            confirmButton.onClick += () =>
            {
                conflictsResolved = true;
                Close();
            };

            CrescentButton cancelButton = new(ButtonStyle.Translucent)
            {
                Content = "Cancel",
                Width = 100
            };

            cancelButton.onClick += () =>
            {
                conflictsResolved = false;
                Close();
            };

            buttonPanel.Children.Add(confirmButton);
            buttonPanel.Children.Add(cancelButton);
            return buttonPanel;
        }
    }
}