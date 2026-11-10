using Microsoft.Win32;

namespace CS2Optimizer.Core.Oosu10;

/// <summary>Recomendação do O&amp;O ShutUp10 (y/n do arquivo .reg) ajustada para uso em jogo.</summary>
public enum Oosu10Advice
{
    Recomendado,
    Opcional,
    Evitar
}

public enum Oosu10State
{
    Ativo,
    Inativo,
    Parcial
}

/// <summary>Uma escrita (ou remoção) de valor de registro que compõe um ajuste.</summary>
public sealed record Oosu10RegAction(string Path, string Name, RegistryValueKind Kind, object? Value)
{
    public bool IsDelete => Value is null;

    public string Describe()
    {
        var where = Path + "\\" + (Name.Length == 0 ? "(padrão)" : Name);
        return IsDelete ? where + " = (remover)" : $"{where} = {Value}";
    }
}

/// <summary>
/// Um ajuste do O&amp;O ShutUp10 traduzido para o app. <see cref="Enable"/> é o estado
/// "ligado" (privacidade/redução) e <see cref="Restore"/> o estado alternativo do arquivo.
/// </summary>
public sealed class Oosu10Tweak
{
    public required string Id { get; init; }
    public required string Group { get; init; }
    public required string Title { get; init; }
    public required string Detail { get; init; }
    public string Explanation { get; init; } = "";
    public string Note { get; init; } = "";
    public required Oosu10Advice Advice { get; init; }
    public bool InPreset { get; init; }
    public bool RequiresReboot { get; init; }
    public required IReadOnlyList<Oosu10RegAction> Enable { get; init; }
    public IReadOnlyList<Oosu10RegAction> Restore { get; init; } = Array.Empty<Oosu10RegAction>();

    public bool NeedsAdmin =>
        Enable.Concat(Restore).Any(a => a.Path.StartsWith("HKLM", StringComparison.OrdinalIgnoreCase));

    public override string ToString() => $"{Id} ({Advice}) {Title}";
}

public sealed record Oosu10ApplyResult(
    IReadOnlyList<string> Applied,
    IReadOnlyList<string> Failed,
    int Writes);

public static class Oosu10Groups
{
    public const string Privacidade = "Privacidade";
    public const string Historico = "Histórico e Área de Transferência";
    public const string Permissoes = "Permissões de Apps";
    public const string Edge = "Microsoft Edge";
    public const string EdgeLegado = "Microsoft Edge (legado)";
    public const string Office = "Microsoft Office";
    public const string Sincronizacao = "Sincronização de Configurações";
    public const string Cortana = "Cortana e Busca";
    public const string Comportamento = "Telemetria e Comportamento";
    public const string Explorer = "Explorer e Barra de Tarefas";
    public const string TelaBloqueio = "Tela de Bloqueio";
    public const string Diversos = "Diversos";
    public const string Localizacao = "Localização";
    public const string Defender = "Defender e SpyNet";
    public const string Seguranca = "Segurança";
    public const string WindowsUpdate = "Windows Update";

    public static readonly string[] Order =
    {
        Privacidade, Historico, Permissoes, Edge, EdgeLegado, Office,
        Sincronizacao, Cortana, Comportamento, Explorer, TelaBloqueio,
        Diversos, Localizacao, Defender, Seguranca, WindowsUpdate
    };
}
