namespace CS2Optimizer.Core.Models;

/// <summary>
/// Definição de uma correção. A lógica (IsApplicable/Apply) é resolvida pelo catálogo,
/// que já recebe o contexto da máquina e só cria os itens aplicáveis.
/// </summary>
public sealed class FixDefinition
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string CurrentState { get; init; }
    public required string Detail { get; init; }
    public required string Explanation { get; init; }
    public required FixLevel Level { get; init; }
    public bool RequiresReboot { get; init; }
    public required string Category { get; init; }
    public bool Recommended { get; init; }

    public Func<FixContext, FixExecutionContext, Task>? ApplyAsync { get; init; }

    public override string ToString() => $"{Id} ({Level}) {Title}";
}
