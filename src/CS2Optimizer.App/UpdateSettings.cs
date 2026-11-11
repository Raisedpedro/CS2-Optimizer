using System;
using System.IO;
using System.Text.Json;

namespace CS2Optimizer.App;

/// <summary>
/// Preferências do usuário, guardadas em <c>config.json</c> ao lado do programa
/// (para o app continuar portátil). Qualquer problema ao ler/gravar não pode
/// impedir o programa de abrir: volta para o padrão.
/// </summary>
internal sealed class UpdateSettings
{
    /// <summary>Verificar atualização sozinho ao abrir (no máximo 1x por dia).</summary>
    public bool CheckOnStart { get; set; } = true;

    /// <summary>Quando foi a última verificação automática.</summary>
    public DateTimeOffset? LastCheck { get; set; }

    /// <summary>Versão que o usuário mandou não avisar de novo (só aquela tag).</summary>
    public string? DismissedVersion { get; set; }

    /// <summary>
    /// Ajustes que o próprio Windows protege: a gravação falha mesmo como administrador.
    /// Ficam na lista do O&amp;O marcados como "bloqueado" e não contam como pendência.
    /// </summary>
    public List<string> BlockedTweaks { get; set; } = new();

    public bool IsBlocked(string id) =>
        BlockedTweaks.Any(x => string.Equals(x, id, StringComparison.OrdinalIgnoreCase));

    public void SetBlocked(string id, bool blocked)
    {
        var antes = IsBlocked(id);
        if (blocked == antes) return;

        if (blocked) BlockedTweaks.Add(id);
        else BlockedTweaks.RemoveAll(x => string.Equals(x, id, StringComparison.OrdinalIgnoreCase));

        Save();
    }

    private static string FilePath => AppPaths.SettingsFile;

    public static UpdateSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new UpdateSettings();

            var lido = JsonSerializer.Deserialize<UpdateSettings>(File.ReadAllText(FilePath));
            return lido ?? new UpdateSettings();
        }
        catch
        {
            return new UpdateSettings();
        }
    }

    public void Save()
    {
        try
        {
            File.WriteAllText(FilePath,
                JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // Não conseguir gravar preferência não é motivo para quebrar nada.
        }
    }
}
