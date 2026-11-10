using CS2Optimizer.Core.Services;

namespace CS2Optimizer.Core.Oosu10;

/// <summary>
/// Lê o estado e aplica/reverte os ajustes do O&amp;O ShutUp10 usando o
/// <see cref="RegistryService"/>, então toda escrita passa pelo journal do backup.
/// </summary>
public sealed class Oosu10Service
{
    private readonly RegistryService _registry;

    public Oosu10Service(RegistryService registry)
    {
        _registry = registry;
    }

    public IReadOnlyList<Oosu10Tweak> All => Oosu10Catalog.All;

    public Oosu10State GetState(Oosu10Tweak tweak)
    {
        if (tweak.Enable.Count == 0) return Oosu10State.Inativo;

        var matches = tweak.Enable.Count(action => Matches(_registry.GetValue(action.Path, action.Name), action));
        if (matches == 0) return Oosu10State.Inativo;
        return matches == tweak.Enable.Count ? Oosu10State.Ativo : Oosu10State.Parcial;
    }

    /// <summary>Aplica o estado "ligado" (ou o estado de volta) e devolve o que passou/falhou.</summary>
    public Oosu10ApplyResult Apply(
        IEnumerable<Oosu10Tweak> tweaks,
        bool restore,
        Action<string>? log = null,
        Action<int, int>? progress = null)
    {
        var applied = new List<string>();
        var failed = new List<string>();
        var writes = 0;
        var list = tweaks as IReadOnlyList<Oosu10Tweak> ?? tweaks.ToList();
        var index = 0;

        foreach (var tweak in list)
        {
            var actions = restore ? RestoreActions(tweak) : tweak.Enable;
            var errors = new List<string>();

            foreach (var action in actions)
            {
                try
                {
                    if (action.IsDelete)
                        _registry.DeleteValue(action.Path, action.Name);
                    else
                        _registry.SetValue(action.Path, action.Name, action.Value!, action.Kind);
                    writes++;
                }
                catch (Exception ex)
                {
                    errors.Add(action.Describe() + " -> " + ex.Message);
                    log?.Invoke($"{tweak.Title}: falhou {action.Describe()} -> {ex.Message}");
                }
            }

            if (errors.Count == 0)
            {
                applied.Add(tweak.Title);
            }
            else
            {
                var label = errors.Count == actions.Count
                    ? tweak.Title
                    : tweak.Title + " (parcial)";
                failed.Add(label + " -> " + string.Join("; ", errors));
            }

            progress?.Invoke(++index, list.Count);
        }

        return new Oosu10ApplyResult(applied, failed, writes);
    }

    /// <summary>Estado de volta: o alternativo do arquivo ou, se não houver, remover o valor.</summary>
    public static IReadOnlyList<Oosu10RegAction> RestoreActions(Oosu10Tweak tweak) =>
        tweak.Restore.Count > 0
            ? tweak.Restore
            : tweak.Enable.Select(a => new Oosu10RegAction(a.Path, a.Name, a.Kind, null)).ToArray();

    private static bool Matches(RegistryValueSnapshot current, Oosu10RegAction action)
    {
        if (action.IsDelete) return !current.Exists;
        if (!current.Exists || current.Value is null) return false;

        try
        {
            return action.Kind switch
            {
                Microsoft.Win32.RegistryValueKind.DWord or Microsoft.Win32.RegistryValueKind.QWord =>
                    Convert.ToInt64(current.Value) == Convert.ToInt64(action.Value),
                Microsoft.Win32.RegistryValueKind.String or Microsoft.Win32.RegistryValueKind.ExpandString =>
                    string.Equals(Convert.ToString(current.Value), Convert.ToString(action.Value),
                        StringComparison.OrdinalIgnoreCase),
                _ => false
            };
        }
        catch
        {
            return false;
        }
    }
}
