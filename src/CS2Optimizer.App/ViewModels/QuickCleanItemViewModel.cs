using CS2Optimizer.Core.Models;

namespace CS2Optimizer.App.ViewModels;

/// <summary>Uma linha da aba "Limpeza rápida" (um cache do catálogo).</summary>
public sealed class QuickCleanItemViewModel : ObservableObject
{
    private bool _isSelected;

    public QuickCleanItemViewModel(FixDefinition definition, bool selected)
    {
        Definition = definition;
        _isSelected = selected;
    }

    public FixDefinition Definition { get; }

    public string Id => Definition.Id;
    public string Title => Definition.Title;
    public string Detail => Definition.Detail;
    public string Explanation => Definition.Explanation;
    public string CurrentState => Definition.CurrentState;

    /// <summary>Cache de shader: aparece na lista, mas desmarcado e com aviso.</summary>
    public bool IsShaderCache => string.Equals(Definition.Id, "shaders", System.StringComparison.OrdinalIgnoreCase);

    public string Warning => IsShaderCache
        ? "Desmarcado de propósito: apagar faz o CS2 recompilar tudo e as primeiras partidas travarem. Só limpe depois de trocar o driver de vídeo."
        : "";

    public bool HasWarning => IsShaderCache;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
