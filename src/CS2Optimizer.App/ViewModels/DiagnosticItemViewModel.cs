using System.Windows;
using System.Windows.Media;
using CS2Optimizer.Core.Models;

namespace CS2Optimizer.App.ViewModels;

public sealed class DiagnosticItemViewModel
{
    public DiagnosticItemViewModel(DiagnosticEntry entry)
    {
        Kind = entry.Kind;
        Text = entry.Text;
        Display = Prefix(entry.Kind) + entry.Text;
        Foreground = entry.Kind switch
        {
            DiagnosticKind.Section => UiBrushes.Accent,
            DiagnosticKind.Ok => UiBrushes.Safe,
            DiagnosticKind.Warn => UiBrushes.Warning,
            DiagnosticKind.Bad => UiBrushes.Risky,
            DiagnosticKind.Todo => UiBrushes.Muted,
            _ => UiBrushes.Text
        };
        Weight = entry.Kind == DiagnosticKind.Section ? FontWeights.SemiBold : FontWeights.Normal;
        TopMargin = entry.Kind == DiagnosticKind.Section ? 14 : 1;
        Margin = new Thickness(4, TopMargin, 4, 1);
    }

    public DiagnosticKind Kind { get; }
    public string Text { get; }
    public string Display { get; }
    public Brush Foreground { get; }
    public FontWeight Weight { get; }
    public double TopMargin { get; }
    public Thickness Margin { get; }

    private static string Prefix(DiagnosticKind kind) => kind switch
    {
        DiagnosticKind.Section => "",
        DiagnosticKind.Ok => "✓  ",
        DiagnosticKind.Warn => "!  ",
        DiagnosticKind.Bad => "✗  ",
        DiagnosticKind.Todo => "☐  ",
        _ => "•  "
    };
}

public sealed class LogItemViewModel
{
    public LogItemViewModel(LogEntry entry)
    {
        Time = entry.Time;
        Text = entry.Text;
        Level = entry.Level;
        Display = $"{entry.Time:HH:mm:ss}  {entry.Text}";
        Foreground = UiBrushes.ForLog(entry.Level);
    }

    public DateTime Time { get; }
    public string Text { get; }
    public LogLevel Level { get; }
    public string Display { get; }
    public Brush Foreground { get; }
}

public sealed class ChecklistItemViewModel : ObservableObject
{
    private bool _isDone;

    public ChecklistItemViewModel(string text)
    {
        Text = text;
    }

    public string Text { get; }

    public bool IsDone
    {
        get => _isDone;
        set => SetProperty(ref _isDone, value);
    }
}
