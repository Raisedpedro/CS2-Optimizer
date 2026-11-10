using CS2Optimizer.Core.Mitigations;
using CS2Optimizer.Core.Services;

namespace CS2Optimizer.Core.Models;

/// <summary>Serviços e callbacks disponíveis durante a aplicação de uma correção.</summary>
public sealed class FixExecutionContext
{
    public required RegistryService Registry { get; init; }
    public required RegistryJournal Journal { get; init; }
    public required ExternalProcessRunner Runner { get; init; }
    public required AppLogger Log { get; init; }
    public required BackupService Backup { get; init; }
    public required MitigationService Mitigation { get; init; }
    public required string BaseDirectory { get; init; }
    public required string Desktop { get; init; }

    public bool Auto { get; init; }
    public bool ReportOnly { get; init; }
    public Func<string, FixLevel, bool>? Ask { get; init; }
    public Action<string>? SetStatus { get; init; }
    public CancellationToken CancellationToken { get; init; }
    public string? Cs2Exe { get; init; }
}
