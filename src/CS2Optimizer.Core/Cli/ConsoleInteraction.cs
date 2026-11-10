using System.Text;
using CS2Optimizer.Core.Engine;
using CS2Optimizer.Core.Models;

namespace CS2Optimizer.Core.Cli;

public sealed class ConsoleInteraction : IInteraction
{
    public bool Confirm(string question, FixLevel level)
    {
        var dangerous = level is FixLevel.Destructive or FixLevel.Risky;
        var suffix = dangerous ? "[s/N]" : "[S/n]";
        Console.Write($"  >> {question} {suffix} ");
        var answer = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(answer)) return !dangerous;
        return answer.Trim().ToLowerInvariant() is "s" or "sim" or "y" or "yes";
    }

    public bool ConfirmBatch(IReadOnlyList<FixDefinition> fixes)
    {
        var labels = new Dictionary<FixLevel, string>
        {
            [FixLevel.Safe] = "seguro",
            [FixLevel.Optional] = "opcional",
            [FixLevel.Destructive] = "cuidado",
            [FixLevel.Risky] = "INSEGURO"
        };

        var sb = new StringBuilder();
        sb.AppendLine($"Vou aplicar {fixes.Count} correção(ões):");
        sb.AppendLine();
        foreach (var fix in fixes)
        {
            var reboot = fix.RequiresReboot ? "  (pede reinício)" : "";
            sb.AppendLine($"  - [{labels[fix.Level]}] {fix.Title}{reboot}");
        }

        var risky = fixes.Where(f => f.Level == FixLevel.Risky).ToArray();
        if (risky.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine("ITENS INSEGUROS (reduzem a proteção do PC):");
            foreach (var item in risky) sb.AppendLine("  - " + item.Title + ": " + item.Detail);
        }

        var destructive = fixes.Count(f => f.Level == FixLevel.Destructive);
        if (destructive > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"Obs.: {destructive} item(ns) pedem mais cuidado (reset de rede / configs do CS2). O backup devolve registro/BCD/plano de energia, mas não desfaz esses dois.");
        }

        sb.AppendLine();
        sb.Append("Antes de começar é feito o backup (registro, BCD e plano de energia). O ponto de restauração é perguntado logo em seguida.");
        Console.WriteLine();
        Console.WriteLine(sb.ToString());
        Console.WriteLine();

        var level = risky.Length > 0 ? FixLevel.Risky : FixLevel.Safe;
        return Confirm("Aplicar agora?", level);
    }

    public bool ConfirmRisky(FixDefinition fix)
    {
        Console.WriteLine();
        Console.WriteLine("ITEM INSEGURO: " + fix.Title);
        Console.WriteLine();
        Console.WriteLine(fix.Explanation);
        Console.WriteLine();
        Console.WriteLine("Enquanto estiver aplicado, o PC fica menos protegido. O backup permite voltar atrás. Aplicar mesmo assim?");
        return Confirm("Aplicar item inseguro?", FixLevel.Risky);
    }

    public RestorePointChoice AskRestorePoint()
    {
        Console.WriteLine();
        Console.WriteLine("Criar um ponto de restauração do Windows antes de aplicar?");
        Console.WriteLine("É a rede de segurança do Windows: se algo der errado, dá para voltar o sistema inteiro pelo 'Restaurar sistema'.");
        Console.WriteLine("Leva de 1 a 3 minutos e o Windows só permite 1 ponto a cada 24h.");
        Console.WriteLine("Sem o ponto, o backup do registro, do BCD e do plano de energia continua sendo feito do mesmo jeito.");
        Console.Write("  >> Criar ponto de restauração? [S/n] ");
        var answer = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(answer)) return RestorePointChoice.Yes;
        return answer.Trim().ToLowerInvariant() is "s" or "sim" or "y" or "yes"
            ? RestorePointChoice.Yes
            : RestorePointChoice.No;
    }

    public bool ConfirmRestoreBackup(string path)
    {
        Console.WriteLine();
        Console.WriteLine("Restaurar o registro/BCD/plano de energia a partir de:");
        Console.WriteLine("  " + path);
        Console.WriteLine();
        Console.WriteLine("Isso desfaz os ajustes feitos naquele dia. Continuar?");
        return Confirm("Restaurar backup?", FixLevel.Destructive);
    }
}
