using CS2Optimizer.Core.Cli;
using CS2Optimizer.Core.Services;
using System.Diagnostics;

namespace CS2Optimizer.Tests;

/// <summary>
/// Guardas de uso: o que o usuário digita/escolhe errado não pode virar
/// aplicação silenciosa nem "sucesso" falso.
/// </summary>
public sealed class UsageGuardTests
{
    [Fact]
    public void Cli_sem_argumentos_nao_define_modo()
    {
        var options = ConsoleRunner.Parse(Array.Empty<string>());

        Assert.Null(options.Error);
        Assert.False(options.Help);
        Assert.Null(options.Revert);
        Assert.False(options.Auto || options.CleanOnly || options.ReportOnly);
    }

    [Fact]
    public void Cli_report_only_aceita_no_pause()
    {
        var options = ConsoleRunner.Parse(new[] { "--report-only", "--no-pause" });

        Assert.Null(options.Error);
        Assert.True(options.ReportOnly);
        Assert.True(options.NoPause);
    }

    [Fact]
    public void Cli_revert_exige_a_pasta()
    {
        var options = ConsoleRunner.Parse(new[] { "--revert" });

        Assert.NotNull(options.Error);
        Assert.Null(options.Revert);
    }

    [Fact]
    public void Cli_revert_com_pasta()
    {
        var options = ConsoleRunner.Parse(new[] { "--revert", @"C:\CS2Optimizer\Backups\bkp" });

        Assert.Null(options.Error);
        Assert.Equal(@"C:\CS2Optimizer\Backups\bkp", options.Revert);
    }

    [Fact]
    public void Cli_nao_aceita_dois_modos()
    {
        var options = ConsoleRunner.Parse(new[] { "--auto", "--clean-only" });

        Assert.NotNull(options.Error);
    }

    [Fact]
    public void Cli_recusa_opcao_desconhecida()
    {
        // Sem isso, um "--auto " digitado errado cairia no fluxo interativo.
        var options = ConsoleRunner.Parse(new[] { "--aplicar-tudo" });

        Assert.NotNull(options.Error);
        Assert.False(options.Auto);
    }

    [Fact]
    public void Cli_help()
    {
        Assert.True(ConsoleRunner.Parse(new[] { "--help" }).Help);
        Assert.True(ConsoleRunner.Parse(new[] { "-h" }).Help);
    }

    [Fact]
    public async Task Restaurar_pasta_que_nao_e_backup_falha_em_vez_de_dizer_que_restaurou()
    {
        var temp = Path.Combine(Path.GetTempPath(), "cs2opt-teste-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            var log = new AppLogger(Path.Combine(temp, "log.txt"));
            var journal = new RegistryJournal();
            var registry = new RegistryService(journal);
            var backup = new BackupService(registry, new ExternalProcessRunner(), log, temp);

            var error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => backup.RestoreBackupAsync(temp, @"C:\game\cs2.exe"));

            Assert.Contains("não parece um backup", error.Message);
        }
        finally
        {
            Directory.Delete(temp, true);
        }
    }

    [Fact]
    public void Migracao_move_os_backups_antigos_para_a_pasta_do_programa()
    {
        using var sandbox = new Sandbox();
        var legacy = Path.Combine(sandbox.Root, "legacy");
        var app = Path.Combine(sandbox.Root, "app");
        var oldBackup = Path.Combine(legacy, "CS2-Optimizer-Backup-20260101-000000");
        Directory.CreateDirectory(oldBackup);
        File.WriteAllText(Path.Combine(oldBackup, "journal.json"), "[]");
        Directory.CreateDirectory(app);

        var backup = sandbox.Service(app);
        backup.MigrateLegacyBackups(legacy);

        Assert.True(Directory.Exists(Path.Combine(app, "Backups", "CS2-Optimizer-Backup-20260101-000000")));
        Assert.Empty(Directory.GetDirectories(legacy));
    }

    [Fact]
    public void Migracao_nao_mexe_quando_a_pasta_nova_ja_tem_backup()
    {
        using var sandbox = new Sandbox();
        var legacy = Path.Combine(sandbox.Root, "legacy");
        var app = Path.Combine(sandbox.Root, "app");
        var oldBackup = Path.Combine(legacy, "CS2-Optimizer-Backup-20260101-000000");
        Directory.CreateDirectory(oldBackup);
        Directory.CreateDirectory(Path.Combine(app, "Backups", "CS2-Optimizer-Backup-20260202-000000"));

        var backup = sandbox.Service(app);
        backup.MigrateLegacyBackups(legacy);

        Assert.True(Directory.Exists(oldBackup));
        Assert.True(Directory.Exists(Path.Combine(app, "Backups", "CS2-Optimizer-Backup-20260202-000000")));
    }

    [Fact]
    public void Lista_de_backups_ordena_do_mais_novo_para_o_mais_antigo()
    {
        using var sandbox = new Sandbox();
        var app = Path.Combine(sandbox.Root, "app");
        var older = Path.Combine(app, "Backups", "CS2-Optimizer-Backup-20260101-000000");
        var newer = Path.Combine(app, "Backups", "CS2-Optimizer-Backup-20260303-000000");
        Directory.CreateDirectory(older);
        Directory.CreateDirectory(newer);
        Directory.SetLastWriteTime(older, new DateTime(2026, 1, 1, 10, 0, 0));
        Directory.SetLastWriteTime(newer, new DateTime(2026, 3, 3, 10, 0, 0));

        var backup = sandbox.Service(app);
        var list = backup.FindBackups();

        Assert.True(list.Count >= 2);
        Assert.Equal("CS2-Optimizer-Backup-20260303-000000", list[0].Name);
        Assert.Equal(Path.Combine(app, "Backups"), backup.BackupRoot);
    }

    [Fact]
    public void Backup_ilegivel_nao_esconde_os_outros()
    {
        using var sandbox = new Sandbox();
        var app = Path.Combine(sandbox.Root, "app");
        var good = Path.Combine(app, "Backups", "CS2-Optimizer-Backup-20260101-000000");
        Directory.CreateDirectory(good);
        File.WriteAllText(Path.Combine(good, "journal.json"), "[]");

        // Junção para uma pasta que não existe: GetFiles nela lança e, antes da
        // correção, isso abortava a listagem inteira.
        var broken = Path.Combine(app, "Backups", "CS2-Optimizer-Backup-20250101-000000");
        var psi = new ProcessStartInfo("cmd.exe",
            $"/c mklink /J \"{broken}\" \"{Path.Combine(sandbox.Root, "sumiu")}\"")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        try
        {
            using var process = Process.Start(psi);
            process?.WaitForExit(5000);
        }
        catch
        {
            // Sem mklink o teste ainda vale para o caso normal.
        }

        var backup = sandbox.Service(app);
        var list = backup.FindBackups();

        Assert.Contains(list, b => b.Name == "CS2-Optimizer-Backup-20260101-000000");
    }

    private sealed class Sandbox : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "cs2opt-teste-" + Guid.NewGuid().ToString("N"));

        public Sandbox() => Directory.CreateDirectory(Root);

        public BackupService Service(string baseDirectory)
        {
            Directory.CreateDirectory(baseDirectory);
            var log = new AppLogger(Path.Combine(baseDirectory, "log.txt"));
            return new BackupService(new RegistryService(new RegistryJournal()),
                new ExternalProcessRunner(), log, baseDirectory);
        }

        public void Dispose()
        {
            try { Directory.Delete(Root, true); } catch { }
        }
    }
}
