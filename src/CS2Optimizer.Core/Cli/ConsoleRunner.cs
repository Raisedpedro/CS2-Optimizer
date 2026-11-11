using CS2Optimizer.Core.Diagnostics;
using CS2Optimizer.Core.Engine;
using CS2Optimizer.Core.Models;

namespace CS2Optimizer.Core.Cli;

/// <summary>Opções da linha de comando, já validadas.</summary>
public sealed record CliOptions
{
    public bool ReportOnly { get; init; }
    public bool Auto { get; init; }
    public bool CleanOnly { get; init; }
    public bool NoPause { get; init; }
    public string? Revert { get; init; }
    public bool Help { get; init; }
    public string? Error { get; init; }
}

/// <summary>Modo texto/CLI: --report-only, --auto, --revert &lt;pasta&gt;, --clean-only.</summary>
public sealed class ConsoleRunner
{
    private static readonly HashSet<string> CleanOnlyIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "trim", "temp", "shaders", "syscache"
    };

    private readonly OptimizationEngine _engine;

    public ConsoleRunner(OptimizationEngine engine)
    {
        _engine = engine;
    }

    /// <summary>
    /// Lê os argumentos sem adivinhar: opção desconhecida, modo a mais ou
    /// --revert sem pasta viram erro (nunca caem no fluxo que aplica coisas).
    /// </summary>
    public static CliOptions Parse(string[] args)
    {
        var reportOnly = false;
        var auto = false;
        var cleanOnly = false;
        var noPause = false;
        var help = false;
        string? revert = null;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = (args[i] ?? "").Trim();
            if (arg.Length == 0) continue;

            switch (arg.ToLowerInvariant())
            {
                case "--report-only" or "-reportonly":
                    reportOnly = true;
                    break;
                case "--auto" or "-auto":
                    auto = true;
                    break;
                case "--clean-only" or "-cleanonly":
                    cleanOnly = true;
                    break;
                case "--no-pause" or "-nopause":
                    noPause = true;
                    break;
                case "--help" or "-h" or "-?" or "/?":
                    help = true;
                    break;
                case "--revert" or "-revert":
                    if (i + 1 >= args.Length || string.IsNullOrWhiteSpace(args[i + 1]))
                        return new CliOptions { Error = "Faltou a pasta depois de --revert." };
                    revert = args[++i];
                    break;
                default:
                    return new CliOptions { Error = $"Opção desconhecida: {arg}" };
            }
        }

        var modes = (reportOnly ? 1 : 0) + (auto ? 1 : 0) + (cleanOnly ? 1 : 0) +
                    (revert is null ? 0 : 1);
        if (modes > 1)
            return new CliOptions { Error = "Use só um modo por vez: --report-only, --auto, --clean-only ou --revert <pasta>." };

        return new CliOptions
        {
            ReportOnly = reportOnly,
            Auto = auto,
            CleanOnly = cleanOnly,
            NoPause = noPause,
            Revert = revert,
            Help = help
        };
    }

    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        ConsoleHelper.EnsureConsole();

        var options = Parse(args);
        if (options.Help)
        {
            PrintUsage();
            return 0;
        }

        if (!string.IsNullOrWhiteSpace(options.Error))
        {
            ConsoleHelper.Write("Erro: " + options.Error, ConsoleColor.Red);
            ConsoleHelper.Write("");
            PrintUsage();
            return 2;
        }

        var reportOnly = options.ReportOnly;
        var auto = options.Auto;
        var cleanOnly = options.CleanOnly;
        var noPause = options.NoPause;
        var revert = options.Revert;

        PrintBanner(reportOnly, auto, cleanOnly);

        _engine.StatusChanged = text => ConsoleHelper.Write("  " + text, ConsoleColor.DarkGray);

        try
        {
            if (!string.IsNullOrWhiteSpace(revert))
            {
                var interaction = new ConsoleInteraction();
                if (!interaction.ConfirmRestoreBackup(revert)) return 0;
                await _engine.RestoreBackupAsync(revert, cancellationToken);
                ConsoleHelper.Write("Backup restaurado. Reinicie o PC.", ConsoleColor.Green);
                WaitForKey(noPause);
                return 0;
            }

            ConsoleHelper.Write("");
            ConsoleHelper.Write("################ FASE 1: DIAGNÓSTICO ################", ConsoleColor.Magenta);
            var scan = await _engine.ScanAsync(cancellationToken);
            PrintDiagnostics(scan.Diagnostics.Entries);

            if (reportOnly)
            {
                ConsoleHelper.Write("");
                ConsoleHelper.Write("Modo -ReportOnly: nenhuma alteração será feita.", ConsoleColor.Yellow);
                PrintChecklist(scan.Diagnostics.Manual, false);
                WaitForKey(noPause);
                return 0;
            }

            IEnumerable<FixDefinition> fixes = scan.Fixes;
            if (auto) fixes = fixes.Where(f => f.Level == FixLevel.Safe);
            if (cleanOnly) fixes = fixes.Where(f => CleanOnlyIds.Contains(f.Id) && f.Level != FixLevel.Risky);
            var selected = fixes.ToList();

            ConsoleHelper.Write("");
            ConsoleHelper.Write("################ FASE 2: CORREÇÕES ################", ConsoleColor.Magenta);
            if (selected.Count == 0)
            {
                ConsoleHelper.Write("Nada a corrigir por aqui.", ConsoleColor.Green);
            }
            else
            {
                var applyOptions = new ApplyOptions
                {
                    ConfirmBatch = !auto,
                    Auto = auto,
                    ReportOnly = reportOnly,
                    // --clean-only mexe só em cache: sem backup e sem ponto de restauração.
                    SkipBackup = cleanOnly,
                    SkipRestorePoint = cleanOnly
                };
                var result = await _engine.ApplyAsync(selected, new ConsoleInteraction(), applyOptions, cancellationToken);
                PrintResult(result);
                PrintChecklist(scan.Diagnostics.Manual, result.NeedReboot);
            }

            ConsoleHelper.Write("");
            ConsoleHelper.Write("Backup: " + (_engine.Backup.CurrentBackupDirectory ?? "(nenhum)"), ConsoleColor.Gray);
            ConsoleHelper.Write("Log:    " + _engine.GetLogFile(), ConsoleColor.Gray);
            ConsoleHelper.Write("");
            ConsoleHelper.Write("Para reverter: use o botão \"Desfazer backup\" na interface ou rode --revert <pasta>.", ConsoleColor.Gray);
            WaitForKey(noPause);
            return 0;
        }
        catch (OperationCanceledException)
        {
            ConsoleHelper.Write("Cancelado.", ConsoleColor.Yellow);
            return 1;
        }
        catch (Exception ex)
        {
            ConsoleHelper.Write("Erro: " + ex.Message, ConsoleColor.Red);
            return 1;
        }
    }

    private static void PrintUsage()
    {
        ConsoleHelper.Write("Uso: CS2Optimizer.exe [modo] [--no-pause]", ConsoleColor.Cyan);
        ConsoleHelper.Write("");
        ConsoleHelper.Write("  (sem modo)          abre a janela do programa", ConsoleColor.Gray);
        ConsoleHelper.Write("  --report-only       só diagnóstico; não altera nada", ConsoleColor.Gray);
        ConsoleHelper.Write("  --auto              aplica só itens seguros, sem perguntar", ConsoleColor.Gray);
        ConsoleHelper.Write("  --clean-only        só limpeza (trim/temp/shaders/syscache)", ConsoleColor.Gray);
        ConsoleHelper.Write("  --revert <pasta>    restaura um backup", ConsoleColor.Gray);
        ConsoleHelper.Write("  --no-pause          não espera ENTER no fim (para scripts)", ConsoleColor.Gray);
        ConsoleHelper.Write("  --help              mostra esta ajuda", ConsoleColor.Gray);
    }

    private static void PrintBanner(bool reportOnly, bool auto, bool cleanOnly)
    {
        ConsoleHelper.Write("################################################", ConsoleColor.Magenta);
        ConsoleHelper.Write("#          CS2 / Windows Optimizer             #", ConsoleColor.Magenta);
        ConsoleHelper.Write("################################################", ConsoleColor.Magenta);
        if (reportOnly) ConsoleHelper.Write("Modo: SOMENTE RELATÓRIO (nada será alterado)", ConsoleColor.Yellow);
        else if (auto) ConsoleHelper.Write("Modo: AUTO (só correções seguras, sem perguntar)", ConsoleColor.Yellow);
        else if (cleanOnly) ConsoleHelper.Write("Modo: LIMPEZA (só trim/temp/shaders/syscache)", ConsoleColor.Yellow);
        else ConsoleHelper.Write("Modo: INTERATIVO (cada lote pede confirmação)", ConsoleColor.Yellow);
    }

    private static void PrintDiagnostics(IReadOnlyList<DiagnosticEntry> entries)
    {
        foreach (var entry in entries)
        {
            switch (entry.Kind)
            {
                case DiagnosticKind.Section:
                    ConsoleHelper.Write("");
                    ConsoleHelper.Write("=== " + entry.Text.ToUpperInvariant() + " ===", ConsoleColor.Cyan);
                    break;
                case DiagnosticKind.Info:
                    ConsoleHelper.Write("  [i] " + entry.Text, ConsoleColor.Gray);
                    break;
                case DiagnosticKind.Ok:
                    ConsoleHelper.Write("  [OK] " + entry.Text, ConsoleColor.Green);
                    break;
                case DiagnosticKind.Warn:
                    ConsoleHelper.Write("  [!] " + entry.Text, ConsoleColor.Yellow);
                    break;
                case DiagnosticKind.Bad:
                    ConsoleHelper.Write("  [X] " + entry.Text, ConsoleColor.Red);
                    break;
                case DiagnosticKind.Todo:
                    ConsoleHelper.Write("  [ ] " + entry.Text, ConsoleColor.White);
                    break;
            }
        }
    }

    private static void PrintChecklist(IReadOnlyList<string> manual, bool needReboot)
    {
        ConsoleHelper.Write("");
        ConsoleHelper.Write("=== Checklist MANUAL (o que o programa não consegue fazer ou verificar) ===", ConsoleColor.Cyan);
        foreach (var item in manual) ConsoleHelper.Write("  [ ] " + item, ConsoleColor.White);

        ConsoleHelper.Write("");
        ConsoleHelper.Write("=== Dentro do CS2 ===", ConsoleColor.Cyan);
        foreach (var tip in Cs2Tips.All) ConsoleHelper.Write("  - " + tip, ConsoleColor.White);

        if (needReboot)
        {
            ConsoleHelper.Write("");
            ConsoleHelper.Write("REINICIE O PC para concluir as alterações (BCD/MPO/HAGS/mouse).", ConsoleColor.Yellow);
        }
    }

    private static void PrintResult(ApplyResult result)
    {
        ConsoleHelper.Write("");
        ConsoleHelper.Write("################ FASE 3: RESUMO ################", ConsoleColor.Magenta);
        ConsoleHelper.Write("");
        ConsoleHelper.Write("=== Aplicado ===", ConsoleColor.Cyan);
        if (result.Applied.Count == 0) ConsoleHelper.Write("  [i] Nada foi alterado.", ConsoleColor.Gray);
        else foreach (var item in result.Applied) ConsoleHelper.Write("  [OK] " + item, ConsoleColor.Green);

        if (result.Skipped.Count > 0)
        {
            ConsoleHelper.Write("");
            ConsoleHelper.Write("=== Pulado ===", ConsoleColor.Cyan);
            foreach (var item in result.Skipped) ConsoleHelper.Write("  [i] " + item, ConsoleColor.Gray);
        }

        if (result.Failed.Count > 0)
        {
            ConsoleHelper.Write("");
            ConsoleHelper.Write("=== Falhou ===", ConsoleColor.Cyan);
            foreach (var item in result.Failed) ConsoleHelper.Write("  [X] " + item, ConsoleColor.Red);
        }
    }

    private static void WaitForKey(bool noPause)
    {
        if (noPause || Console.IsInputRedirected) return;
        ConsoleHelper.Write("");
        ConsoleHelper.Write("Pressione ENTER para fechar...", ConsoleColor.Cyan);
        try { Console.ReadLine(); } catch { }
    }
}
