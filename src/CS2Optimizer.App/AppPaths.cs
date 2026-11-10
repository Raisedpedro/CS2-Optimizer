using System;
using System.IO;

namespace CS2Optimizer.App;

/// <summary>
/// Onde o programa grava o que é dele. A regra é simples: fica tudo junto do
/// <c>CS2Optimizer.exe</c> (modo portátil, que é como o zip é distribuído) —
/// <c>Backups\</c>, <c>logs\</c> e o <c>janela.json</c>. Só se a pasta do
/// programa não aceitar escrita (ex.: instalado em Program Files) é que cai
/// para <c>%LOCALAPPDATA%\CS2-Optimizer</c>.
/// </summary>
internal static class AppPaths
{
    private static readonly Lazy<string> LazyBase = new(ResolveBaseDirectory);

    /// <summary>Pasta base: a do executável quando dá para escrever nela.</summary>
    public static string BaseDirectory => LazyBase.Value;

    public static string BackupsDirectory => Path.Combine(BaseDirectory, "Backups");

    public static string LogsDirectory => Path.Combine(BaseDirectory, "logs");

    public static string WindowPlacementFile => Path.Combine(BaseDirectory, "janela.json");

    /// <summary>Cria a pasta (idempotente) e devolve o caminho, para encadear.</summary>
    public static string EnsureDirectory(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }

    private static string ResolveBaseDirectory()
    {
        var appDir = AppContext.BaseDirectory;

        // Teste real de escrita: pasta só leitura (Program Files) não pode ser a base.
        try
        {
            var probe = Path.Combine(appDir, ".cs2opt-teste-" + Guid.NewGuid().ToString("N")[..8] + ".tmp");
            File.WriteAllText(probe, "x");
            File.Delete(probe);
            return appDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch
        {
            // Segue para o fallback abaixo.
        }

        var local = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CS2-Optimizer");
        Directory.CreateDirectory(local);
        return local.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
