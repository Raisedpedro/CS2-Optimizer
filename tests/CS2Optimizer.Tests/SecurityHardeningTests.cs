using CS2Optimizer.Core.Fixes;
using CS2Optimizer.Core.Mitigations;
using CS2Optimizer.Core.Models;
using CS2Optimizer.Core.Oosu10;
using CS2Optimizer.Core.Services;

namespace CS2Optimizer.Tests;

public sealed class SecurityHardeningTests
{
    [Fact]
    public void Windows_tools_resolve_to_absolute_system_paths()
    {
        foreach (var tool in new[]
                 {
                     "reg.exe", "bcdedit.exe", "powercfg.exe", "powershell.exe",
                     "schtasks.exe", "sc.exe", "netsh.exe", "ipconfig.exe"
                 })
        {
            var resolved = ExternalProcessRunner.ResolveSystemTool(tool);
            Assert.True(Path.IsPathRooted(resolved), $"{tool} resolveu para {resolved}");
            Assert.True(File.Exists(resolved), $"Executável não encontrado: {resolved}");
            Assert.StartsWith(Environment.SystemDirectory, resolved, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Catalogs_do_not_write_the_same_registry_values()
    {
        var overlap = RegistryWriteAllowlist.FixCatalogPairs
            .Intersect(RegistryWriteAllowlist.Oosu10Pairs, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.True(overlap.Length == 0, "Sobreposição: " + string.Join(" | ", overlap));
    }

    [Fact]
    public void Fix_catalog_ids_are_unique_and_telemetry_is_not_silently_recommended()
    {
        var fixes = FixCatalog.Build(new FixContext());
        var ids = fixes.Select(f => f.Id).ToList();

        Assert.Equal(ids.Count, ids.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.DoesNotContain(fixes, f => f.Id == "telemetry-servico");
        Assert.DoesNotContain(fixes.Where(f => f.Recommended),
            f => f.Id.StartsWith("telemetry-", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Vbs_values_are_allowed_for_backup_restore()
    {
        Assert.True(RegistryWriteAllowlist.IsAllowed(MitigationService.DeviceGuardKey,
            MitigationService.EnableVbsValue));
        Assert.True(RegistryWriteAllowlist.IsAllowed(MitigationService.DeviceGuardKey,
            MitigationService.RequirePlatformSecurityFeaturesValue));
        Assert.True(RegistryWriteAllowlist.IsAllowed(MitigationService.HvciKey,
            MitigationService.EnabledValue));
        Assert.True(RegistryWriteAllowlist.IsAllowed(MitigationService.KernelShadowStacksKey,
            MitigationService.EnabledValue));
        Assert.True(RegistryWriteAllowlist.IsAllowed(MitigationService.CredentialGuardKey,
            MitigationService.EnabledValue));
        Assert.True(RegistryWriteAllowlist.IsAllowed(MitigationService.LsaKey,
            MitigationService.LsaCfgFlagsValue));
    }

    [Fact]
    public void Restore_only_accepts_backup_folders_in_known_roots()
    {
        var root = Path.Combine(Path.GetTempPath(), "cs2opt-roots-" + Guid.NewGuid().ToString("N"));
        var backupRoot = Path.Combine(root, "Backups");
        var good = Path.Combine(backupRoot, "CS2-Optimizer-Backup-20260101-000000");
        var bad = Path.Combine(root, "Documentos", "CS2-Optimizer-Backup-20260101-000000");
        Directory.CreateDirectory(good);
        Directory.CreateDirectory(bad);

        try
        {
            var log = new AppLogger(Path.Combine(root, "log.txt"));
            var backup = new BackupService(new RegistryService(new RegistryJournal()),
                new ExternalProcessRunner(), log, root);

            Assert.True(backup.IsKnownBackupDirectory(good));
            Assert.False(backup.IsKnownBackupDirectory(bad));
            Assert.False(backup.IsKnownBackupDirectory(Path.Combine(root, "Backups")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
