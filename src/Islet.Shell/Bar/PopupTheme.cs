using System.IO;
using System.Text.Json;
using System.Windows.Media;
using Islet.Core.Bar;
namespace Islet.Shell.Bar;
internal static class PopupTheme
{
    private static readonly Lazy<Dictionary<string,JsonElement>> DarkTokens=new(()=>Load("dark"));
    private static readonly Lazy<Dictionary<string,JsonElement>> LightTokens=new(()=>Load("light"));
    private static Dictionary<string,JsonElement> Load(string theme)
    {
        using var file=typeof(PopupTheme).Assembly.GetManifestResourceStream($"Islet.Shell.Bar.{theme}-theme.json")??throw new InvalidDataException("Built-in theme missing");
        using var document=JsonDocument.Parse(file);
        var tokens=new Dictionary<string,JsonElement>(StringComparer.Ordinal);
        foreach(var property in document.RootElement.EnumerateObject())tokens.Add(property.Name,property.Value.Clone());
        return tokens;
    }
    public static Brush Get(string token,BarTheme theme)
    {
        var tokens=theme==BarTheme.Light?LightTokens.Value:DarkTokens.Value;
        var color=(Color)ColorConverter.ConvertFromString(tokens[token].GetString()!);var brush=new SolidColorBrush(color);if(token=="surface")brush.Opacity=tokens["surfaceOpacity"].GetDouble();brush.Freeze();return brush;
    }
}

