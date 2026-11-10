using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using CS2Optimizer.Core.Mitigations;
using CS2Optimizer.Core.Models;

namespace CS2Optimizer.Core.Fixes;

public static class FixCategories
{
    public const string Video = "Vídeo/GPU";
    public const string Energia = "Energia";
    public const string Sistema = "Sistema";
    public const string Rede = "Rede";
    public const string Cpu = "CPU";
    public const string Limpeza = "Limpeza";
    public const string Privacidade = "Privacidade";
    public const string Reparo = "Reparo";
    public const string Cs2 = "CS2";
}

/// <summary>
/// Catálogo de correções (seção 7 do plano). Cada item só é criado se estiver
/// aplicável ao retrato da máquina — é isso que faz o item desaparecer depois
/// de aplicado. Privacidade/telemetria do Windows NÃO mora aqui: mora no
/// catálogo do O&O ShutUp10 (<c>Oosu10Catalog</c>), que lê o estado real de
/// cada ajuste. Não recrie essas correções aqui.
/// </summary>
public static class FixCatalog
{
    // Só entra aqui item Opcional que representa ESTADO pendente (ex.: aceleração
    // do mouse ligada). Tarefas de limpeza (trim/shaders/temp/syscache) ficam de
    // fora: elas sempre podem rodar, então nunca "zeram" depois de aplicadas.
    private static readonly string[] RecommendedOptionalIds = {
        "mouse"
    };

    public static IReadOnlyList<FixDefinition> Build(FixContext ctx)
    {
        var fixes = new List<FixDefinition>();

        // ---------------- MPO / BCD ----------------
        if (ctx.OverlayTestMode is not null)
        {
            fixes.Add(Make("mpo",
                "Reabilitar MPO (remover OverlayTestMode)",
                $"OverlayTestMode = {ctx.OverlayTestMode}",
                "Remover OverlayTestMode do DWM para reabilitar o MPO (multi-plane overlay)?",
                "O MPO é a sobreposição de planos usada pelo DWM. Com OverlayTestMode no registro, o Windows mantém o MPO desligado e o CS2 pode perder o DirectFlip, o que causa stutter/input lag em janela. " +
                "Remover o valor devolve o comportamento padrão. Não muda o FPS médio; o efeito é em fluidez e alt-tab. Como volta: o Desfazer regrava o valor anterior.",
                FixLevel.Safe, true, FixCategories.Video,
                (c, e) =>
                {
                    e.Registry.DeleteValue(c.DwmPath, "OverlayTestMode");
                    return Task.CompletedTask;
                }));
        }

        foreach (var (key, value) in ctx.BcdKeys.ToArray())
        {
            if (key.Equals("useplatformclock", StringComparison.OrdinalIgnoreCase))
            {
                if (!Regex.IsMatch(value, "^(no|false)$", RegexOptions.IgnoreCase))
                {
                    fixes.Add(Make("bcd-hpet",
                        "BCD: useplatformclock = no",
                        $"useplatformclock = {value}",
                        "Definir useplatformclock = no (usar TSC em vez de forçar o HPET)?",
                        "HPET forçado no BCD costuma aumentar a latência de timers. Definir useplatformclock = no devolve o uso do TSC, que é o padrão moderno do Windows. " +
                        "O ganho é pequeno e depende da placa; não é um tweak de FPS garantido. Exige reiniciar. Como volta: o Desfazer importa o bcd.bak.",
                        FixLevel.Safe, true, FixCategories.Cpu,
                        async (_, e) => await RunCheckedAsync(e, "bcdedit.exe",
                            new[] { "/set", "useplatformclock", "no" }, "bcdedit")));
                }
            }
            else
            {
                var keyCopy = key;
                fixes.Add(Make("bcd-" + keyCopy,
                    "BCD: remover " + keyCopy,
                    $"{keyCopy} = {value}",
                    $"Remover '{keyCopy}' do BCD (voltar ao padrão)?",
                    $"A chave '{keyCopy}' altera o comportamento de timer/tick do Windows e está fora do padrão. Removê-la devolve o comportamento automático do Windows. " +
                    "Não há ganho garantido de FPS; tweaks de timer costumam causar instabilidade. Exige reiniciar. Como volta: o Desfazer importa o bcd.bak.",
                    FixLevel.Safe, true, FixCategories.Cpu,
                    async (_, e) => await RunCheckedAsync(e, "bcdedit.exe",
                        new[] { "/deletevalue", keyCopy }, "bcdedit")));
            }
        }

        // ---------------- Energia ----------------
        if (!ctx.OnPerformancePowerPlan)
        {
            var level = ctx.HasBattery ? FixLevel.Optional : FixLevel.Safe;
            var state = "plano atual sem desempenho máximo" +
                        (ctx.HasBattery ? " (notebook: aquece mais e gasta bateria)" : "");
            fixes.Add(Make("power-plan",
                "Plano de energia: CS2 Desempenho Máximo",
                state,
                "Criar e ativar o plano 'CS2 Desempenho Maximo' (processador em 100%, PCIe sem economia de energia)?\r\n\r\nO plano antigo continua salvo e volta no desfazer.",
                "Duplica o plano Alto Desempenho (ou Ultimate) e cria/ativa o 'CS2 Desempenho Maximo', com estado mínimo do processador em 100% e ASPM desligado na tomada. " +
                "Evita downclock e economia de energia no PCIe durante o jogo. Custo: mais consumo e calor; em notebook aquece e gasta bateria (por isso é Opcional). " +
                "O plano antigo continua salvo e o Desfazer reativa o original. Se o plano já existir, ele é reaproveitado.",
                level, false, FixCategories.Energia,
                async (_, e) => await NewPerformancePlanAsync(e)));
        }

        if (ctx.FastBoot == 1)
        {
            fixes.Add(Make("fastboot",
                "Desativar Inicialização Rápida",
                "ativada",
                "Desativar a Inicialização Rápida (hibernação híbrida)? Ela deixa o PC dias sem reiniciar de verdade.",
                "A Inicialização Rápida usa uma hibernação híbrida e pode manter drivers/estado antigos entre desligamentos, causando comportamento estranho. " +
                "Desativá-la faz o desligamento ser completo. Custo: o boot fica alguns segundos mais lento. Como volta: o Desfazer regrava o valor anterior.",
                FixLevel.Safe, false, FixCategories.Energia,
                (c, e) =>
                {
                    e.Registry.SetValue(@"HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Power",
                        "HiberbootEnabled", 0, RegistryValueKind.DWord);
                    return Task.CompletedTask;
                }));
        }

        if (ctx.GameDvrAppCapture == 1 || ctx.GameDvrEnabled == 1)
        {
            fixes.Add(Make("gamedvr",
                "Desativar capturas do Game Bar/GameDVR",
                "capturas ativadas",
                "Desativar as capturas em segundo plano do Xbox Game Bar (o Game Bar em si continua)?",
                "Desliga a captura/gravação em segundo plano do Game Bar. Isso reduz overhead de CPU/GPU e evita gravações acidentais. " +
                "O Game Bar continua disponível. Como volta: o Desfazer regrava os valores anteriores.",
                FixLevel.Safe, false, FixCategories.Sistema,
                (c, e) =>
                {
                    e.Registry.SetValue(@"HKCU\Software\Microsoft\Windows\CurrentVersion\GameDVR",
                        "AppCaptureEnabled", 0, RegistryValueKind.DWord);
                    e.Registry.SetValue(@"HKCU\System\GameConfigStore",
                        "GameDVR_Enabled", 0, RegistryValueKind.DWord);
                    return Task.CompletedTask;
                }));
        }

        if (ctx.GameMode == 0)
        {
            fixes.Add(Make("gamemode",
                "Reativar Modo de Jogo",
                "desativado",
                "Reativar o Modo de Jogo do Windows?",
                "O Modo de Jogo dá prioridade de CPU/GPU ao jogo em foco e evita que o Windows faça manutenção pesada durante a partida. " +
                "Reativá-lo é seguro. Como volta: o Desfazer regrava os valores anteriores.",
                FixLevel.Safe, false, FixCategories.Sistema,
                (c, e) =>
                {
                    e.Registry.SetValue(@"HKCU\Software\Microsoft\GameBar",
                        "AutoGameModeEnabled", 1, RegistryValueKind.DWord);
                    e.Registry.SetValue(@"HKCU\Software\Microsoft\GameBar",
                        "AllowAutoGameMode", 1, RegistryValueKind.DWord);
                    return Task.CompletedTask;
                }));
        }

        if (ctx.IsWin11 && ctx.Hags != 2)
        {
            fixes.Add(Make("hags-on",
                "Ativar HAGS (Windows 11)",
                $"HwSchMode = {ctx.Hags}",
                "Ativar o agendamento de GPU acelerado por hardware (HAGS)? Requer GPU/driver compatíveis (NVIDIA série 1000+). O efeito varia por GPU/driver (em alguns PCs melhora, em outros piora o 1% low): meça antes de manter.",
                "Liga o HAGS no Windows 11 (HwSchMode=2), que deixa a GPU gerenciar a fila de trabalho. Pode melhorar o 1% low e a fluidez; em alguns drivers piora. " +
                "Não é garantido: meça antes e depois. Exige reiniciar. Como volta: o Desfazer regrava o valor anterior.",
                FixLevel.Optional, true, FixCategories.Video,
                (c, e) =>
                {
                    e.Registry.SetValue(@"HKLM\SYSTEM\CurrentControlSet\Control\GraphicsDrivers",
                        "HwSchMode", 2, RegistryValueKind.DWord);
                    return Task.CompletedTask;
                }));
        }
        else if (!ctx.IsWin11 && ctx.Hags == 2)
        {
            fixes.Add(Make("hags-off",
                "Desativar HAGS (Windows 10)",
                "HwSchMode = 2",
                "Desativar o agendamento de GPU acelerado por hardware (HAGS) no Windows 10?",
                "No Windows 10 o HAGS costuma piorar o 1% low em vários drivers, mas não é regra: se você ligou e notou stutter, desligar é o caminho. " +
                "Exige reiniciar. Como volta: o Desfazer regrava o valor anterior.",
                FixLevel.Optional, true, FixCategories.Video,
                (c, e) =>
                {
                    e.Registry.SetValue(@"HKLM\SYSTEM\CurrentControlSet\Control\GraphicsDrivers",
                        "HwSchMode", 1, RegistryValueKind.DWord);
                    return Task.CompletedTask;
                }));
        }

        // ---------------- Windows opcional ----------------
        if (!ctx.BackgroundAppsDisabled)
        {
            fixes.Add(Make("bgapps",
                "Desativar apps em segundo plano",
                "permitido",
                "Impedir que apps do Windows rodem em segundo plano? Pode atrasar notificações da Microsoft Store.",
                "Impede apps da Store/Windows de rodar em segundo plano, liberando RAM/CPU. Pode atrasar notificações e sincronização da Store. " +
                "Como volta: o Desfazer regrava o valor anterior.",
                FixLevel.Optional, false, FixCategories.Sistema,
                (c, e) =>
                {
                    if (c.IsWin11)
                    {
                        e.Registry.SetValue(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\AppPrivacy",
                            "LetAppsRunInBackground", 2, RegistryValueKind.DWord);
                    }
                    else
                    {
                        e.Registry.SetValue(
                            @"HKCU\Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications",
                            "GlobalUserDisabled", 1, RegistryValueKind.DWord);
                    }
                    return Task.CompletedTask;
                }));
        }

        if (!(ctx.SystemResponsiveness == 0 && ctx.NetworkThrottling == -1))
        {
            var nt = ctx.NetworkThrottling == -1
                ? "-1 (sem limite)"
                : ctx.NetworkThrottling?.ToString() ?? "padrão";
            fixes.Add(Make("mmcss",
                "Priorizar jogos no MMCSS (mídia/rede)",
                $"SystemResponsiveness={ctx.SystemResponsiveness?.ToString() ?? "padrão"}, NetworkThrottlingIndex={nt}",
                "Ajustar o agendador multimídia do Windows para jogos (SystemResponsiveness = 0 e sem limite de rede)?",
                "Ajusta o MMCSS para dar prioridade a jogos: SystemResponsiveness=0 e NetworkThrottlingIndex=0xFFFFFFFF (sem limite). " +
                "O efeito é pequeno e não é garantido; não substitui driver/rede bons. Como volta: o Desfazer regrava os valores anteriores.",
                FixLevel.Optional, false, FixCategories.Rede,
                (c, e) =>
                {
                    e.Registry.SetValue(
                        @"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile",
                        "SystemResponsiveness", 0, RegistryValueKind.DWord);
                    e.Registry.SetValue(
                        @"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile",
                        "NetworkThrottlingIndex", -1, RegistryValueKind.DWord);
                    return Task.CompletedTask;
                }));
        }

        // ---------------- cs2.exe ----------------
        if (!string.IsNullOrWhiteSpace(ctx.Cs2Exe))
        {
            if (ctx.FsoDisabled)
            {
                fixes.Add(Make("cs2-fso",
                    "Reativar otimizações de tela inteira do cs2.exe",
                    "flag marcada",
                    "Remover a flag \"Desativar otimizações de tela inteira\" do cs2.exe? (com ela o CS2 perde o DirectFlip)",
                    "Remove a flag DISABLEDXMAXIMIZEDWINDOWEDMODE das camadas de compatibilidade do cs2.exe. Com a flag, o CS2 perde o DirectFlip e pode ficar mais sujeito a stutter/input lag em janela. " +
                    "Se o jogo apresentar problema de tela, desfaça. Como volta: o Desfazer regrava o valor anterior.",
                    FixLevel.Safe, false, FixCategories.Video,
                    (c, e) =>
                    {
                        foreach (var layer in c.LayerPaths)
                        {
                            var value = e.Registry.GetString(layer, c.Cs2Exe!);
                            if (value is null ||
                                !value.Contains("DISABLEDXMAXIMIZEDWINDOWEDMODE", StringComparison.OrdinalIgnoreCase))
                                continue;

                            var tokens = value.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                                .Where(t => !t.Equals("DISABLEDXMAXIMIZEDWINDOWEDMODE", StringComparison.OrdinalIgnoreCase))
                                .ToArray();
                            if (tokens.Length == 0 || (tokens.Length == 1 && tokens[0] == "~"))
                                e.Registry.DeleteValue(layer, c.Cs2Exe!);
                            else
                                e.Registry.SetValue(layer, c.Cs2Exe!, string.Join(' ', tokens), RegistryValueKind.String);
                        }
                        return Task.CompletedTask;
                    }));
            }

            if (ctx.GpuPreference?.Contains("GpuPreference=2", StringComparison.OrdinalIgnoreCase) != true)
            {
                fixes.Add(Make("cs2-gpu",
                    "GPU Alto Desempenho para cs2.exe",
                    "sem preferência",
                    "Definir cs2.exe como GPU \"Alto desempenho\" (otimização de modo janela ON, VRR do Windows OFF)?",
                    "Grava a preferência de GPU do cs2.exe como Alto desempenho (GpuPreference=2), com SwapEffectUpgradeEnable=1 e VRROptimizeEnable=0. " +
                    "Em notebook garante que o jogo use a GPU dedicada; em desktop evita a otimização de swap do Windows. Como volta: o Desfazer regrava o valor anterior.",
                    FixLevel.Safe, false, FixCategories.Video,
                    (c, e) =>
                    {
                        e.Registry.SetValue(@"HKCU\Software\Microsoft\DirectX\UserGpuPreferences",
                            c.Cs2Exe!, "GpuPreference=2;SwapEffectUpgradeEnable=1;VRROptimizeEnable=0;",
                            RegistryValueKind.String);
                        return Task.CompletedTask;
                    }));
            }

            const string ifeo = @"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\cs2.exe\PerfOptions";
            if (ctx.Cs2PriorityClass != 3)
            {
                fixes.Add(Make("cs2-prio",
                "Prioridade Alta permanente para o cs2.exe",
                "prioridade normal",
                "Definir CpuPriorityClass = 3 (Alta) para cs2.exe?\r\nO Windows passa a abrir o CS2 com prioridade Alta (chave Image File Execution Options\\PerfOptions, só isso - não injeta nada e não usa debugger).\r\nGanho pequeno; se Discord/OBS engasgarem com o jogo aberto, desfaça.",
                "Usa a chave Image File Execution Options\\PerfOptions para o Windows abrir o cs2.exe com prioridade Alta (CpuPriorityClass=3). " +
                "Não injeta DLL, não usa debugger e não deixa processo residente. O ganho é pequeno e pode fazer Discord/OBS perderem CPU quando o jogo está aberto. " +
                "Como volta: o Desfazer regrava/remove o valor.",
                FixLevel.Optional, false, FixCategories.Cs2,
                (c, e) =>
                {
                    e.Registry.SetValue(ifeo, "CpuPriorityClass", 3, RegistryValueKind.DWord);
                    return Task.CompletedTask;
                }));
            }
        }

        // ---------------- Limpeza / manutenção ----------------
        if (ctx.MouseSpeed != "0")
        {
            fixes.Add(Make("mouse",
                "Desativar aceleração do mouse",
                $"MouseSpeed = {ctx.MouseSpeed ?? "padrão"}",
                "Desativar \"Aprimorar precisão do ponteiro\" (aceleração do mouse)?",
                "Zera MouseSpeed/MouseThreshold1/MouseThreshold2, desligando a aceleração do ponteiro. O movimento fica 1:1 com o mouse, o que é o esperado por jogadores. " +
                "Precisa sair e entrar na conta (ou reiniciar) para valer em todos os apps. Como volta: o Desfazer regrava os valores anteriores.",
                FixLevel.Optional, true, FixCategories.Sistema,
                (c, e) =>
                {
                    e.Registry.SetValue(@"HKCU\Control Panel\Mouse", "MouseSpeed", "0", RegistryValueKind.String);
                    e.Registry.SetValue(@"HKCU\Control Panel\Mouse", "MouseThreshold1", "0", RegistryValueKind.String);
                    e.Registry.SetValue(@"HKCU\Control Panel\Mouse", "MouseThreshold2", "0", RegistryValueKind.String);
                    return Task.CompletedTask;
                }));
        }

        if (ctx.SsdLetters.Count > 0)
        {
            fixes.Add(Make("trim",
                $"ReTrim nos SSDs ({string.Join(", ", ctx.SsdLetters)})",
                "manutenção",
                "Rodar TRIM/otimização nos SSDs? É rápido e seguro.",
                "Executa o ReTrim (defrag /L) nos SSDs. Mantém o desempenho de escrita e a saúde do SSD; é rápido e não apaga dados. " +
                "Não precisa desfazer (é manutenção, não configuração).",
                FixLevel.Optional, false, FixCategories.Limpeza,
                async (c, e) =>
                {
                    foreach (var letter in c.SsdLetters)
                    {
                        await RunCheckedAsync(e, "defrag.exe",
                            new[] { $"{letter}:", "/L" }, $"ReTrim {letter}:");
                    }
                }));
        }

        if (ctx.ShaderMB > 0)
        {
            fixes.Add(Make("shaders",
                $"Limpar cache de shaders (~{ctx.ShaderMB:0.#} MB)",
                $"~{ctx.ShaderMB:0.#} MB",
                "Limpar caches de shader (DirectX, driver, Steam 730 -> 730.old)?\r\nNÃO ajuda FPS: o cache pronto é o que evita travadas. Apagar faz o CS2 recompilar tudo e a(s) primeira(s) partida(s) travam. Só vale depois de trocar o driver de vídeo ou se houver falha gráfica/crash.",
                "Apaga os caches de shader do DirectX, NVIDIA/AMD e o shadercache\\730 da Steam (renomeado para 730.old). " +
                "NÃO melhora FPS: o cache pronto é justamente o que evita travadas; apagar faz o CS2 recompilar e as primeiras partidas travarem. " +
                "Só faz sentido depois de trocar o driver de vídeo ou se houver falha gráfica/crash. Como volta: o cache é recriado; o 730.old pode ser renomeado de volta.",
                FixLevel.Optional, false, FixCategories.Limpeza,
                async (c, e) =>
                {
                    await StopSteamCs2Async(e);
                    foreach (var path in c.ShaderPaths)
                    {
                        try { DeleteDirectoryContents(path, e); }
                        catch (Exception ex) { e.Log.Warn($"  não consegui limpar {path}: {ex.Message}"); }
                    }

                    foreach (var shader in c.Steam.SteamShaderPaths)
                    {
                        try
                        {
                            var old = shader + ".old";
                            if (Directory.Exists(old)) Directory.Delete(old, true);
                            if (Directory.Exists(shader))
                                Directory.Move(shader, old);
                        }
                        catch (Exception ex)
                        {
                            e.Log.Warn($"  não consegui renomear {shader}: {ex.Message}");
                        }
                    }
                }));
        }

        if (ctx.TempMB > 200)
        {
            fixes.Add(Make("temp",
                $"Limpar %TEMP% (~{ctx.TempMB:0.#} MB)",
                $"~{ctx.TempMB:0.#} MB",
                $"Limpar a pasta TEMP do usuário (~{ctx.TempMB:0.#} MB)?",
                "Apaga o conteúdo de %TEMP% do usuário. Libera espaço e não afeta o jogo em execução (arquivos em uso são ignorados). " +
                "Não há o que restaurar: é lixo temporário.",
                FixLevel.Optional, false, FixCategories.Limpeza,
                (c, e) =>
                {
                    var temp = Environment.GetEnvironmentVariable("TEMP") ?? Path.GetTempPath();
                    DeleteDirectoryContents(temp, e);
                    return Task.CompletedTask;
                }));
        }

        fixes.Add(Make("syscache",
            "Limpar cache do sistema (DNS/Temp do Windows/Steam)",
            "manutenção",
            "Limpar DNS, Temp do Windows e cache de download da Steam? Prefetch fica de fora de propósito (apagar só deixa a próxima abertura de programas mais lenta). Quase não muda FPS.",
            "Limpa o cache DNS, a pasta C:\\Windows\\Temp e o cache de download da Steam. O Prefetch fica de fora de propósito. " +
            "Quase não muda FPS; serve para liberar espaço e resolver DNS estranho. Como volta: os caches são recriados automaticamente.",
            FixLevel.Optional, false, FixCategories.Limpeza,
            async (c, e) =>
            {
                await StopSteamCs2Async(e);
                await RunIgnoredAsync(e, "ipconfig.exe", new[] { "/flushdns" }, "Flush DNS");

                var windowsTemp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp");
                if (Directory.Exists(windowsTemp)) DeleteDirectoryContents(windowsTemp, e);
                foreach (var path in c.Steam.SteamCachePaths)
                {
                    if (Directory.Exists(path)) DeleteDirectoryContents(path, e);
                }
            }));

        // ---------------- Telemetria (tudo reversível pelo backup) ----------------
        if (ctx.TelemetryServiceStart != 4 || ctx.TelemetryWapStart != 4)
        {
            var state = new List<string>();
            if (ctx.TelemetryServiceStart != 4) state.Add("DiagTrack ligado");
            if (ctx.TelemetryWapStart != 4) state.Add("WAP Push ligado");

            fixes.Add(Make("telemetry-servico",
                "Desativar serviço de telemetria (DiagTrack)",
                string.Join(", ", state),
                "Parar e desativar o DiagTrack (Experiências do Usuário Conectado) e o WAP Push?\r\n" +
                "É daqui que sai a maior parte dos dados de uso enviados para a Microsoft.\r\n" +
                "Não mexe em Windows Update, Defender, Store, Xbox nem em login.",
                "Para e desativa os serviços DiagTrack e dmwappushservice. É daqui que sai a maior parte dos dados de uso enviados à Microsoft. " +
                "Não afeta Windows Update, Defender, Store, Xbox ou login. Como volta: o Desfazer restaura o Start e o backup .reg dos serviços.",
                FixLevel.Safe, false, FixCategories.Privacidade,
                async (c, e) =>
                {
                    foreach (var service in new[] { "DiagTrack", "dmwappushservice" })
                    {
                        try
                        {
                            e.Registry.SetValue(
                                $@"HKLM\SYSTEM\CurrentControlSet\Services\{service}",
                                "Start", 4, RegistryValueKind.DWord);
                        }
                        catch (Exception ex)
                        {
                            e.Log.Warn($"  não consegui desativar {service}: {ex.Message}");
                        }
                        await RunIgnoredAsync(e, "sc.exe", new[] { "stop", service }, "Parando " + service);
                    }
                }));
        }

        if (ctx.TelemetryLevel != 0 || ctx.TelemetryCeip != 0)
        {
            var state = ctx.TelemetryLevel != 0
                ? (ctx.TelemetryLevel is null ? "padrão do Windows" : $"nível {ctx.TelemetryLevel}")
                : "CEIP (experiência do usuário) ligado";

            fixes.Add(Make("telemetry-dados",
                "Dados de diagnóstico no mínimo",
                state,
                "Deixar o envio de diagnóstico no mínimo (AllowTelemetry = 0) e desligar o CEIP?\r\n" +
                "No Windows Home/Pro o sistema ainda manda o pacote 'obrigatório', mas para de mandar o opcional " +
                "(apps abertos, uso do navegador, tempo de tela). Windows Update continua normal.",
                "Coloca AllowTelemetry=0 e desliga CEIP/limites de coleta. No Windows Home/Pro ainda existe o pacote obrigatório, mas o opcional deixa de ser enviado. " +
                "Windows Update continua normal. Não afeta FPS. Como volta: o Desfazer regrava os valores anteriores.",
                FixLevel.Safe, false, FixCategories.Privacidade,
                (c, e) =>
                {
                    e.Registry.SetValue(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\DataCollection",
                        "AllowTelemetry", 0, RegistryValueKind.DWord);
                    e.Registry.SetValue(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\DataCollection",
                        "LimitDiagnosticLogCollection", 1, RegistryValueKind.DWord);
                    e.Registry.SetValue(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\DataCollection",
                        "LimitDumpCollection", 1, RegistryValueKind.DWord);
                    e.Registry.SetValue(
                        @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection",
                        "AllowTelemetry", 0, RegistryValueKind.DWord);
                    e.Registry.SetValue(@"HKLM\SOFTWARE\Policies\Microsoft\SQMClient\Windows",
                        "CEIPEnable", 0, RegistryValueKind.DWord);
                    return Task.CompletedTask;
                }));
        }

        if (ctx.TelemetryAdId != 0 || ctx.TelemetryFeed != 0)
        {
            fixes.Add(Make("telemetry-rastreio",
                "Desativar rastreio de uso (ID de publicidade)",
                "rastreio ligado",
                "Desligar ID de publicidade, experiências personalizadas, histórico de atividades e a coleta de escrita?\r\n" +
                "São os rastreadores do dia a dia (não o diagnóstico do sistema). Não afeta login, Store nem OneDrive.",
                "Desliga ID de publicidade, experiências personalizadas, histórico de atividades e coleta de escrita. São rastreadores do dia a dia, não o diagnóstico do sistema. " +
                "Não afeta login, Store ou OneDrive e não muda FPS. Como volta: o Desfazer regrava os valores anteriores.",
                FixLevel.Safe, false, FixCategories.Privacidade,
                (c, e) =>
                {
                    e.Registry.SetValue(@"HKCU\Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo",
                        "Enabled", 0, RegistryValueKind.DWord);
                    e.Registry.SetValue(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Privacy",
                        "TailoredExperiencesWithDiagnosticDataEnabled", 0, RegistryValueKind.DWord);
                    e.Registry.SetValue(@"HKCU\Software\Microsoft\Siuf\Rules",
                        "NumberOfSIUFInPeriod", 0, RegistryValueKind.DWord);
                    e.Registry.SetValue(@"HKCU\Software\Microsoft\Siuf\Rules",
                        "PeriodInNanoSeconds", 0, RegistryValueKind.DWord);
                    e.Registry.SetValue(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\System",
                        "EnableActivityFeed", 0, RegistryValueKind.DWord);
                    e.Registry.SetValue(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\System",
                        "PublishUserActivities", 0, RegistryValueKind.DWord);
                    e.Registry.SetValue(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\System",
                        "UploadUserActivities", 0, RegistryValueKind.DWord);
                    e.Registry.SetValue(@"HKCU\Software\Microsoft\Input\TIPC",
                        "Enabled", 0, RegistryValueKind.DWord);
                    e.Registry.SetValue(@"HKCU\Software\Microsoft\InputPersonalization",
                        "RestrictImplicitTextCollection", 1, RegistryValueKind.DWord);
                    e.Registry.SetValue(@"HKCU\Software\Microsoft\InputPersonalization",
                        "RestrictImplicitInkCollection", 1, RegistryValueKind.DWord);
                    return Task.CompletedTask;
                }));
        }

        if (ctx.TelemetryVoice != 0)
        {
            fixes.Add(Make("telemetry-voz",
                "Desligar envio de voz (fala online)",
                "reconhecimento online ligado",
                "Desligar o envio de áudio para reconhecimento de fala na nuvem e a personalização de entrada?\r\n" +
                "O microfone do jogo (Steam/Discord/CS2) não é afetado - isso é só o 'ditado'/'falar para o Windows'.",
                "Desliga o envio de áudio para reconhecimento de fala na nuvem e a personalização de entrada. O microfone do jogo não é afetado. " +
                "Não muda FPS. Como volta: o Desfazer regrava os valores anteriores.",
                FixLevel.Safe, false, FixCategories.Privacidade,
                (c, e) =>
                {
                    e.Registry.SetValue(
                        @"HKCU\Software\Microsoft\Speech_OneCore\Settings\OnlineSpeechPrivacy",
                        "HasAccepted", 0, RegistryValueKind.DWord);
                    e.Registry.SetValue(@"HKLM\SOFTWARE\Policies\Microsoft\InputPersonalization",
                        "AllowInputPersonalization", 0, RegistryValueKind.DWord);
                    return Task.CompletedTask;
                }));
        }

        if (ctx.TelemetryWer != 1 ||
            (ctx.TelemetryCompatTask is not ("Disabled" or "Ausente")))
        {
            var state = ctx.TelemetryWer != 1 ? "envio de relatórios ligado" : "tarefa de compatibilidade ativa";
            fixes.Add(Make("telemetry-erros",
                "Parar envio de relatórios de erro (WER)",
                state,
                "Desligar o envio de relatórios de erro (WER) para a Microsoft e as tarefas 'Experiência de Aplicativos'?\r\n" +
                "O Windows continua mostrando o erro na tela; só para de empacotar e enviar. Jogos e programas não são afetados.",
                "Desliga o envio de relatórios de erro (WER) e as tarefas de compatibilidade. O Windows continua mostrando o erro; só para de empacotar/enviar. " +
                "Jogos e programas não são afetados. Como volta: o Desfazer regrava os valores e reative as tarefas manualmente se quiser.",
                FixLevel.Safe, false, FixCategories.Privacidade,
                async (c, e) =>
                {
                    e.Registry.SetValue(@"HKLM\SOFTWARE\Microsoft\Windows\Windows Error Reporting",
                        "Disabled", 1, RegistryValueKind.DWord);
                    e.Registry.SetValue(@"HKLM\SOFTWARE\Microsoft\Windows\Windows Error Reporting",
                        "DontSendAdditionalData", 1, RegistryValueKind.DWord);

                    foreach (var task in new[] { "Microsoft Compatibility Appraiser", "ProgramDataUpdater" })
                    {
                        await RunIgnoredAsync(e, "schtasks.exe",
                            new[] { "/change", "/tn", @"\Microsoft\Windows\Application Experience\" + task, "/disable" },
                            "Desativando tarefa");
                    }
                }));
        }

        // ---------------- Mitigações de CPU ----------------
        if (!ctx.Mit.IsDefault)
        {
            var resolution = MitigationService.Resolve(new[] { "padrao" });
            var state = "sobrescrita presente" + (ctx.Mit.IsReduced ? " (proteção REDUZIDA)" : "");
            fixes.Add(Make("mit-padrao",
                "Mitigações de CPU: voltar ao padrão do Windows",
                state,
                "Apagar FeatureSettingsOverride, FeatureSettingsOverrideMask e MinVmVersionForCpuBasedMitigations?",
                resolution.Explanation,
                FixLevel.Optional, true, FixCategories.Cpu,
                (c, e) =>
                {
                    e.Mitigation.Apply(new[] { "padrao" });
                    return Task.CompletedTask;
                }));
        }

        if (ctx.Mit.IsDefault && ctx.Mit.Vendor is "Intel" or "AMD")
        {
            var resolution = MitigationService.Resolve(new[] { "desligar-tudo" });
            fixes.Add(Make("mit-desligar-tudo",
                "Desativar mitigações de CPU (Spectre v2 + Meltdown)",
                "ativas (padrão do Windows)",
                "Gravar FeatureSettingsOverride = 3 e Mask = 3 para desligar Spectre v2 e Meltdown?",
                resolution.Explanation + "\r\n\r\n" +
                "Enquanto estiver aplicado, o PC fica menos protegido. O backup permite voltar atrás. Só vale depois de reiniciar.",
                FixLevel.Risky, true, FixCategories.Cpu,
                (c, e) =>
                {
                    e.Mitigation.Apply(new[] { "desligar-tudo" });
                    return Task.CompletedTask;
                }));
        }

        // ---------------- Reparo ----------------
        fixes.Add(Make("dism-restore",
            "DISM RestoreHealth (reparar imagem)",
            "demorado",
            "Reparar a imagem do Windows (DISM /RestoreHealth)? Leva de 5 a 20 min e usa o Windows Update.",
            "Executa DISM /Online /Cleanup-Image /RestoreHealth para reparar a imagem do Windows. Leva de 5 a 20 minutos e usa o Windows Update. " +
            "Não altera configurações nem FPS diretamente; resolve corrupção que causa crashes. Não precisa desfazer.",
            FixLevel.Optional, false, FixCategories.Reparo,
            async (_, e) => await RunCheckedAsync(e, "DISM.exe",
                new[] { "/Online", "/Cleanup-Image", "/RestoreHealth" }, "DISM RestoreHealth")));

        fixes.Add(Make("sfc",
            "SFC /scannow (arquivos do sistema)",
            "demorado",
            "Verificar e reparar arquivos do sistema (sfc /scannow)? Leva de 5 a 15 min.",
            "Executa sfc /scannow para verificar e reparar arquivos protegidos do Windows. Leva de 5 a 15 minutos. " +
            "Não altera configurações nem FPS diretamente; resolve corrupção de sistema. Não precisa desfazer.",
            FixLevel.Optional, false, FixCategories.Reparo,
            async (_, e) => await RunCheckedAsync(e, "sfc.exe",
                new[] { "/scannow" }, "SFC /scannow")));

        fixes.Add(Make("dism-cleanup",
            "DISM StartComponentCleanup",
            "demorado",
            "Limpar componentes antigos do Windows (libera espaço em WinSxS)?",
            "Executa DISM /Online /Cleanup-Image /StartComponentCleanup para remover componentes antigos do WinSxS e liberar espaço. " +
            "Não muda FPS; depois de limpar, atualizações podem demorar mais para instalar. Não precisa desfazer.",
            FixLevel.Optional, false, FixCategories.Reparo,
            async (_, e) => await RunCheckedAsync(e, "DISM.exe",
                new[] { "/Online", "/Cleanup-Image", "/StartComponentCleanup" }, "DISM Cleanup")));

        fixes.Add(Make("chkdsk",
            "CHKDSK /scan (erros no disco)",
            "demorado",
            $"Verificar erros no disco do Windows (chkdsk {Environment.GetEnvironmentVariable("SystemDrive") ?? "C:"} /scan, só leitura, sem reiniciar)?",
            "Executa chkdsk /scan (somente leitura) no disco do Windows para encontrar erros de sistema de arquivos. Não reinicia e não corrige automaticamente; " +
            "se encontrar problema, o próprio Windows agenda a correção. Não muda FPS. Não precisa desfazer.",
            FixLevel.Optional, false, FixCategories.Reparo,
            async (_, e) =>
            {
                var drive = Environment.GetEnvironmentVariable("SystemDrive") ?? "C:";
                var result = await e.Runner.RunAsync("chkdsk.exe", new[] { drive, "/scan" },
                    "CHKDSK /scan", line => e.Log.Log("    " + line), e.SetStatus, e.CancellationToken);
                if (!result.Started)
                    throw new InvalidOperationException("Não consegui iniciar chkdsk: " + result.StandardError);
                if (result.ExitCode >= 2)
                    throw new InvalidOperationException(
                        $"CHKDSK retornou código {result.ExitCode} (2/3 = problemas no disco ou acesso negado). Se houver erro, rode 'chkdsk /f /r' e reinicie.");
            }));

        fixes.Add(Make("net-reset",
            "Reset de rede (Winsock/TCP-IP)",
            "só se tiver problema",
            "Resetar Winsock e pilha TCP/IP? Só se tiver problema de rede; pode apagar IP fixo/DNS manual e exige reiniciar.",
            "Reseta Winsock e a pilha TCP/IP. Só use se tiver problema de rede. Pode apagar IP fixo/DNS manual e exige reiniciar. " +
            "O backup do registro/BCD/plano de energia NÃO desfaz a configuração de rede; você terá de reconfigurar manualmente.",
            FixLevel.Destructive, true, FixCategories.Rede,
            async (_, e) =>
            {
                await RunIgnoredAsync(e, "netsh.exe", new[] { "winsock", "reset" }, "Reset Winsock");
                await RunIgnoredAsync(e, "netsh.exe", new[] { "int", "ip", "reset" }, "Reset TCP/IP");
                await RunIgnoredAsync(e, "ipconfig.exe", new[] { "/flushdns" }, "Flush DNS");
            }));

        // ---------------- Destrutivo ----------------
        if (ctx.Steam.Userdata730.Count > 0)
        {
            fixes.Add(Make("cs2-reset",
                "Resetar configs do CS2 (userdata 730)",
                $"{ctx.Steam.Userdata730.Count} pasta(s)",
                "Renomear userdata\\...\\730 para 730.old?\r\nAPAGA binds, crosshair e configs de vídeo. Desative a Steam Cloud do CS2 antes de abrir o jogo.",
                "Renomeia cada pasta userdata\\...\\730 para 730.old, apagando binds, crosshair e configurações de vídeo do CS2. " +
                "Desative a Steam Cloud do CS2 antes de abrir o jogo, senão ela devolve as configs antigas. O backup NÃO desfaz isso; o 730.old fica na pasta e pode ser renomeado de volta manualmente.",
                FixLevel.Destructive, false, FixCategories.Cs2,
                async (c, e) =>
                {
                    foreach (var name in new[] { "steam", "cs2" })
                    {
                        foreach (var process in Process.GetProcessesByName(name))
                        {
                            using (process) { }
                            throw new InvalidOperationException("Feche a Steam e o CS2 antes.");
                        }
                    }

                    foreach (var path in c.Steam.Userdata730)
                    {
                        var old = path + ".old";
                        if (Directory.Exists(old)) Directory.Delete(old, true);
                        Directory.Move(path, old);
                    }
                    await Task.CompletedTask;
                }));
        }

        return fixes;
    }

    private static FixDefinition Make(
        string id, string title, string currentState, string detail, string explanation,
        FixLevel level, bool requiresReboot, string category,
        Func<FixContext, FixExecutionContext, Task> apply)
    {
        return new FixDefinition
        {
            Id = id,
            Title = title,
            CurrentState = currentState,
            Detail = detail,
            Explanation = explanation,
            Level = level,
            RequiresReboot = requiresReboot,
            Category = category,
            Recommended = level == FixLevel.Safe ||
                          RecommendedOptionalIds.Contains(id, StringComparer.OrdinalIgnoreCase),
            ApplyAsync = apply
        };
    }

    private static async Task NewPerformancePlanAsync(FixExecutionContext e)
    {
        var list = await e.Runner.RunAsync("powercfg.exe", new[] { "/list" },
            "Listando planos", onStatus: e.SetStatus, cancellationToken: e.CancellationToken);
        var match = Regex.Match(list.StandardOutput ?? "",
            @"(?im)^[^\r\n]*?([0-9a-fA-F]{8}-(?:[0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12})[^\r\n]*CS2 Desempenho [Mm].ximo");
        string? guid = match.Success ? match.Groups[1].Value : null;

        if (guid is null)
        {
            foreach (var template in new[]
                     {
                         "e9a42b02-d5df-448d-aa00-03f14749eb61",
                         "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c"
                     })
            {
                var duplicate = await e.Runner.RunAsync("powercfg.exe",
                    new[] { "/duplicatescheme", template }, "Criando plano",
                    onStatus: e.SetStatus, cancellationToken: e.CancellationToken);
                var found = Regex.Match((duplicate.StandardOutput ?? "") + "\n" + (duplicate.StandardError ?? ""),
                    "[0-9a-fA-F]{8}-([0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}");
                if (found.Success)
                {
                    guid = found.Value;
                    break;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(guid))
            throw new InvalidOperationException(
                "Não foi possível criar/achar um plano de energia de desempenho neste sistema.");

        await RunIgnoredAsync(e, "powercfg.exe",
            new[] { "/changename", guid, "CS2 Desempenho Maximo" }, "Nomeando plano");
        await RunCheckedAsync(e, "powercfg.exe",
            new[] { "/setacvalueindex", guid, "SUB_PCIEXPRESS", "ASPM", "0" }, "ASPM");
        await RunCheckedAsync(e, "powercfg.exe",
            new[] { "/setacvalueindex", guid, "SUB_PROCESSOR", "PROCTHROTTLEMIN", "100" }, "CPU 100%");
        await RunCheckedAsync(e, "powercfg.exe",
            new[] { "/setactive", guid }, "Ativando plano");
    }

    private static async Task StopSteamCs2Async(FixExecutionContext e)
    {
        var processes = Process.GetProcessesByName("steam")
            .Concat(Process.GetProcessesByName("steamwebhelper"))
            .Concat(Process.GetProcessesByName("cs2"))
            .ToArray();

        if (processes.Length == 0) return;

        var close = e.Auto ||
                    (e.Ask?.Invoke("Steam/CS2 estão abertos. Fechar agora?", FixLevel.Safe) ?? false);
        if (!close)
        {
            foreach (var process in processes) process.Dispose();
            throw new InvalidOperationException("Feche a Steam/CS2 e tente de novo.");
        }

        e.Log.Warn("  fechando Steam/CS2...");
        foreach (var process in processes)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            finally { process.Dispose(); }
        }

        await Task.Delay(3000, e.CancellationToken);
    }

    private static void DeleteDirectoryContents(string path, FixExecutionContext e)
    {
        if (!Directory.Exists(path)) return;
        foreach (var entry in Directory.EnumerateFileSystemEntries(path))
        {
            try
            {
                if (Directory.Exists(entry)) Directory.Delete(entry, true);
                else File.Delete(entry);
            }
            catch (Exception ex)
            {
                e.Log.Warn($"  não consegui apagar {entry}: {ex.Message}");
            }
        }
    }

    private static async Task RunCheckedAsync(FixExecutionContext e, string exe,
        string[] args, string label)
    {
        e.SetStatus?.Invoke(label + "...");
        var result = await e.Runner.RunAsync(exe, args, label,
            line => e.Log.Log("    " + line), e.SetStatus, e.CancellationToken);

        if (!result.Started)
            throw new InvalidOperationException($"Não consegui iniciar {exe}: {result.StandardError}");
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"{label} retornou código {result.ExitCode}");
    }

    private static async Task RunIgnoredAsync(FixExecutionContext e, string exe,
        string[] args, string label)
    {
        e.SetStatus?.Invoke(label + "...");
        var result = await e.Runner.RunAsync(exe, args, label,
            line => e.Log.Log("    " + line), e.SetStatus, e.CancellationToken);
        if (!result.Started)
            e.Log.Warn($"  não consegui iniciar {exe}: {result.StandardError}");
    }
}
