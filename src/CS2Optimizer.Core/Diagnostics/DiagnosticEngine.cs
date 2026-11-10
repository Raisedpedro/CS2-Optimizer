using System.Text.RegularExpressions;
using CS2Optimizer.Core.Models;
using CS2Optimizer.Core.Services;

namespace CS2Optimizer.Core.Diagnostics;

public sealed record DiagnosticResult(
    IReadOnlyList<DiagnosticEntry> Entries,
    IReadOnlyList<string> Manual,
    IReadOnlyList<string> Problems);

/// <summary>Diagnóstico somente leitura (fase 1 do protótipo).</summary>
public sealed class DiagnosticEngine
{
    private readonly AppLogger _log;
    private readonly List<DiagnosticEntry> _entries = new();
    private readonly List<string> _manual = new();
    private readonly List<string> _problems = new();

    public DiagnosticEngine(AppLogger log)
    {
        _log = log;
    }

    public event Action<DiagnosticEntry>? EntryAdded;

    public DiagnosticResult Run(FixContext ctx)
    {
        _entries.Clear();
        _manual.Clear();
        _problems.Clear();

        RunSystem(ctx);
        RunCpu(ctx);
        RunRam(ctx);
        RunGpu(ctx);
        RunStorage(ctx);
        RunSteam(ctx);
        RunMpo(ctx);
        RunBcd(ctx);
        RunPowerAndShell(ctx);
        RunTelemetry(ctx);
        RunMitigations(ctx);
        RunSecurity(ctx);
        RunProcesses(ctx);

        return new DiagnosticResult(_entries.ToArray(), _manual.ToArray(), _problems.ToArray());
    }

    private void Emit(DiagnosticKind kind, string text)
    {
        var entry = new DiagnosticEntry(kind, text);
        _entries.Add(entry);
        _log.Log($"[{kind.ToString().ToLowerInvariant()}] {text}");
        if (kind is DiagnosticKind.Warn or DiagnosticKind.Bad) _problems.Add(text);
        EntryAdded?.Invoke(entry);
    }

    private void Section(string text) => Emit(DiagnosticKind.Section, text);
    private void Info(string text) => Emit(DiagnosticKind.Info, text);
    private void Ok(string text) => Emit(DiagnosticKind.Ok, text);
    private void Warn(string text) => Emit(DiagnosticKind.Warn, text);
    private void Bad(string text) => Emit(DiagnosticKind.Bad, text);

    private void Todo(string text)
    {
        if (!_manual.Contains(text)) _manual.Add(text);
        Emit(DiagnosticKind.Todo, text);
    }

    private void RunSystem(FixContext ctx)
    {
        Section("Sistema");
        Info($"{ctx.OsCaption} (build {ctx.BuildNumber})");
        Info($"Placa-mãe: {ctx.Board}");
        Info($"Máquina: {ctx.Machine}".Trim());

        var biosOld = ctx.BiosDate is { } d && d < DateTime.Now.AddMonths(-12);

        if (ctx.BiosDate is { } date)
        {
            Info($"BIOS: {ctx.BiosVersion} | {ctx.BiosManufacturer} | data: {date:dd/MM/yyyy}");
            if (biosOld)
                Warn("BIOS com mais de 12 meses. Veja se há atualização no site do fabricante da placa-mãe.");
        }
        else
        {
            Info($"BIOS: {ctx.BiosVersion} | {ctx.BiosManufacturer} | data: não informada (confira em msinfo32 > Versão/data do BIOS)");
        }

        // BIOS não é item de rotina: só entra no checklist quando está velho de verdade.
        if (biosOld)
        {
            Todo("Atualizar BIOS (site do fabricante da placa; energia estável durante o processo) e, junto, o driver de CHIPSET (vale pelo chipset, ex.: B550).");
        }
        else if (ctx.BiosDate is not null)
        {
            Ok("BIOS com menos de 12 meses: só atualize se houver um problema específico. Driver de CHIPSET: confira no site da placa-mãe.");
        }

        Todo("Opcional: se o Windows tem anos de instalação, erros estranhos ou restos de vários drivers, considere instalar do zero com pendrive (mídia oficial + Rufus). Não muda FPS por si só.");
        if (!ctx.IsWin11)
            Info("Windows 10 detectado: se possível migre para o Windows 11 (drivers e agendamento de GPU mais atuais).");

        if (ctx.IsLaptop)
            Warn("Notebook detectado: limite de energia/temperatura pesa muito. Jogue na tomada, plano máximo e base/cooler.");
    }

    private void RunCpu(FixContext ctx)
    {
        Section("CPU");
        Info(ctx.Cpu.Name);
        Info($"{ctx.Cpu.Cores} núcleos / {ctx.Cpu.LogicalProcessors} threads - clock base {ctx.Cpu.MaxClockMHz} MHz (o boost real é maior)");
        Todo("Ganho grande de FPS: overclock de CPU (PBO / Curve Optimizer em Ryzen) e de RAM (frequência/timings). Tweaks de registro \"milagrosos\" só causam instabilidade.");
        Todo("Com HWiNFO + Cinebench, confira se o clock efetivo chega perto do esperado sob carga. Bem abaixo = limite de energia/temperatura/PBO mal ajustado.");
    }

    private void RunRam(FixContext ctx)
    {
        Section("Memória RAM");
        var totalBytes = ctx.Ram.Aggregate(0UL, (total, module) => total + module.CapacityBytes);
        var totalGb = Math.Round(totalBytes / 1024d / 1024d / 1024d, 1);
        Info($"Total: {totalGb} GB em {ctx.Ram.Count} pente(s)");

        foreach (var module in ctx.Ram)
        {
            Info($"  {module.Locator} | {BytesToGb(module.CapacityBytes)} GB | rated {module.SpeedMHz} MHz | atual {module.ConfiguredMHz} MHz | {module.Manufacturer}");
        }

        var locations = ctx.Ram.Select(r => r.Locator).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        if (ctx.Ram.Count >= 2 && locations < ctx.Ram.Count)
            Info("A BIOS reporta o mesmo nome de slot para os pentes; não dá para confirmar os slots por script. Use o CPU-Z.");

        if (ctx.Ram.Count == 1)
        {
            Bad("Apenas 1 pente de RAM = single channel. Use 2 pentes idênticos (dual channel) - um dos maiores ganhos no CS2.");
        }
        else if (ctx.Ram.Count >= 2)
        {
            Ok($"{ctx.Ram.Count} pentes (confirme no manual da placa se estão nos slots corretos, em geral A2 e B2 / slots 2 e 4).");
            Todo("Confirme no CPU-Z (aba Memory) que aparece \"Dual\" e que os pentes estão nos slots recomendados pelo manual.");
        }

        var memType = ctx.Ram.FirstOrDefault()?.SmbiosMemoryType ?? 0;
        var configured = ctx.Ram.Count > 0 ? ctx.Ram.Max(r => r.ConfiguredMHz) : 0;

        if (memType == 26 && configured <= 2666)
        {
            Bad($"DDR4 rodando a {configured} MHz: provavelmente XMP/DOCP desativado na BIOS.");
            Todo("Ativar XMP/DOCP/EXPO na BIOS.");
        }
        else if (memType == 34 && configured <= 4800)
        {
            Bad($"DDR5 rodando a {configured} MHz: provavelmente XMP/EXPO desativado na BIOS.");
            Todo("Ativar XMP/EXPO na BIOS.");
        }
        else if (memType == 34 && configured > 6400)
        {
            Warn("DDR5 acima de 6400 MHz: em Ryzen pode cair em UCLK = MCLK/2. Confira MCLK x UCLK no Zen Timings.");
        }
        else
        {
            Ok($"Frequência da RAM parece aplicada ({configured} MHz).");
        }
    }

    private void RunGpu(FixContext ctx)
    {
        Section("GPU / Vídeo");
        foreach (var gpu in ctx.Gpus)
        {
            Info($"{gpu.Name} | driver {gpu.DriverVersion} | {gpu.HorizontalResolution}x{gpu.VerticalResolution} @ {gpu.RefreshRate} Hz");
        }

        foreach (var line in ctx.NvidiaInfoLines) Info(line);

        if (ctx.HasAmd)
        {
            Warn("GPU AMD: Smart Access Memory/Resizable BAR vale para todos os jogos; no CS2 (limitado por CPU) pode até perder desempenho. Teste ligado e desligado.");
            Todo("AMD Adrenalin: resetar para padrão; desligar Radeon Boost / Super Resolution / Anti-Lag+ e overlay.");
        }

        if (ctx.HasNvidia)
        {
            Todo("NVIDIA - instalação limpa: baixe o driver antes, rode o DDU (modo seguro, \"Limpar e reiniciar\") e instale só o driver, SEM o NVIDIA App. Nunca instale um driver por cima do outro.");
            Todo("NVIDIA App/GeForce Experience: desative o overlay (Alt+Z), filtros de jogo e o Instant Replay.");
            Todo("Painel NVIDIA > Gerenciar configurações 3D: global no padrão; no perfil do cs2.exe \"Modo de gerenciamento de energia\" = Preferir desempenho máximo. \"Modo de baixa latência\" desligado se usar o Reflex no jogo.");
            Todo("Painel NVIDIA > Alterar resolução: escolha a taxa máxima do monitor na seção \"PC\" (a seção Ultra HD/HD não mostra o maior Hz). Cor: \"Usar configurações de cor NVIDIA\".");
        }

        if (ctx.Gpus.Count > 1 && ctx.Gpus.Any(g =>
                g.Name.Contains("Intel", StringComparison.OrdinalIgnoreCase) ||
                g.Name.Contains("UHD", StringComparison.OrdinalIgnoreCase) ||
                g.Name.Contains("Radeon Graphics", StringComparison.OrdinalIgnoreCase)))
        {
            Warn("Mais de uma GPU (integrada + dedicada). Confirme que o monitor está ligado na placa dedicada, não na placa-mãe.");
        }

        Todo("Confirme a taxa de atualização real do monitor (OSD ou testufo.com). Se estiver abaixo do máximo, ajuste pelo painel do driver (não pelo Windows).");
        Todo("Windows > Sistema > Tela > Elementos gráficos: confirme que o cs2.exe está com \"Alto desempenho\" (o script define isso).");
    }

    private void RunStorage(FixContext ctx)
    {
        Section("Armazenamento");
        foreach (var volume in ctx.Volumes)
        {
            var line = $"{volume.Letter}: {volume.MediaType} | {BytesToGb(volume.FreeBytes)} GB livres de {BytesToGb(volume.TotalBytes)} GB ({volume.FreePercent}%)";
            if (volume.FreePercent < 15)
                Warn(line + " -> menos de 15% livre (ideal 15-20% em SSD)");
            else
                Info(line);
        }
    }

    private void RunSteam(FixContext ctx)
    {
        Section("Steam / CS2");
        var steam = ctx.Steam;

        if (!string.IsNullOrWhiteSpace(steam.SteamPath) && Directory.Exists(steam.SteamPath))
            Info("Steam: " + steam.SteamPath);
        else
            Warn("Steam não encontrada no registro.");

        if (!string.IsNullOrWhiteSpace(steam.Cs2Exe))
        {
            Ok("CS2: " + steam.Cs2Exe);
            var drive = char.ToUpperInvariant(steam.Cs2Exe[0]);
            if (ctx.SsdLetters.Count > 0 && !ctx.SsdLetters.Contains(drive))
                Bad($"O CS2 não está em um SSD (drive {drive}). Mova para um SSD.");
        }
        else
        {
            Warn("cs2.exe não encontrado nas bibliotecas Steam.");
        }

        if (!string.IsNullOrWhiteSpace(steam.Cs2Exe))
        {
            if (ctx.FsoDisabled)
                Warn($"cs2.exe tem 'Desativar otimizações de tela inteira' MARCADO ({ctx.FsoLayerPath}). O ideal é desmarcado.");
            else
                Ok("Otimizações de tela inteira do cs2.exe: não desativadas.");
        }

        Info($"Cache de shaders (DirectX/driver/Steam): ~{ctx.ShaderMB} MB");
        Info($"Pastas de config do CS2 (userdata\\...\\730): {steam.Userdata730.Count}");
    }

    private void RunMpo(FixContext ctx)
    {
        Section("MPO / DWM");
        if (ctx.OverlayTestMode is not null)
            Bad($"OverlayTestMode existe em {ctx.DwmPath} (valor {ctx.OverlayTestMode}) -> MPO desativado.");
        else
            Ok("OverlayTestMode não existe (MPO habilitado).");
    }

    private void RunBcd(FixContext ctx)
    {
        Section("Timer / HPET (BCDEdit)");
        if (ctx.BcdKeys.Count == 0)
            Ok("Nenhuma alteração de clock/tick no BCD (padrão do sistema = TSC).");

        foreach (var (key, value) in ctx.BcdKeys)
        {
            if (key.Equals("useplatformclock", StringComparison.OrdinalIgnoreCase) &&
                Regex.IsMatch(value, "^(no|false)$", RegexOptions.IgnoreCase))
            {
                Ok($"useplatformclock = {value} (HPET não forçado).");
            }
            else if (key.Equals("useplatformclock", StringComparison.OrdinalIgnoreCase))
            {
                Bad($"useplatformclock = {value} -> HPET FORÇADO.");
            }
            else
            {
                Bad($"{key} = {value} -> ajuste de timer fora do padrão.");
            }
        }
    }

    private void RunPowerAndShell(FixContext ctx)
    {
        Section("Energia / Inicialização rápida / Game Bar");
        Info("Plano ativo: " + ctx.ActivePowerGuid);

        if (ctx.OnPerformancePowerPlan)
            Ok($"Plano de energia de alto desempenho{(ctx.MinAcValue >= 0 ? $" (estado mínimo do processador em CA: {ctx.MinAcValue}%)" : "")}.");
        else
            Warn($"Plano de energia não é Alto/Desempenho máximo{(ctx.MinAcValue >= 0 ? $" (estado mínimo do processador em CA: {ctx.MinAcValue}%)" : "")}.");

        if (ctx.FastBoot == 1) Warn("Inicialização rápida ATIVADA (pode causar problemas).");
        else Ok("Inicialização rápida desativada.");

        if (ctx.GameDvrAppCapture == 1 || ctx.GameDvrEnabled == 1)
            Warn("Capturas do Xbox Game Bar / GameDVR ativadas.");
        else
            Ok("Capturas do Game Bar desativadas.");

        if (ctx.GameMode == 0) Warn("Modo de Jogo DESATIVADO (deveria ficar ativo).");
        else Ok("Modo de Jogo ativo/padrão.");

        if (ctx.IsWin11)
        {
            if (ctx.Hags == 2)
                Ok("HAGS ativado. No CS2 costuma ser neutro (em alguns PCs melhora o 1% low, em outros piora): se tiver stutter, teste desligado.");
            else
                Ok("HAGS desativado. É uma escolha válida no Windows 11: se o 1% low está bom, deixe assim.");
        }
        else
        {
            if (ctx.Hags == 2)
                Info("HAGS ativado no Windows 10 (existe aqui também, não é exclusivo do 11). Teste se melhora o 1% low; se piorar, desligue.");
            else
                Ok("HAGS desativado (padrão do Windows 10).");
        }

        if (ctx.BackgroundAppsDisabled) Ok("Apps do Windows em segundo plano desativados.");
        else Warn("Apps do Windows podem rodar em segundo plano.");

        if (ctx.MouseSpeed != "0") Warn("Aceleração do mouse ('Aprimorar precisão do ponteiro') ATIVADA.");
        else Ok("Aceleração do mouse desativada.");
        Todo("Mouse: polling rate de 1000-4000 Hz (2K/4K costuma ser o ideal; acima disso só gera carga extra).");

        if (ctx.SystemResponsiveness is null || ctx.NetworkThrottling is null)
            Info("MMCSS (prioridade multimídia/rede): padrão do Windows.");
        else if (ctx.SystemResponsiveness == 0 && ctx.NetworkThrottling == -1)
            Ok("MMCSS já prioriza jogos (SystemResponsiveness=0, NetworkThrottlingIndex=-1).");
        else
            Info($"MMCSS ajustado parcialmente (SystemResponsiveness={ctx.SystemResponsiveness}, NetworkThrottlingIndex={ctx.NetworkThrottling}).");

        if (!string.IsNullOrWhiteSpace(ctx.Cs2Exe))
        {
            if (ctx.GpuPreference?.Contains("GpuPreference=2", StringComparison.OrdinalIgnoreCase) == true)
                Ok("cs2.exe já está com preferência 'Alto desempenho'.");
            else
                Warn("cs2.exe sem preferência de GPU 'Alto desempenho' nas Configurações Gráficas.");
        }
    }

    private void RunTelemetry(FixContext ctx)
    {
        Section("Telemetria do Windows");

        if (ctx.TelemetryServiceStart == 4) Ok("Serviço de telemetria (DiagTrack) desativado.");
        else Warn("Serviço de telemetria (DiagTrack) LIGADO (envia dados de uso para a Microsoft).");

        if (ctx.TelemetryLevel == 0) Ok("Dados de diagnóstico no mínimo (AllowTelemetry = 0).");
        else if (ctx.TelemetryLevel is null) Warn("Dados de diagnóstico no padrão do Windows (envia o pacote completo).");
        else Warn($"Dados de diagnóstico em nível {ctx.TelemetryLevel} (o mínimo é 0).");

        if (ctx.TelemetryAdId == 0) Ok("ID de publicidade desligado.");
        else Warn("ID de publicidade ligado (usado para anúncio/rastreio entre apps).");

        if (ctx.TelemetryWer == 1) Ok("Envio de relatórios de erro (WER) desligado.");
        else Info("Relatórios de erro: o Windows ainda envia pacotes de falha para a Microsoft.");

        if (ctx.TelemetryVoice == 0) Ok("Dados de voz (reconhecimento online) desligados.");
        else Info("Reconhecimento de voz online: o Windows pode enviar áudio para a Microsoft.");

        if (ctx.TelemetryCompatTask is not ("Disabled" or "Ausente"))
            Warn($"Tarefa de telemetria ativa: 'Microsoft Compatibility Appraiser' ({ctx.TelemetryCompatTask}).");
    }

    private void RunMitigations(FixContext ctx)
    {
        var state = ctx.Mit;
        Section("Mitigações de CPU (Spectre/Meltdown) - leitura do registro");
        Info($"CPU: {state.Cpu} [{state.Vendor}]");

        if (state.IsDefault)
            Ok("Sem sobrescritas: o Windows usa o padrão (proteções ligadas conforme a CPU exige).");
        else if (state.IsReduced)
            Warn($"Proteção REDUZIDA por sobrescrita (FeatureSettingsOverride = {state.Override}): Spectre v2 e/ou Meltdown desligados.");
        else
            Info($"Sobrescrita presente (FeatureSettingsOverride = {state.Override}): " +
                 (state.IsEffective ? "proteções adicionais ligadas." : "possivelmente sem efeito."));

        if (!string.IsNullOrWhiteSpace(state.Warning)) Warn(state.Warning);
        Info("Valores do registro só valem depois de reiniciar. Gerencie na aba \"Mitigações CPU\".");
    }

    private void RunSecurity(FixContext ctx)
    {
        Section("Segurança (VBS / HVCI) - só informativo");
        if (!ctx.HvciRead)
        {
            Info("Não foi possível ler o estado do VBS/HVCI.");
        }
        else if (ctx.HvciActive)
        {
            Info("Integridade de memória (HVCI/VBS) ATIVA: custa alguns % de FPS. Desligar mexe na Segurança do Windows (Isolamento de núcleo) e reduz proteção - decisão sua, o script não altera.");
            Todo("Segurança do Windows > Segurança do dispositivo > Isolamento de núcleo > Integridade de memória: desligar devolve alguns % de FPS, mas tira uma camada de proteção do kernel. Decida.");
        }
        else
        {
            Ok("Integridade de memória (HVCI) desligada.");
        }
    }

    private void RunProcesses(FixContext ctx)
    {
        Section("Processos e programas de inicialização");
        if (ctx.RunningOverlayProcesses.Count > 0)
            Warn("Rodando agora (podem causar overlay/conflito): " + string.Join(", ", ctx.RunningOverlayProcesses));
        else
            Ok("Nenhum overlay/app conhecido rodando.");

        const string suspect = "RGB|iCUE|Razer|Synapse|Armoury|Aura|SignalRGB|G HUB|LGHUB|MSI Center|Dragon Center|Afterburner|HWiNFO|Overwolf|Telemetr|Spotify|Teams|Adobe|Updater";
        var flagged = ctx.StartupCommands
            .Where(c => Regex.IsMatch(c, suspect, RegexOptions.IgnoreCase))
            .ToArray();

        Info($"Programas de inicialização: {ctx.StartupCommands.Count}");
        if (flagged.Length > 0)
        {
            var names = flagged.Select(f => f.Split('|')[0].Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase);
            Warn("Suspeitos na inicialização (RGB/telemetria/updaters): " + string.Join(", ", names));
            Todo("Gerenciador de Tarefas > Inicializar: desative RGB, telemetria, updaters e o que não for essencial.");
        }

        Todo("Steam > Configurações > Em jogo: desative o overlay (ao menos para o CS2) e as gravações.");
        Todo("Steam > CS2 > Propriedades > Opções de inicialização: +fps_max 0 -nojoy. -high é opcional (pode piorar com outros apps abertos) e -novid não faz mais nada no CS2.");
        Todo("Opcional: teste -vulkan (troca DirectX 11 por Vulkan). Pode ganhar ou perder FPS e atrapalhar o alt-tab; desfaça se piorar.");
        Todo("Discord: sobreposição do jogo = OFF e feche para a bandeja ao jogar. Aceleração de hardware: só desligue se der stutter/overlay travando — desligada, ela joga carga na CPU.");
        Todo("Chrome/Edge: se deixa aberto durante o jogo, feche abas pesadas e desative \"Continuar executando apps em segundo plano\". A aceleração gráfica só vale desligar se causar conflito de overlay.");
        Todo("NVIDIA App / Adrenalin: desative o overlay do driver.");
        Todo("Medir resultado: mapa \"CS2 FPS Benchmark\" da Oficina, anote FPS médio e 1% low (o 1% low importa mais que o médio).");
    }

    private static double BytesToGb(ulong bytes) => Math.Round(bytes / 1024d / 1024d / 1024d, 1);
}
