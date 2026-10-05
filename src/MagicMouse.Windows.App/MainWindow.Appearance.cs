using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Media;

namespace MagicMouse.Windows.App;

public sealed partial class MainWindow
{
    private UIElement VisualChoices(string key, string[] choices, string initial, Action<string>? changed = null)
    {
        var panel = new Grid { ColumnSpacing = 8 };
        var preferences = Preferences;
        var selected = preferences.Choices.GetValueOrDefault(key, initial);
        if (!choices.Contains(selected)) selected = initial;
        var buttons = new List<Button>();
        void Refresh()
        {
            foreach (var button in buttons)
            {
                var active = Equals(button.Tag, selected);
                button.BorderBrush = new SolidColorBrush(ColorFromHex(active ? DesignTokens.Accent : "#DDE3ED"));
                button.Foreground = new SolidColorBrush(ColorFromHex(active ? DesignTokens.Accent : DesignTokens.PrimaryText));
                button.BorderThickness = new Thickness(active ? 2 : 1);
            }
        }
        for (var i = 0; i < choices.Length; i++)
        {
            var choice = choices[i];
            panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var content = new StackPanel { Spacing = 12, HorizontalAlignment = HorizontalAlignment.Center };
            content.Children.Add(key == "appearance.style" ? IconFactory.Pointer(choice) : IconFactory.ClickEffect(choice));
            content.Children.Add(new TextBlock { Text = choice, FontSize = 10, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap });
            var button = new Button { Tag = choice, Content = content, HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center, Padding = new Thickness(4, 14, 4, 12),
                CornerRadius = new CornerRadius(10), Background = new SolidColorBrush(ColorFromHex("#55FFFFFF")), MinHeight = 92 };
            AutomationProperties.SetName(button, choice);
            button.Click += (_, _) => { selected = choice; preferences.Choices[key] = choice; Refresh(); changed?.Invoke(choice); QueueSave(); };
            Grid.SetColumn(button, i);
            buttons.Add(button);
            panel.Children.Add(button);
        }
        Refresh();
        return panel;
    }
}
