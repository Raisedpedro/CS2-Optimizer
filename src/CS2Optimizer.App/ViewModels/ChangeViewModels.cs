using System.Windows.Media;

namespace CS2Optimizer.App.ViewModels;

/// <summary>Uma alteração marcada em qualquer aba, mostrada no painel do resumo.</summary>
public sealed class PendingChangeViewModel
{
    public PendingChangeViewModel(string kind, string title, string badge, Brush badgeBrush, string targetTab)
    {
        Kind = kind;
        Title = title;
        Badge = badge;
        BadgeBrush = badgeBrush;
        TargetTab = targetTab;
    }

    public string Kind { get; }
    public string Title { get; }
    public string Badge { get; }
    public Brush BadgeBrush { get; }
    public string TargetTab { get; }
    public string TargetTabText => "Abrir " + AppTabs.LabelOf(TargetTab);
}

/// <summary>Registro do que foi aplicado/revertido nesta sessão.</summary>
public sealed class SessionChangeViewModel
{
    public SessionChangeViewModel(System.DateTime when, string action, string summary, string detail, Brush actionBrush)
    {
        When = when;
        Action = action;
        Summary = summary;
        Detail = detail;
        ActionBrush = actionBrush;
    }

    public System.DateTime When { get; }
    public string TimeText => When.ToString("HH:mm");
    public string Action { get; }
    public string Summary { get; }
    public string Detail { get; }
    public Brush ActionBrush { get; }
    public bool HasDetail => !string.IsNullOrWhiteSpace(Detail);
}
