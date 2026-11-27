using CS2Optimizer.Core.Mitigations;
using CS2Optimizer.Core.Oosu10;

namespace CS2Optimizer.Core.Services;

/// <summary>
/// Pares (chave, valor) que este programa pode gravar — e, portanto, os únicos
/// que um backup pode restaurar. É a defesa contra um journal.json ou .reg
/// plantado numa pasta portátil gravável: mesmo que o arquivo seja alterado, o
/// processo elevado só devolve valores que o próprio programa sabe escrever.
/// </summary>
public static class RegistryWriteAllowlist
{
    /// <summary>Pares declarados por cada catálogo (usados também pelos testes de sobreposição).</summary>
    public static IReadOnlySet<string> FixCatalogPairs { get; } = BuildFixCatalogPairs();
    public static IReadOnlySet<string> Oosu10Pairs { get; } = BuildOosu10Pairs();

    // Valores que versões antigas escreviam e que ainda podem aparecer em
    // journals de backups antigos. Não entram nos testes de sobreposição porque
    // nenhum catálogo atual os usa mais.
    private static readonly HashSet<string> LegacyPairs = new(StringComparer.OrdinalIgnoreCase)
    {
        @"HKLM\SOFTWARE\Policies\Microsoft\Windows\AppPrivacy|LetAppsRunInBackground"
    };

    private static readonly HashSet<string> Pairs = new HashSet<string>(FixCatalogPairs, StringComparer.OrdinalIgnoreCase)
        .Concat(Oosu10Pairs)
        .Concat(LegacyPairs)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> PathValueKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        Normalize(@"HKCU\Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers"),
        Normalize(@"HKCU\Software\Microsoft\DirectX\UserGpuPreferences")
    };

    public static bool IsAllowed(string path, string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        var normalized = Normalize(path);
        if (Pairs.Contains(Key(normalized, name))) return true;

        // Nomes dinâmicos: o caminho completo do cs2.exe (descoberto na leitura).
        return PathValueKeys.Contains(normalized) &&
               name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
               (name.Contains('\\') || name.Contains('/'));
    }

    public static string Normalize(string path)
    {
        try { return RegistryService.ParsePath(path).Normalized; }
        catch { return ""; }
    }

    private static string Key(string normalizedPath, string name) =>
        normalizedPath + "|" + name.Trim();

    private static HashSet<string> BuildOosu10Pairs()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // Todas as ações, inclusive as de "voltar", que também podem aparecer
        // no journal quando o usuário reverte um ajuste.
        foreach (var tweak in Oosu10Catalog.All)
        {
            foreach (var action in tweak.Enable.Concat(tweak.Restore))
                Add(set, action.Path, action.Name);
        }
        return set;
    }

    private static HashSet<string> BuildFixCatalogPairs()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // Valores que não existem no catálogo O&O.
        Add(set, @"HKLM\SOFTWARE\Microsoft\Windows\Dwm", "OverlayTestMode");
        Add(set, @"HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Power", "HiberbootEnabled");
        Add(set, @"HKCU\Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled");
        Add(set, @"HKCU\System\GameConfigStore", "GameDVR_Enabled");
        Add(set, @"HKCU\Software\Microsoft\GameBar", "AutoGameModeEnabled");
        Add(set, @"HKCU\Software\Microsoft\GameBar", "AllowAutoGameMode");
        Add(set, @"HKLM\SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode");
        Add(set, @"HKCU\Control Panel\Mouse", "MouseSpeed");
        Add(set, @"HKCU\Control Panel\Mouse", "MouseThreshold1");
        Add(set, @"HKCU\Control Panel\Mouse", "MouseThreshold2");
        Add(set, @"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "SystemResponsiveness");
        Add(set, @"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "NetworkThrottlingIndex");
        Add(set, @"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\cs2.exe\PerfOptions", "CpuPriorityClass");
        Add(set, @"HKLM\SOFTWARE\Policies\Microsoft\Windows\DataCollection", "LimitDumpCollection");
        Add(set, @"HKLM\SOFTWARE\Microsoft\Windows\Windows Error Reporting", "DontSendAdditionalData");
        Add(set, @"HKCU\Software\Microsoft\Speech_OneCore\Settings\OnlineSpeechPrivacy", "HasAccepted");

        // Mitigações de CPU.
        Add(set, MitigationService.MemoryManagementKey, MitigationService.OverrideValue);
        Add(set, MitigationService.MemoryManagementKey, MitigationService.MaskValue);
        Add(set, MitigationService.VirtualizationKey, MitigationService.MinVmValue);

        // VBS / HVCI: valores que o desligamento grava e o desfazer restaura.
        Add(set, MitigationService.DeviceGuardKey, MitigationService.EnableVbsValue);
        Add(set, MitigationService.DeviceGuardKey, MitigationService.RequirePlatformSecurityFeaturesValue);
        Add(set, MitigationService.HvciKey, MitigationService.EnabledValue);
        Add(set, MitigationService.KernelShadowStacksKey, MitigationService.EnabledValue);
        Add(set, MitigationService.CredentialGuardKey, MitigationService.EnabledValue);
        Add(set, MitigationService.LsaKey, MitigationService.LsaCfgFlagsValue);

        return set;
    }

    private static void Add(HashSet<string> set, string path, string name)
    {
        var normalized = Normalize(path);
        if (normalized.Length > 0) set.Add(Key(normalized, name));
    }
}
