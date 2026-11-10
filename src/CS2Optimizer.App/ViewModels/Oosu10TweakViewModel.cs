using System.Windows.Media;
using CS2Optimizer.Core.Oosu10;

namespace CS2Optimizer.App.ViewModels;

public sealed class Oosu10TweakViewModel : ObservableObject
{
    private bool _isSelected;
    private Oosu10State _state;

    public Oosu10TweakViewModel(Oosu10Tweak tweak, Oosu10State state)
    {
        Tweak = tweak;
        _state = state;
    }

    public Oosu10Tweak Tweak { get; }

    public string Id => Tweak.Id;
    public string Title => Tweak.Title;
    public string Detail => Tweak.Detail;
    public string Explanation => Tweak.Explanation;
    public string Note => Tweak.Note;
    public string Group => Tweak.Group;
    public bool InPreset => Tweak.InPreset;
    public bool RequiresReboot => Tweak.RequiresReboot;
    public bool NeedsAdmin => Tweak.NeedsAdmin;
    public Oosu10Advice Advice => Tweak.Advice;

    public int GroupOrder
    {
        get
        {
            var index = Array.IndexOf(Oosu10Groups.Order, Group);
            return index < 0 ? int.MaxValue : index;
        }
    }

    public Oosu10State State
    {
        get => _state;
        set
        {
            if (SetProperty(ref _state, value))
            {
                OnPropertyChanged(nameof(StateText));
                OnPropertyChanged(nameof(StateBrush));
                OnPropertyChanged(nameof(IsActive));
                OnPropertyChanged(nameof(IsPending));
            }
        }
    }

    public string StateText => State switch
    {
        Oosu10State.Ativo => "Ativo",
        Oosu10State.Parcial => "Parcial",
        _ => "Inativo"
    };

    public Brush StateBrush => State switch
    {
        Oosu10State.Ativo => UiBrushes.Safe,
        Oosu10State.Parcial => UiBrushes.Warning,
        _ => UiBrushes.Muted
    };

    public bool IsActive => State == Oosu10State.Ativo;
    public bool IsPending => State != Oosu10State.Ativo;

    public string AdviceDisplay => Advice switch
    {
        Oosu10Advice.Recomendado => "recomendado",
        Oosu10Advice.Opcional => "opcional",
        _ => "evitar"
    };

    public Brush AdviceBrush => Advice switch
    {
        Oosu10Advice.Recomendado => UiBrushes.Safe,
        Oosu10Advice.Opcional => UiBrushes.Optional,
        _ => UiBrushes.Risky
    };

    public string PresetText => InPreset ? "sugerido p/ CS2" : "";
    public bool HasNote => !string.IsNullOrWhiteSpace(Note);
    public string RebootText => RequiresReboot ? "exige reinício" : "";
    public string AdminText => NeedsAdmin ? "HKLM (admin)" : "";

    public string OpsText => string.Join("\n", Tweak.Enable.Select(a => a.Describe()));

    public string RestoreText => Oosu10Service.RestoreActions(Tweak) is { Count: > 0 } actions
        ? string.Join("\n", actions.Select(a => a.Describe()))
        : "—";

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
