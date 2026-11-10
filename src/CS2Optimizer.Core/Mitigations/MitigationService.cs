using Microsoft.Win32;
using CS2Optimizer.Core.Models;
using CS2Optimizer.Core.Services;

namespace CS2Optimizer.Core.Mitigations;

/// <summary>
/// Mitigações de CPU (Spectre/Meltdown/MDS...), baseadas no KB4073119.
/// Só mexe em FeatureSettingsOverride, FeatureSettingsOverrideMask e
/// MinVmVersionForCpuBasedMitigations. Voltar ao padrão = APAGAR os valores.
/// </summary>
public sealed class MitigationService
{
    public const string MemoryManagementKey =
        @"HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management";
    public const string VirtualizationKey =
        @"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Virtualization";

    public const string OverrideValue = "FeatureSettingsOverride";
    public const string MaskValue = "FeatureSettingsOverrideMask";
    public const string MinVmValue = "MinVmVersionForCpuBasedMitigations";

    private readonly RegistryService _registry;

    public MitigationService(RegistryService registry)
    {
        _registry = registry;
    }

    public static IReadOnlyList<MitigationProfile> Profiles { get; } = new List<MitigationProfile>
    {
        new("padrao",
            "Padrão do Windows (recomendado)",
            "Apaga as sobrescritas: o Windows volta a decidir sozinho o que ligar conforme a sua CPU.",
            "Apaga as sobrescritas e devolve o Windows ao comportamento de fábrica: Spectre v2, Meltdown, MDS/TAA e as demais proteções ficam ativas conforme a CPU e o microcódigo exigem (CPUs novas já trazem várias em hardware e quase não pagam custo). É o ponto de partida mais seguro.",
            0, true, new[] { "Intel", "AMD", "ARM" }, false, MitigationSecurity.Padrao),

        new("desligar-v2",
            "Desligar só a proteção Spectre v2 (CVE-2017-5715)",
            "Desliga o isolamento da previsão de desvios (Spectre v2). Pode render um pouco em CPU antiga; deixa o PC mais exposto.",
            "Desliga a proteção contra Spectre v2, que impede um programa de influenciar a previsão de desvios da CPU para espiar memória de outros. Pode devolver desempenho em CPUs mais antigas (onde a proteção é por software/microcódigo); em CPUs recentes o ganho tende a ser pequeno. Um código malicioso rodando no PC poderia, em teoria, ler dados de outros processos.",
            1, false, new[] { "Intel", "AMD" }, false, MitigationSecurity.Reduzida),

        new("desligar-tudo",
            "Desativar TODAS as mitigações possíveis (máximo FPS)",
            "Grava o valor 3 do KB4073119: desliga Spectre v2 e Meltdown. É o máximo que o registro do Windows permite desligar.",
            "Grava FeatureSettingsOverride = 3, o ajuste clássico de \"desligar tudo\" (o mesmo do InSpectre). Ele desliga as duas proteções que o Windows deixa controlar por software: Spectre v2 (CVE-2017-5715) e Meltdown (CVE-2017-5754). As demais — MDS/TAA/L1TF, SSBD, BHI e Inception — são decididas pelo microcódigo da CPU e por outros bits, e NÃO são desligadas por este valor; em CPU moderna várias já rodam em hardware e não custam FPS. O ganho aparece em CPU antiga (Haswell e anteriores), onde a proteção é feita por software. Em troca, qualquer código malicioso que rode no PC fica com caminho mais fácil para ler memória do kernel e de outros processos. Só faz sentido em PC dedicado a jogo, sem dados sensíveis. Meça o FPS antes e depois: se não subir, volte ao padrão.",
            3, false, new[] { "Intel", "AMD" }, false, MitigationSecurity.Reduzida),

        new("ssbd",
            "SSBD (CVE-2018-3639) + Spectre v2 + Meltdown",
            "LIGA a proteção Speculative Store Bypass (desligada por padrão). Mais seguro, mais lento: não ajuda FPS.",
            "Liga o SSBD (Speculative Store Bypass Disable), que no Windows vem DESLIGADO por padrão. Aumenta a segurança e custa desempenho de CPU. É o contrário de otimizar FPS.",
            8, false, new[] { "Intel", "AMD" }, false, MitigationSecurity.Maior),

        new("amd-arm-v2",
            "AMD/ARM: Spectre v2 usuário->kernel",
            "Liga a proteção Spectre v2 entre usuário e kernel. Mais seguro, algum custo de desempenho.",
            "Liga a proteção Spectre v2 na passagem do modo usuário para o kernel em CPUs AMD/ARM. Mais segurança, algum custo de desempenho.",
            64, false, new[] { "AMD", "ARM" }, false, MitigationSecurity.Maior),

        new("amd-completo",
            "AMD: Spectre v2 usuário->kernel + SSBD",
            "Liga Spectre v2 usuário->kernel e SSBD. Mais seguro, mais lento.",
            "Liga Spectre v2 usuário->kernel e SSBD em CPUs AMD. Mais segurança, custo de desempenho. Com o papel Hyper-V instalado, o KB exige também MinVmVersionForCpuBasedMitigations (o programa grava).",
            72, false, new[] { "AMD" }, true, MitigationSecurity.Maior),

        new("intel-full",
            "Intel: TAA + MDS + L1TF + SSBD, mantendo Hyper-Threading",
            "Reforça as proteções MDS/TAA/L1TF e SSBD. Mais seguro, mais lento.",
            "Reforça as proteções contra MDS, TAA e L1TF e liga SSBD, mantendo o Hyper-Threading. Pensado para servidores/VMs: custo de desempenho e nenhum ganho de FPS.",
            72, false, new[] { "Intel" }, true, MitigationSecurity.Maior),

        new("intel-full-semht",
            "Intel: TAA + MDS + L1TF + SSBD, com Hyper-Threading DESLIGADO",
            "Como o perfil acima, mas DESLIGA o Hyper-Threading: perde threads e costuma piorar o jogo.",
            "Como intel-full, mas desliga o Hyper-Threading (metade das threads lógicas). Máxima proteção contra MDS/L1TF, e quase sempre PIORA o desempenho no CS2 e em multitarefa (Discord/OBS).",
            8264, false, new[] { "Intel" }, true, MitigationSecurity.Maior),

        new("intel-bhi",
            "Intel: Branch History Injection (CVE-2022-0001)",
            "LIGA a proteção BHI (opt-in). Mais seguro, algum custo.",
            "Liga a proteção contra Branch History Injection, que é opt-in no Windows. Mais segurança, algum custo de desempenho.",
            8388608, false, new[] { "Intel" }, false, MitigationSecurity.Maior),

        new("intel-completo",
            "Intel: TAA + MDS + L1TF + SSBD + HT desligado + BHI",
            "Todas as proteções Intel, com Hyper-Threading desligado. Máximo de segurança, pior desempenho.",
            "Liga todas as proteções Intel documentadas (MDS/TAA/L1TF, SSBD, BHI) e desliga o Hyper-Threading. Máximo de segurança e pior desempenho.",
            8396872, false, new[] { "Intel" }, true, MitigationSecurity.Maior),

        new("amd-btc",
            "AMD: Branch Type Confusion (CVE-2022-23825)",
            "LIGA a proteção BTC em CPUs AMD afetadas. Mais seguro, algum custo.",
            "Liga a proteção contra Branch Type Confusion em CPUs AMD afetadas. Mais segurança, algum custo de desempenho.",
            16777280, false, new[] { "AMD" }, false, MitigationSecurity.Maior),

        new("amd-inception",
            "AMD: Return Address Predictor / Inception (CVE-2023-20569)",
            "LIGA a proteção Inception em CPUs AMD Zen 3/4 afetadas. Mais seguro, custo perceptível em alguns casos.",
            "Liga a proteção contra Inception (Return Address Predictor) em CPUs AMD afetadas. Mais segurança, com custo de desempenho que pode ser perceptível.",
            67108928, false, new[] { "AMD" }, false, MitigationSecurity.Maior)
    };

    public static IReadOnlyList<(uint Hex, string Name)> Bits { get; } = new List<(uint, string)>
    {
        (0x00000001, "Spectre v2 DESLIGADO (CVE-2017-5715)"),
        (0x00000002, "Meltdown DESLIGADO (CVE-2017-5754)"),
        (0x00000008, "SSBD ligado (CVE-2018-3639)"),
        (0x00000040, "Spectre v2 usuário->kernel (AMD/ARM); em Intel, junto com o bit 8 = bloco MDS/TAA/L1TF do KB"),
        (0x00002000, "Hyper-Threading desligado (parte do valor 8264 do KB)"),
        (0x00800000, "BHI ligado (CVE-2022-0001)"),
        (0x01000000, "AMD Branch Type Confusion ligado (CVE-2022-23825)"),
        (0x04000000, "AMD Inception ligado (CVE-2023-20569)")
    };

    public static IReadOnlyList<string> WindowsDefaultLines { get; } = new[]
    {
        "Spectre v2 (CVE-2017-5715) ..... ATIVO",
        "Meltdown (CVE-2017-5754) ....... depende da CPU (AMD e Intel 9ª geração ou mais nova já resolvem em hardware)",
        "Spectre v1 (CVE-2017-5753) ..... ATIVO, sem opção de desligar",
        "SSBD (CVE-2018-3639) ........... DESLIGADO",
        "MDS / TAA / MMIO ............... ATIVO (só em CPU afetada; nas novas roda em hardware)",
        "BHI (CVE-2022-0001) ............ DESLIGADO (opt-in)"
    };

    public static MitigationProfile? FindProfile(string key) =>
        Profiles.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));

    public static MitigationResolution Resolve(IEnumerable<string> names)
    {
        var keys = new List<string>();
        foreach (var raw in names)
        {
            foreach (var part in (raw ?? "").Split(new[] { ',', ';', ' ', '\t', '\r', '\n' },
                         StringSplitOptions.RemoveEmptyEntries))
            {
                var key = part.Trim().ToLowerInvariant();
                if (key.Length > 0 && !keys.Contains(key)) keys.Add(key);
            }
        }

        if (keys.Count == 0) return MitigationResolution.Fail("Nenhum perfil informado.");

        foreach (var key in keys)
        {
            if (FindProfile(key) is null)
                return MitigationResolution.Fail("Perfil desconhecido: " + key);
        }

        if (keys.Contains("padrao") && keys.Count > 1)
            return MitigationResolution.Fail("O perfil 'padrao' apaga as sobrescritas e não combina com outros.");

        uint value = 0;
        var applies = new[] { "Intel", "AMD", "ARM" }.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var minVm = false;
        var reduced = false;
        var higher = false;
        var explanations = new List<string>();

        foreach (var key in keys)
        {
            var profile = FindProfile(key)!;
            value |= profile.Value;
            applies.IntersectWith(profile.AppliesTo);
            if (profile.MinVm) minVm = true;
            if (profile.Security == MitigationSecurity.Reduzida) reduced = true;
            if (profile.Security == MitigationSecurity.Maior) higher = true;
            explanations.Add("- " + profile.Name + ": " + profile.Explanation);
        }

        if (reduced && higher)
            return MitigationResolution.Fail(
                "Não misture perfis que REDUZEM a proteção com perfis que a AUMENTAM: o resultado não é documentado.");

        if (applies.Count == 0)
            return MitigationResolution.Fail("Estes perfis são de fabricantes diferentes e não combinam.");

        var security = MitigationSecurity.Padrao;
        if (reduced) security = MitigationSecurity.Reduzida;
        else if (higher) security = MitigationSecurity.Maior;

        var first = FindProfile(keys[0])!;
        var name = first.Name;
        var summary = first.Summary;
        if (keys.Count > 1)
        {
            name = "Combinação: " + string.Join(" + ", keys);
            summary = "Combinação de perfis (veja a explicação completa ao confirmar).";
        }

        return new MitigationResolution(
            null, keys, name, summary, value, first.Erase, security,
            applies.ToArray(), minVm, string.Join(Environment.NewLine, explanations));
    }

    public MitigationState GetState(CpuInfo cpu)
    {
        var overrideSnapshot = _registry.GetValue(MemoryManagementKey, OverrideValue);
        var maskSnapshot = _registry.GetValue(MemoryManagementKey, MaskValue);
        var minVmSnapshot = _registry.GetValue(VirtualizationKey, MinVmValue);

        return DecodeState(cpu, overrideSnapshot, maskSnapshot, minVmSnapshot);
    }

    /// <summary>Decodifica o estado a partir de snapshots, sem tocar no registro (testável).</summary>
    public static MitigationState DecodeState(
        CpuInfo cpu,
        RegistryValueSnapshot overrideSnapshot,
        RegistryValueSnapshot maskSnapshot,
        RegistryValueSnapshot minVmSnapshot)
    {
        long? overrideValue = overrideSnapshot.Exists && overrideSnapshot.Value is not null
            ? ToInt64(overrideSnapshot.Value) : null;
        long? mask = maskSnapshot.Exists && maskSnapshot.Value is not null
            ? ToInt64(maskSnapshot.Value) : null;
        string? minVm = minVmSnapshot.Exists
            ? minVmSnapshot.Value as string ?? Convert.ToString(minVmSnapshot.Value) : null;

        var vendor = DetectVendor(cpu);
        var ov = overrideValue is null ? (long?)null : overrideValue.Value & 0xFFFFFFFFL;

        var isDefault = overrideValue is null && mask is null && minVm is null;
        var effective = overrideValue is not null && mask is not null && mask.Value != 0;
        var reduced = effective && ((ov ?? 0) & 3) != 0;

        var warning = "";
        if (overrideValue is not null)
        {
            if (mask is null)
                warning = "FeatureSettingsOverrideMask AUSENTE: sem ele o Windows pode ignorar o Override; o estado real tende a ser o padrão.";
            else if (mask.Value == 0)
                warning = "FeatureSettingsOverrideMask = 0: o Override provavelmente não tem efeito; o estado real tende a ser o padrão.";
            else if (mask.Value != 3)
                warning = $"FeatureSettingsOverrideMask = {mask.Value}, diferente do 3 documentado pela Microsoft; o efeito pode não ser o esperado.";
        }

        var matching = new List<string>();
        var bits = new List<string>();
        if (ov is not null)
        {
            foreach (var profile in Profiles)
            {
                if (!profile.Erase && profile.Value == (uint)ov.Value)
                    matching.Add(profile.Key);
            }

            foreach (var bit in Bits)
            {
                if ((ov.Value & bit.Hex) == bit.Hex) bits.Add(bit.Name);
            }
        }

        return new MitigationState(
            string.IsNullOrWhiteSpace(cpu.Name) ? "desconhecida" : cpu.Name.Trim(),
            vendor, overrideValue, mask, minVm, ov, isDefault, effective, reduced,
            warning, matching, bits);
    }

    public static string DetectVendor(CpuInfo cpu)
    {
        if (cpu.Architecture is 5 or 12) return "ARM";
        if (string.Equals(cpu.Manufacturer, "GenuineIntel", StringComparison.OrdinalIgnoreCase)) return "Intel";
        if (string.Equals(cpu.Manufacturer, "AuthenticAMD", StringComparison.OrdinalIgnoreCase)) return "AMD";
        return "Desconhecido";
    }

    public string GetStateText(MitigationState state)
    {
        var lines = new List<string>
        {
            "ESTADO ATUAL (lido do registro)",
            "",
            $"CPU ................ {state.Cpu}  [{state.Vendor}]"
        };

        if (state.Override is null)
            lines.Add("FeatureSettingsOverride ... (ausente = padrão do Windows)");
        else
            lines.Add($"FeatureSettingsOverride ... {state.Override}  (0x{state.Value ?? 0:X8})");

        lines.Add(state.Mask is null
            ? "FeatureSettingsOverrideMask ... (ausente)"
            : "FeatureSettingsOverrideMask ... " + state.Mask);

        if (state.MinVm is not null)
            lines.Add("MinVmVersionForCpuBasedMitigations ... " + state.MinVm);

        lines.Add("");
        if (state.IsDefault)
            lines.Add("SITUAÇÃO: PADRÃO DO WINDOWS (sem sobrescritas). Nada a fazer se você quer segurança máxima.");
        else if (state.IsReduced)
            lines.Add("SITUAÇÃO: PROTEÇÃO REDUZIDA por sobrescrita (Spectre v2 e/ou Meltdown desligados).");
        else if (state.IsEffective)
            lines.Add("SITUAÇÃO: PERSONALIZADA, com proteções adicionais ligadas (mais seguro que o padrão).");
        else
            lines.Add("SITUAÇÃO: valores presentes, mas possivelmente sem efeito (veja o aviso abaixo).");

        if (state.MatchingProfiles.Count > 0)
            lines.Add("Perfil(is) com este valor: " + string.Join(" ou ", state.MatchingProfiles));
        foreach (var bit in state.Bits) lines.Add("  - " + bit);

        if (!string.IsNullOrWhiteSpace(state.Warning))
        {
            lines.Add("");
            lines.Add("AVISO: " + state.Warning);
        }

        lines.Add("");
        lines.Add("Lembretes:");
        lines.Add("  - O que você muda aqui só passa a valer DEPOIS de reiniciar (o programa nunca reinicia sozinho).");
        lines.Add("  - Para ver o que está REALMENTE em vigor no kernel, no PowerShell (administrador): Install-Module SpeculationControl ; Get-SpeculationControlSettings (módulo oficial da Microsoft, KB4074629).");
        lines.Add("  - Todo valor gravado aqui é salvo antes no backup: o botão Desfazer devolve o estado anterior.");
        return string.Join(Environment.NewLine, lines);
    }

    public string GetWindowsDefaultText()
    {
        var lines = new List<string>
        {
            "PADRÃO DO WINDOWS (sem sobrescritas) - KB4073119",
            ""
        };
        lines.AddRange(WindowsDefaultLines);
        lines.Add("");
        lines.Add("Voltar ao padrão APAGA FeatureSettingsOverride, FeatureSettingsOverrideMask e MinVmVersionForCpuBasedMitigations (não grava 0: gravar 0 é diferente de não ter a chave).");
        lines.Add("Efeito só depois de reiniciar.");
        return string.Join(Environment.NewLine, lines);
    }

    public string GetProfileText(MitigationProfile profile, MitigationState? state)
    {
        var lines = new List<string>
        {
            profile.Name,
            "Perfil: " + profile.Key,
            "Segurança: " + profile.SecurityDisplay,
            "Vale para: " + string.Join(" e ", profile.AppliesTo)
        };

        if (profile.Erase)
            lines.Add("Valor: apaga as chaves de sobrescrita");
        else
            lines.Add($"Valor: FeatureSettingsOverride = {profile.Value} (0x{profile.Value:X8}) ; Mask = 3");

        lines.Add("");
        lines.Add(profile.Explanation);

        if (profile.Security == MitigationSecurity.Reduzida)
        {
            lines.Add("");
            lines.Add("ATENÇÃO: deixa o PC mais exposto que o padrão. Recomendado apenas em PC dedicado a jogo, sem dados sensíveis, e depois de MEDIR se o FPS realmente sobe.");
        }

        if (state is not null && state.Vendor != "Desconhecido" &&
            !profile.AppliesTo.Contains(state.Vendor, StringComparer.OrdinalIgnoreCase))
        {
            lines.Add("");
            lines.Add($"Este perfil é para {string.Join(" e ", profile.AppliesTo)}. Sua CPU é {state.Vendor}: não pode ser marcado.");
        }

        return string.Join(Environment.NewLine, lines);
    }

    public void Apply(IReadOnlyList<string> keys)
    {
        var resolution = Resolve(keys);
        if (!resolution.Success) throw new InvalidOperationException(resolution.Error);

        if (resolution.Erase)
        {
            _registry.DeleteValue(MemoryManagementKey, OverrideValue);
            _registry.DeleteValue(MemoryManagementKey, MaskValue);
            _registry.DeleteValue(VirtualizationKey, MinVmValue);
            return;
        }

        _registry.SetValue(MemoryManagementKey, OverrideValue, unchecked((int)resolution.Value),
            RegistryValueKind.DWord);
        _registry.SetValue(MemoryManagementKey, MaskValue, 3, RegistryValueKind.DWord);

        // O KB exige este valor junto do bloco MDS/TAA quando o papel Hyper-V está instalado.
        if (resolution.MinVm && _registry.KeyExists(@"HKLM\SYSTEM\CurrentControlSet\Services\vmms"))
        {
            _registry.SetValue(VirtualizationKey, MinVmValue, "1.0", RegistryValueKind.String);
        }
    }

    public static long? ToInt64(object? value)
    {
        if (value is null) return null;
        try
        {
            return value switch
            {
                int i => i,
                uint ui => ui,
                long l => l,
                ulong ul => unchecked((long)ul),
                short s => s,
                ushort us => us,
                byte b => b,
                string str when long.TryParse(str, out var parsed) => parsed,
                _ => Convert.ToInt64(value)
            };
        }
        catch
        {
            return null;
        }
    }
}
