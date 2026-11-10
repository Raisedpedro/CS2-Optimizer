using System.Text;
using System.Text.RegularExpressions;
using CS2Optimizer.Core.Models;

namespace CS2Optimizer.Core.Services;

public static class RegistryExports
{
    /// <summary>Chaves exportadas no backup (também é a lista do que o Desfazer aceita importar).</summary>
    public static readonly IReadOnlyList<KeyValuePair<string, string>> Map = new List<KeyValuePair<string, string>>
    {
        new("dwm.reg", @"HKLM\SOFTWARE\Microsoft\Windows\Dwm"),
        new("power.reg", @"HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Power"),
        new("graphics.reg", @"HKLM\SYSTEM\CurrentControlSet\Control\GraphicsDrivers"),
        new("gamedvr.reg", @"HKCU\Software\Microsoft\Windows\CurrentVersion\GameDVR"),
        new("gameconfig.reg", @"HKCU\System\GameConfigStore"),
        new("gamebar.reg", @"HKCU\Software\Microsoft\GameBar"),
        new("mouse.reg", @"HKCU\Control Panel\Mouse"),
        new("gpupref.reg", @"HKCU\Software\Microsoft\DirectX\UserGpuPreferences"),
        new("compat.reg", @"HKCU\Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers"),
        new("bgapps-user.reg", @"HKCU\Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications"),
        new("bgapps-policy.reg", @"HKLM\SOFTWARE\Policies\Microsoft\Windows\AppPrivacy"),
        new("mmcss.reg", @"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile"),
        new("telemetry.reg", @"HKLM\SOFTWARE\Policies\Microsoft\Windows\DataCollection"),
        new("telemetry-mu.reg", @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection"),
        new("sqm.reg", @"HKLM\SOFTWARE\Policies\Microsoft\SQMClient"),
        new("diagtrack.reg", @"HKLM\SYSTEM\CurrentControlSet\Services\DiagTrack"),
        new("wappush.reg", @"HKLM\SYSTEM\CurrentControlSet\Services\dmwappushservice"),
        new("adinfo.reg", @"HKCU\Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo"),
        new("privacy.reg", @"HKCU\Software\Microsoft\Windows\CurrentVersion\Privacy"),
        new("siuf.reg", @"HKCU\Software\Microsoft\Siuf"),
        new("activity.reg", @"HKLM\SOFTWARE\Policies\Microsoft\Windows\System"),
        new("tipc.reg", @"HKCU\Software\Microsoft\Input\TIPC"),
        new("inputpers.reg", @"HKCU\Software\Microsoft\InputPersonalization"),
        new("inputpol.reg", @"HKLM\SOFTWARE\Policies\Microsoft\InputPersonalization"),
        new("speech.reg", @"HKCU\Software\Microsoft\Speech_OneCore"),
        new("wer.reg", @"HKLM\SOFTWARE\Microsoft\Windows\Windows Error Reporting")
    };

    public static string ToLongName(string path) => path
        .Replace(@"HKLM\", @"HKEY_LOCAL_MACHINE\", StringComparison.OrdinalIgnoreCase)
        .Replace(@"HKCU\", @"HKEY_CURRENT_USER\", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Backup em pasta protegida, exportação/importação de .reg e desfazer (seção 5.3).</summary>
public sealed class BackupService
{
    private readonly RegistryService _registry;
    private readonly ExternalProcessRunner _runner;
    private readonly AppLogger _log;

    public BackupService(RegistryService registry, ExternalProcessRunner runner, AppLogger log,
        string? baseDirectory = null)
    {
        _registry = registry;
        _runner = runner;
        _log = log;
        BaseDirectory = baseDirectory
            ?? AppContext.BaseDirectory;
        Desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if (string.IsNullOrWhiteSpace(Desktop))
            Desktop = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        BackupRoot = Path.Combine(BaseDirectory, "Backups");
    }

    public string BackupRoot { get; }
    public string BaseDirectory { get; }
    public string Desktop { get; }
    public string? CurrentBackupDirectory { get; private set; }
    public bool BackupMade { get; private set; }

    public static string NewStamp() => DateTime.Now.ToString("yyyyMMdd-HHmmss");

    public string GetBackupDirectory(string stamp) =>
        Path.Combine(BackupRoot, $"CS2-Optimizer-Backup-{stamp}");

    /// <summary>
    /// Depois de um desfazer, a próxima aplicação deve criar um backup novo em vez
    /// de reaproveitar a pasta da sessão anterior.
    /// </summary>
    public void ResetSession()
    {
        BackupMade = false;
        CurrentBackupDirectory = null;
    }

    public async Task<bool> CreateBackupAsync(string stamp, bool createRestorePoint,
        CancellationToken cancellationToken = default)
    {
        if (BackupMade) return true;

        var dir = GetBackupDirectory(stamp);
        CurrentBackupDirectory = dir;
        _log.Title("Criando backup em: " + dir);

        await ProtectBackupRootAsync(cancellationToken);
        Directory.CreateDirectory(dir);

        try
        {
            var bcd = await _runner.RunAsync("bcdedit.exe",
                new[] { "/export", Path.Combine(dir, "bcd.bak") },
                "Exportando BCD", cancellationToken: cancellationToken);
            if (bcd.Started && bcd.ExitCode == 0)
                _log.Ok("BCD exportado.");
            else
                _log.Warn("Não consegui exportar o BCD.");
        }
        catch
        {
            _log.Warn("Não consegui exportar o BCD.");
        }

        foreach (var export in RegistryExports.Map)
        {
            var target = Path.Combine(dir, export.Key);
            try
            {
                var result = await _runner.RunAsync("reg.exe",
                    new[] { "export", export.Value, target, "/y" },
                    "Exportando registro", cancellationToken: cancellationToken);
                if (!result.Started || result.ExitCode != 0)
                    _log.Warn($"Não consegui exportar {export.Value}.");
            }
            catch (Exception ex)
            {
                _log.Warn($"Não consegui exportar {export.Value}: {ex.Message}");
            }
        }
        _log.Ok("Chaves de registro exportadas.");

        try
        {
            var power = await _runner.RunAsync("powercfg.exe", new[] { "/getactivescheme" },
                "Lendo plano de energia", cancellationToken: cancellationToken);
            var text = (power.StandardOutput ?? "").Trim();
            var guid = Regex.Match(text,
                "[0-9a-fA-F]{8}-([0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}").Value;
            File.WriteAllText(Path.Combine(dir, "powerscheme.txt"),
                text + Environment.NewLine + guid, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            _log.Warn("Não consegui salvar o plano de energia: " + ex.Message);
        }

        if (createRestorePoint)
        {
            var systemDrive = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            var driveRoot = Path.GetPathRoot(systemDrive) ?? "C:\\";
            var description = $"CS2-Optimizer {stamp}";
            var script =
                $"Enable-ComputerRestore -Drive '{driveRoot.Replace("'", "''")}' -ErrorAction SilentlyContinue; " +
                $"Checkpoint-Computer -Description '{description}' -RestorePointType MODIFY_SETTINGS -ErrorAction Stop";
            try
            {
                var result = await _runner.RunAsync("powershell.exe",
                    new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", script },
                    "Criando ponto de restauração", cancellationToken: cancellationToken);
                if (result.Started && result.ExitCode == 0)
                    _log.Ok("Ponto de restauração criado.");
                else
                    _log.Warn("Ponto de restauração não criado (limite de 1 por 24h ou proteção do sistema desativada).");
            }
            catch
            {
                _log.Warn("Ponto de restauração não criado (limite de 1 por 24h ou proteção do sistema desativada).");
            }
        }
        else
        {
            _log.Warn("Ponto de restauração: não criado (você escolheu não criar).");
        }

        BackupMade = true;
        return true;
    }

    /// <summary>Pasta antiga (versões que gravavam em ProgramData), só para migrar uma vez.</summary>
    public static string LegacyBackupRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "CS2-Optimizer", "Backups");

    /// <summary>
    /// Traz os backups da versão antiga (%ProgramData%) para a pasta do programa,
    /// para o usuário não perder o histórico nem precisar procurar em outro caminho.
    /// Roda no máximo uma vez: se já existe backup na pasta nova, não mexe em nada.
    /// </summary>
    /// <param name="legacyRoot">Só para teste; por padrão usa o caminho antigo real.</param>
    public void MigrateLegacyBackups(string? legacyRoot = null)
    {
        try
        {
            var legacy = string.IsNullOrWhiteSpace(legacyRoot) ? LegacyBackupRoot : legacyRoot;
            if (!Directory.Exists(legacy)) return;
            if (string.Equals(
                    Path.GetFullPath(legacy).TrimEnd(Path.DirectorySeparatorChar),
                    Path.GetFullPath(BackupRoot).TrimEnd(Path.DirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase))
                return;

            Directory.CreateDirectory(BackupRoot);
            if (Directory.EnumerateDirectories(BackupRoot).Any()) return;

            var moved = 0;
            foreach (var dir in Directory.EnumerateDirectories(legacy, "CS2-Optimizer-Backup-*"))
            {
                var destination = Path.Combine(BackupRoot, Path.GetFileName(dir));
                if (Directory.Exists(destination)) continue;

                Directory.Move(dir, destination);
                moved++;
            }

            if (moved > 0)
                _log.Ok($"{moved} backup(s) antigos movidos de {legacy} para {BackupRoot}.");
        }
        catch (Exception ex)
        {
            // Sem permissão ou pasta em uso: segue funcionando, só não migra.
            _log.Warn("Não consegui trazer os backups antigos: " + ex.Message);
        }
    }

    /// <summary>
    /// Garante que a pasta de backup existe. Não mexe em ACL: a pasta fica dentro
    /// da pasta do programa (modo portátil), e restringir a herança ali deixaria
    /// o Explorer (que roda sem elevação) sem acesso — o usuário não conseguiria
    /// nem abrir a pasta para restaurar um backup.
    /// </summary>
    public Task ProtectBackupRootAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            Directory.CreateDirectory(BackupRoot);
        }
        catch (Exception ex)
        {
            _log.Warn("Não consegui criar a pasta de backup: " + ex.Message);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Um .reg só é importado se tiver o cabeçalho certo, nenhuma exclusão de chave
    /// e SOMENTE chaves que o próprio programa exporta.
    /// </summary>
    public static bool TestRegSafe(string file)
    {
        try
        {
            var lines = File.ReadAllLines(file);
            var prefixes = RegistryExports.Map
                .Select(kv => RegistryExports.ToLongName(kv.Value))
                .ToArray();

            var first = lines.FirstOrDefault(l => !string.IsNullOrWhiteSpace(l))?.Trim();
            if (!string.Equals(first, "Windows Registry Editor Version 5.00", StringComparison.Ordinal))
                return false;

            foreach (var line in lines)
            {
                var t = line.Trim();
                if (t.StartsWith("[-", StringComparison.Ordinal)) return false;
                if (!t.StartsWith("[", StringComparison.Ordinal)) continue;

                var key = t.Trim('[', ']');
                var ok = prefixes.Any(p =>
                    string.Equals(key, p, StringComparison.OrdinalIgnoreCase) ||
                    key.StartsWith(p + "\\", StringComparison.OrdinalIgnoreCase));
                if (!ok) return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task RestoreBackupAsync(string directory, string? cs2Exe,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(directory))
            throw new DirectoryNotFoundException($"Pasta de backup não encontrada: {directory}");

        // Sem isso, escolher a pasta errada (Documentos, por exemplo) terminava com
        // "backup restaurado com sucesso" sem ter restaurado nada.
        var hasJournal = File.Exists(Path.Combine(directory, "journal.json"));
        var hasReg = Directory.EnumerateFiles(directory, "*.reg").Any();
        var hasBcd = File.Exists(Path.Combine(directory, "bcd.bak"));
        var hasPower = File.Exists(Path.Combine(directory, "powerscheme.txt"));
        if (!hasJournal && !hasReg && !hasBcd && !hasPower)
        {
            throw new InvalidOperationException(
                "Esta pasta não parece um backup do CS2 / Windows Optimizer.\n" +
                "Um backup tem journal.json, algum .reg, bcd.bak ou powerscheme.txt.\n\n" + directory);
        }

        _log.Title("Restaurando de: " + directory);

        foreach (var regFile in Directory.GetFiles(directory, "*.reg").OrderBy(f => f))
        {
            var name = Path.GetFileName(regFile);
            if (hasJournal && (name.Equals("power.reg", StringComparison.OrdinalIgnoreCase) ||
                               name.Equals("graphics.reg", StringComparison.OrdinalIgnoreCase)))
            {
                _log.Log("  ignorado (coberto pelo journal): " + name);
                continue;
            }

            if (!TestRegSafe(regFile))
            {
                _log.Warn("  RECUSADO (conteúdo fora do esperado): " + name);
                continue;
            }

            var result = await _runner.RunAsync("reg.exe", new[] { "import", regFile },
                "Importando " + name, cancellationToken: cancellationToken);
            if (result.Started && result.ExitCode == 0)
                _log.Log("  importado: " + name);
            else
                _log.Warn("  falhou ao importar: " + name);
        }

        RegistryJournal.RestoreFrom(directory, _registry, _log.Log);

        var bcd = Path.Combine(directory, "bcd.bak");
        if (File.Exists(bcd))
        {
            var result = await _runner.RunAsync("bcdedit.exe", new[] { "/import", bcd },
                "Restaurando BCD", cancellationToken: cancellationToken);
            if (result.Started && result.ExitCode == 0)
                _log.Ok("BCD restaurado.");
            else
                _log.Warn("Não consegui restaurar o BCD.");
        }

        var powerFile = Path.Combine(directory, "powerscheme.txt");
        if (File.Exists(powerFile))
        {
            var text = File.ReadAllText(powerFile);
            var guid = Regex.Match(text,
                "[0-9a-fA-F]{8}-([0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}").Value;
            if (!string.IsNullOrWhiteSpace(guid))
            {
                var result = await _runner.RunAsync("powercfg.exe", new[] { "/setactive", guid },
                    "Reativando plano de energia", cancellationToken: cancellationToken);
                if (result.Started && result.ExitCode == 0)
                    _log.Ok("Plano de energia original reativado.");
                else
                    _log.Warn("Plano original não existe mais neste PC.");
            }
        }

        // Preferência de GPU do cs2.exe: só remove se o backup não tinha esse valor.
        if (!string.IsNullOrWhiteSpace(cs2Exe))
        {
            var gpuBackup = Path.Combine(directory, "gpupref.reg");
            var hadValue = false;
            if (File.Exists(gpuBackup))
            {
                var txt = File.ReadAllText(gpuBackup);
                hadValue = txt.Contains(cs2Exe.Replace(@"\", @"\\"), StringComparison.OrdinalIgnoreCase);
            }

            if (!hadValue)
            {
                _registry.DeleteValueRaw(
                    @"HKCU\Software\Microsoft\DirectX\UserGpuPreferences", cs2Exe);
                _log.Ok("Preferência de GPU do cs2.exe removida.");
            }
        }

        _log.Ok("Backup restaurado. Reinicie o PC.");
    }

    public IReadOnlyList<BackupInfo> FindBackups()
    {
        var found = new Dictionary<string, BackupInfo>(StringComparer.OrdinalIgnoreCase);
        var unreadable = 0;

        foreach (var dir in new[] { BackupRoot, BaseDirectory, Desktop })
        {
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) continue;

            string[] paths;
            try
            {
                paths = Directory.GetDirectories(dir, "CS2-Optimizer-Backup-*");
            }
            catch
            {
                continue;
            }

            foreach (var path in paths)
            {
                // Pasta protegida/sem acesso (backup antigo com ACL) não pode
                // esconder as outras: cada uma falha sozinha.
                try
                {
                    var full = Path.GetFullPath(path);
                    if (found.ContainsKey(full)) continue;

                    var files = Directory.GetFiles(full, "*", SearchOption.TopDirectoryOnly);
                    long size = 0;
                    foreach (var f in files)
                    {
                        try { size += new FileInfo(f).Length; } catch { }
                    }

                    found[full] = new BackupInfo(full, Path.GetFileName(full),
                        Directory.GetLastWriteTime(full), files.Length, size);
                }
                catch
                {
                    unreadable++;
                }
            }
        }

        if (unreadable > 0)
            _log.Warn($"{unreadable} pasta(s) de backup sem permissão de leitura foram ignoradas.");

        return found.Values.OrderByDescending(b => b.LastWrite).ToArray();
    }
}
