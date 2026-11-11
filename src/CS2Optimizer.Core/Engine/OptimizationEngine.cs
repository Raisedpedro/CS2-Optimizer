using CS2Optimizer.Core.Diagnostics;
using CS2Optimizer.Core.Fixes;
using CS2Optimizer.Core.Hardware;
using CS2Optimizer.Core.Mitigations;
using CS2Optimizer.Core.Models;
using CS2Optimizer.Core.Services;

namespace CS2Optimizer.Core.Engine;

public enum RestorePointChoice
{
    Yes,
    No,
    Cancel
}

public interface IInteraction
{
    bool Confirm(string question, FixLevel level);
    bool ConfirmBatch(IReadOnlyList<FixDefinition> fixes, bool willBackup);
    bool ConfirmRisky(FixDefinition fix);
    RestorePointChoice AskRestorePoint();
    bool ConfirmRestoreBackup(string path);
}

public sealed class ApplyOptions
{
    public bool ConfirmBatch { get; init; }
    public bool Auto { get; init; }
    public bool ReportOnly { get; init; }
    public bool SkipRiskyConfirmation { get; init; }
    public bool SkipBatchConfirmation { get; init; }

    /// <summary>Limpeza de cache: nada de registro/BCD, então não há o que restaurar.</summary>
    public bool SkipBackup { get; init; }

    /// <summary>Limpeza de cache não muda configuração: dispensar o ponto de restauração.</summary>
    public bool SkipRestorePoint { get; init; }
}

public sealed record ScanResult(
    FixContext Context,
    DiagnosticResult Diagnostics,
    IReadOnlyList<FixDefinition> Fixes);

public sealed record ApplyResult(
    IReadOnlyList<string> Applied,
    IReadOnlyList<string> Skipped,
    IReadOnlyList<string> Failed,
    bool NeedReboot);

/// <summary>Fluxo de aplicar da seção 5.4: confirmações, ponto, backup, aplicação, journal.</summary>
public sealed class OptimizationEngine
{
    private readonly AppLogger _log;
    private readonly RegistryService _registry;
    private readonly RegistryJournal _journal;
    private readonly ExternalProcessRunner _runner;
    private readonly MitigationService _mitigation;
    private readonly SystemScanner _scanner;
    private readonly DiagnosticEngine _diagnostics;
    private FixContext? _lastContext;

    public OptimizationEngine(
        AppLogger log,
        RegistryService registry,
        RegistryJournal journal,
        ExternalProcessRunner runner,
        BackupService backup,
        MitigationService mitigation,
        SystemScanner scanner,
        DiagnosticEngine diagnostics)
    {
        _log = log;
        _registry = registry;
        _journal = journal;
        _runner = runner;
        Backup = backup;
        _mitigation = mitigation;
        _scanner = scanner;
        _diagnostics = diagnostics;
    }

    public BackupService Backup { get; }
    public Action<string>? StatusChanged { get; set; }
    public string? LastCs2Exe { get; private set; }
    public bool NeedReboot { get; private set; }

    public async Task<ScanResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        StatusChanged?.Invoke("Lendo a máquina...");
        var context = await _scanner.ScanAsync(cancellationToken);
        _lastContext = context;
        LastCs2Exe = context.Cs2Exe;
        var diagnostics = _diagnostics.Run(context);
        var fixes = FixCatalog.Build(context);
        StatusChanged?.Invoke("Diagnóstico concluído.");
        return new ScanResult(context, diagnostics, fixes);
    }

    public async Task<ApplyResult> ApplyAsync(
        IReadOnlyList<FixDefinition> fixes,
        IInteraction interaction,
        ApplyOptions options,
        CancellationToken cancellationToken = default,
        Action<int, int>? progress = null)
    {
        var applied = new List<string>();
        var skipped = new List<string>();
        var failed = new List<string>();

        if (fixes.Count == 0)
        {
            _log.Warn("Nada marcado para aplicar.");
            return new ApplyResult(applied, skipped, failed, false);
        }

        if (options.ReportOnly)
        {
            _log.Warn("Modo somente relatório: nada foi alterado.");
            return new ApplyResult(applied, skipped, failed, false);
        }

        // 1) Itens INSEGUROS: confirmação individual, padrão "Não".
        var selected = new List<FixDefinition>();
        foreach (var fix in fixes)
        {
            if (fix.Level == FixLevel.Risky && !options.SkipRiskyConfirmation && !options.Auto)
            {
                if (!interaction.ConfirmRisky(fix))
                {
                    skipped.Add(fix.Title);
                    _log.Warn("PULADO (inseguro, não confirmado)  " + fix.Title);
                    continue;
                }
            }
            selected.Add(fix);
        }

        if (selected.Count == 0)
        {
            _log.Warn("Nada para aplicar.");
            return new ApplyResult(applied, skipped, failed, false);
        }

        // 2) Confirmação em lote (opcional).
        if (options.ConfirmBatch && !options.SkipBatchConfirmation && !options.Auto)
        {
            if (!interaction.ConfirmBatch(selected, !options.SkipBackup))
            {
                _log.Warn("Cancelado - nada foi alterado.");
                return new ApplyResult(applied, skipped, failed, false);
            }
        }

        // 3) Ponto de restauração: sempre perguntado, nunca automático.
        var createPoint = false;
        if (options.SkipRestorePoint)
        {
            // Limpeza de cache: não há configuração para restaurar.
        }
        else if (options.Auto)
        {
            createPoint = true;
        }
        else
        {
            var choice = interaction.AskRestorePoint();
            if (choice == RestorePointChoice.Cancel)
            {
                _log.Warn("Cancelado - nada foi alterado.");
                return new ApplyResult(applied, skipped, failed, false);
            }
            createPoint = choice == RestorePointChoice.Yes;
        }

        // 4) Backup antes de qualquer escrita.
        if (!options.SkipBackup && !Backup.BackupMade)
        {
            var stamp = BackupService.NewStamp();
            await Backup.CreateBackupAsync(stamp, createPoint, cancellationToken);
        }

        // 5) Aplicação.
        var context = new FixExecutionContext
        {
            Registry = _registry,
            Journal = _journal,
            Runner = _runner,
            Log = _log,
            Backup = Backup,
            Mitigation = _mitigation,
            BaseDirectory = Backup.BaseDirectory,
            Desktop = Backup.Desktop,
            Auto = options.Auto,
            ReportOnly = options.ReportOnly,
            Ask = (question, level) =>
            {
                if (options.Auto) return level == FixLevel.Safe;
                return interaction.Confirm(question, level);
            },
            SetStatus = StatusChanged,
            CancellationToken = cancellationToken,
            Cs2Exe = LastCs2Exe
        };

        var machineContext = _lastContext ?? new FixContext();
        var needReboot = false;
        var done = 0;
        foreach (var fix in selected)
        {
            try
            {
                _log.Title("aplicando: " + fix.Title);
                if (fix.ApplyAsync is null)
                    throw new InvalidOperationException("Correção sem ação definida.");

                await fix.ApplyAsync(machineContext, context);
                applied.Add(fix.Title);
                if (fix.RequiresReboot) needReboot = true;
                _log.Ok("OK  " + fix.Title);
            }
            catch (Exception ex)
            {
                failed.Add(fix.Title + " -> " + ex.Message);
                _log.Bad("FALHOU  " + fix.Title + " -> " + ex.Message);
            }

            progress?.Invoke(++done, selected.Count);
        }

        // 6) Journal.
        if (Backup.CurrentBackupDirectory is not null)
            _journal.SaveTo(Backup.CurrentBackupDirectory, _log.Log);

        NeedReboot |= needReboot;
        if (applied.Count > 0)
            _log.Ok($"{applied.Count} correção(ões) aplicada(s).");

        StatusChanged?.Invoke("Pronto.");
        return new ApplyResult(applied, skipped, failed, needReboot);
    }

    public async Task RestoreBackupAsync(string directory, CancellationToken cancellationToken = default)
    {
        var cs2 = LastCs2Exe;
        if (string.IsNullOrWhiteSpace(cs2))
        {
            var context = await _scanner.ScanAsync(cancellationToken);
            cs2 = context.Cs2Exe;
        }

        await Backup.RestoreBackupAsync(directory, cs2, cancellationToken);
    }

    public IReadOnlyList<BackupInfo> GetBackups() => Backup.FindBackups();

    public string GetLogFile() => _log.LogFile;
}
