using System.IO;
using CS2Optimizer.Core.Diagnostics;
using CS2Optimizer.Core.Engine;
using CS2Optimizer.Core.Hardware;
using CS2Optimizer.Core.Mitigations;
using CS2Optimizer.Core.Oosu10;
using CS2Optimizer.Core.Services;

namespace CS2Optimizer.App;

public sealed class AppServices
{
    public required AppLogger Logger { get; init; }
    public required RegistryService Registry { get; init; }
    public required RegistryJournal Journal { get; init; }
    public required ExternalProcessRunner Runner { get; init; }
    public required BackupService Backup { get; init; }
    public required MitigationService Mitigation { get; init; }
    public required Oosu10Service Oosu10 { get; init; }
    public required SystemScanner Scanner { get; init; }
    public required DiagnosticEngine Diagnostics { get; init; }
    public required OptimizationEngine Engine { get; init; }

    public static AppServices Create()
    {
        var baseDir = AppPaths.BaseDirectory;
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var logger = new AppLogger(Path.Combine(
            AppPaths.EnsureDirectory(AppPaths.LogsDirectory),
            $"CS2-Optimizer-Log-{stamp}.txt"));

        var journal = new RegistryJournal();
        var registry = new RegistryService(journal);
        var runner = new ExternalProcessRunner();
        var backup = new BackupService(registry, runner, logger, baseDir);
        backup.MigrateLegacyBackups();
        var mitigation = new MitigationService(registry);
        var oosu10 = new Oosu10Service(registry);
        var scanner = new SystemScanner(registry, runner, mitigation, logger);
        var diagnostics = new DiagnosticEngine(logger);
        var engine = new OptimizationEngine(logger, registry, journal, runner, backup,
            mitigation, scanner, diagnostics);

        return new AppServices
        {
            Logger = logger,
            Registry = registry,
            Journal = journal,
            Runner = runner,
            Backup = backup,
            Mitigation = mitigation,
            Oosu10 = oosu10,
            Scanner = scanner,
            Diagnostics = diagnostics,
            Engine = engine
        };
    }
}
