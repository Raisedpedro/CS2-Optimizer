using System.Windows.Media;
using CS2Optimizer.Core.Mitigations;

namespace CS2Optimizer.App.ViewModels;

public sealed class MitigationProfileViewModel : ObservableObject
{
    private bool _isSelected;

    public MitigationProfileViewModel(MitigationProfile profile, bool isCompatible, string explanation)
    {
        Profile = profile;
        IsCompatible = isCompatible;
        Explanation = explanation;
    }

    public MitigationProfile Profile { get; }
    public string Key => Profile.Key;
    public string Name => Profile.Name;
    public string Summary => Profile.Summary;
    public string SecurityDisplay => Profile.SecurityDisplay;
    public MitigationSecurity Security => Profile.Security;
    public bool IsReduced => Profile.Security == MitigationSecurity.Reduzida;
    public bool IsHigher => Profile.Security == MitigationSecurity.Maior;
    public Brush SecurityBrush => UiBrushes.ForSecurity(Profile.Security);
    public string AppliesToText => "Vale para: " + string.Join(" / ", Profile.AppliesTo);
    public bool IsCompatible { get; }
    public bool IsBlocked => !IsCompatible;
    public string BlockReason => IsCompatible ? "" : "Indisponível para o fabricante da sua CPU.";
    public string Explanation { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (!IsCompatible && value) return;
            SetProperty(ref _isSelected, value);
        }
    }
}
