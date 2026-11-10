using System.Windows.Media;
using CS2Optimizer.Core.Models;

namespace CS2Optimizer.App.ViewModels;

public sealed class FixItemViewModel : ObservableObject
{
    private bool _isSelected;

    public FixItemViewModel(FixDefinition definition)
    {
        Definition = definition;
    }

    public FixDefinition Definition { get; }

    public string Id => Definition.Id;
    public string Title => Definition.Title;
    public string CurrentState => Definition.CurrentState;
    public string Detail => Definition.Detail;
    public string Explanation => Definition.Explanation;
    public string Category => Definition.Category;
    public FixLevel Level => Definition.Level;
    public bool IsRecommended => Definition.Recommended;
    public bool RequiresReboot => Definition.RequiresReboot;

    public string LevelDisplay => Definition.Level.ToDisplay();
    public Brush LevelBrush => UiBrushes.ForLevel(Definition.Level);
    public string RecommendedText => Definition.Recommended ? "recomendada" : "";
    public string RebootText => Definition.RequiresReboot ? "reinicia" : "";
    public bool HasReboot => Definition.RequiresReboot;

    public string PrivacyGroup => Definition.Id switch
    {
        var id when id.StartsWith("copilot") => "🤖 Copilot & IA",
        var id when id.StartsWith("phone")   => "📱 Dispositivos Móveis",
        var id when id.StartsWith("taskbar") => "📌 Barra de Tarefas",
        var id when id.StartsWith("sync")    => "🔄 Sincronização",
        "privacy-clipboard"                  => "📋 Histórico e Clipboard",
        "privacy-suggestions"                => "💬 Sugestões e Publicidade",
        "privacy-app-caps"                   => "🔒 Permissões de Apps",
        "privacy-cortana"                    => "🔍 Cortana e Busca",
        "privacy-spynet"                     => "🛡️ Defender e Telemetria",
        "privacy-update-p2p"                 => "🔄 Sincronização",
        "privacy-update-defer"               => "🔄 Sincronização",
        "privacy-explorer"                   => "🗂️ Explorer e Interface",
        "privacy-people-taskbar"             => "🗂️ Explorer e Interface",
        "privacy-handwriting"                => "✏️ Entrada e Dados",
        "privacy-bluetooth-ads"              => "✏️ Entrada e Dados",
        "privacy-lockscreen-camera"          => "✏️ Entrada e Dados",
        "privacy-inventory"                  => "✏️ Entrada e Dados",
        "privacy-onesettings"                => "🛡️ Defender e Telemetria",
        "privacy-location-service"           => "📍 Localização",
        "privacy-edge"                       => "🌐 Microsoft Edge",
        "privacy-device-meta"                => "⚙️ Windows Update",
        _                                    => "Outros"
    };

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
