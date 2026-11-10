using System;

namespace CS2Optimizer.App.ViewModels;

/// <summary>
/// Ordem das abas em um lugar só. A navegação e o XAML usam a chave,
/// nunca o índice - assim mover uma aba não quebra nenhum atalho.
/// </summary>
public static class AppTabs
{
    public const string Inicio = "inicio";
    public const string Limpeza = "limpeza";
    public const string Correcoes = "correcoes";
    public const string Mitigacoes = "mitigacoes";
    public const string Oosu10 = "oosu10";
    public const string Diagnostico = "diagnostico";
    public const string Checklist = "checklist";
    public const string Backups = "backups";

    public static readonly IReadOnlyList<string> Order = new[]
    {
        Inicio, Limpeza, Correcoes, Mitigacoes, Oosu10, Diagnostico, Checklist, Backups
    };

    public static int IndexOf(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return 0;

        for (var i = 0; i < Order.Count; i++)
        {
            if (string.Equals(Order[i], key, StringComparison.OrdinalIgnoreCase)) return i;
        }

        return 0;
    }

    public static string LabelOf(string? key) => key switch
    {
        Limpeza => "Limpeza",
        Correcoes => "Correções",
        Mitigacoes => "Mitigações",
        Oosu10 => "O&O ShutUp10",
        Diagnostico => "Diagnóstico",
        Checklist => "Checklist",
        Backups => "Backups",
        _ => "Início"
    };
}
