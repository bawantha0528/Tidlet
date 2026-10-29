using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Islet.Core;

namespace Islet.Shell;

/// <summary>A plain M0 bootstrap window. This is not the M1 island.</summary>
public sealed class ScaffoldWindow : Window
{
    public ScaffoldWindow()
    {
        Title = $"{ProductInfo.Name} — M0 scaffold";
        Width = 440;
        Height = 240;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.FromRgb(22, 27, 36));
        Foreground = Brushes.White;
        var content = new StackPanel { Margin = new Thickness(28) };
        content.Children.Add(new TextBlock
        {
            Text = ProductInfo.Name,
            FontSize = 26,
            FontWeight = FontWeights.SemiBold
        });
        content.Children.Add(new TextBlock
        {
            Text = "M0 · Project foundation",
            FontSize = 14,
            Margin = new Thickness(0, 12, 0, 12)
        });
        content.Children.Add(new TextBlock
        {
            Text = "The island and live sensors arrive in later milestones.",
            TextWrapping = TextWrapping.Wrap
        });
        Content = content;
    }
}
