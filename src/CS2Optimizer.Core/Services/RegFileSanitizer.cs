using System.Text;

namespace CS2Optimizer.Core.Services;

/// <summary>
/// Lê um .reg de backup e mantém somente as linhas que representam valores que
/// o programa escreve. Cabeçalho errado, exclusão de chave/valor e qualquer
/// valor fora da allowlist fazem o arquivo ser recusado. Assim um .reg plantado
/// numa pasta portátil não consegue gravar ImagePath, Run, tarefas etc. com o
/// processo elevado.
/// </summary>
public static class RegFileSanitizer
{
    private const string Header = "Windows Registry Editor Version 5.00";

    // Mesmo que apareçam numa chave exportada, estes valores nunca podem ser
    // importados: controlam o que o Windows executa.
    private static readonly HashSet<string> DangerousNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "ImagePath", "ServiceDll", "Shell", "UserInit", "AppInit_DLLs", "AppInit",
        "Debugger", "Load", "Run", "CommandLine", "DelegateExecute", "Dll", "Path"
    };

    public static bool TrySanitize(string source, string? target, out int keptValues)
    {
        keptValues = 0;
        var lines = File.ReadAllLines(source);
        var first = lines.FirstOrDefault(l => !string.IsNullOrWhiteSpace(l))?.Trim();
        if (!string.Equals(first, Header, StringComparison.Ordinal)) return false;

        var allowedPrefixes = RegistryExports.Map
            .Select(kv => RegistryWriteAllowlist.Normalize(kv.Value))
            .Where(p => p.Length > 0)
            .ToArray();

        var output = new StringBuilder();
        output.AppendLine(Header);
        output.AppendLine();

        var section = "";
        var wroteSection = false;
        var inHexValue = false;

        foreach (var raw in lines)
        {
            var text = raw.Trim();
            if (text.Length == 0 || text.StartsWith(";", StringComparison.Ordinal)) continue;

            if (text.StartsWith("[-", StringComparison.Ordinal)) return false;

            if (text.StartsWith("[", StringComparison.Ordinal))
            {
                section = text.Trim('[', ']');
                var normalized = RegistryWriteAllowlist.Normalize(section);
                if (!allowedPrefixes.Any(p =>
                        string.Equals(normalized, p, StringComparison.OrdinalIgnoreCase) ||
                        normalized.StartsWith(p + "\\", StringComparison.OrdinalIgnoreCase)))
                    return false;

                wroteSection = false;
                inHexValue = false;
                continue;
            }

            if (inHexValue && IsHexContinuation(raw))
            {
                output.AppendLine(raw.TrimEnd());
                continue;
            }
            inHexValue = false;

            var eq = text.IndexOf('=');
            if (eq <= 0) continue;

            var nameToken = text[..eq].Trim();
            var data = text[(eq + 1)..].Trim();
            if (nameToken == "-" || data == "-") return false; // exclusão de valor
            if (!nameToken.StartsWith('"') || !nameToken.EndsWith('"')) continue;

            var name = nameToken[1..^1];
            if (DangerousNames.Contains(name)) return false;
            if (!RegistryWriteAllowlist.IsAllowed(section, name)) continue;

            if (!wroteSection)
            {
                output.AppendLine("[" + section + "]");
                wroteSection = true;
            }
            output.AppendLine(raw.TrimEnd());
            keptValues++;
            inHexValue = data.StartsWith("hex", StringComparison.OrdinalIgnoreCase);
        }

        if (target is not null) File.WriteAllText(target, output.ToString(), Encoding.Unicode);
        return true;
    }

    private static bool IsHexContinuation(string line)
    {
        var t = line.Trim();
        if (t.Length == 0 || t.StartsWith("[", StringComparison.Ordinal)) return false;
        return char.IsWhiteSpace(line[0]) &&
               (t.StartsWith("hex", StringComparison.OrdinalIgnoreCase) ||
                t.All(c => char.IsAsciiHexDigit(c) || c == ',' || c == '\\'));
    }
}
