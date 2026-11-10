using System.Windows.Media;
using CS2Optimizer.Core.Mitigations;
using CS2Optimizer.Core.Models;

namespace CS2Optimizer.App.ViewModels;

public static class UiBrushes
{
    public static readonly Brush Safe = Freeze("#22C55E");
    public static readonly Brush Optional = Freeze("#8A8A96");
    public static readonly Brush Caution = Freeze("#EAB308");
    public static readonly Brush Danger = Freeze("#EF4444");
    public static readonly Brush Risky = Freeze("#F87171");
    public static readonly Brush Accent = Freeze("#60A5FA");
    public static readonly Brush Muted = Freeze("#A1A1AA");
    public static readonly Brush Dim = Freeze("#71717A");
    public static readonly Brush Text = Freeze("#E4E4E7");
    public static readonly Brush Warning = Freeze("#EAB308");

    private static Brush Freeze(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }

    public static Brush ForLevel(FixLevel level) => level switch
    {
        FixLevel.Safe => Safe,
        FixLevel.Optional => Optional,
        FixLevel.Destructive => Danger,
        FixLevel.Risky => Risky,
        _ => Text
    };

    public static Brush ForSecurity(MitigationSecurity security) => security switch
    {
        MitigationSecurity.Padrao => Safe,
        MitigationSecurity.Maior => Caution,
        MitigationSecurity.Reduzida => Risky,
        _ => Text
    };

    public static Brush ForLog(LogLevel level) => level switch
    {
        LogLevel.Ok => Safe,
        LogLevel.Warn => Warning,
        LogLevel.Bad => Risky,
        LogLevel.Title => Accent,
        _ => Muted
    };
}
