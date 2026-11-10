namespace CS2Optimizer.Core.Models;

/// <summary>Nível de risco de uma correção (seção 5.1 do plano).</summary>
public enum FixLevel
{
    Safe,
    Optional,
    Destructive,
    Risky
}

public static class FixLevelExtensions
{
    public static string ToDisplay(this FixLevel level) => level switch
    {
        FixLevel.Safe => "Seguro",
        FixLevel.Optional => "Opcional",
        FixLevel.Destructive => "Cuidado",
        FixLevel.Risky => "Inseguro",
        _ => level.ToString()
    };

    public static string ToShortDisplay(this FixLevel level) => level switch
    {
        FixLevel.Safe => "seguro",
        FixLevel.Optional => "opcional",
        FixLevel.Destructive => "cuidado",
        FixLevel.Risky => "INSEGURO",
        _ => level.ToString()
    };
}
