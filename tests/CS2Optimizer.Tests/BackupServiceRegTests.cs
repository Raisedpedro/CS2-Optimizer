using System.Text;
using CS2Optimizer.Core.Services;

namespace CS2Optimizer.Tests;

public sealed class BackupServiceRegTests
{
    [Fact]
    public void Accepts_exported_registry_keys()
    {
        var file = WriteReg(
            "Windows Registry Editor Version 5.00\r\n\r\n" +
            "[HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows\\Dwm]\r\n" +
            "\"OverlayTestMode\"=dword:00000005\r\n");

        try
        {
            Assert.True(BackupService.TestRegSafe(file));
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void Accepts_only_values_known_to_the_program()
    {
        var file = WriteReg(
            "Windows Registry Editor Version 5.00\r\n\r\n" +
            "[HKEY_CURRENT_USER\\Control Panel\\Mouse]\r\n" +
            "\"MouseSpeed\"=\"0\"\r\n\r\n" +
            "[HKEY_LOCAL_MACHINE\\SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection]\r\n" +
            "\"LimitDumpCollection\"=dword:00000001\r\n");

        try
        {
            Assert.True(BackupService.TestRegSafe(file));
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void Drops_unknown_values_and_keeps_only_program_values()
    {
        var file = WriteReg(
            "Windows Registry Editor Version 5.00\r\n\r\n" +
            "[HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows\\Dwm]\r\n" +
            "\"OverlayTestMode\"=dword:00000005\r\n" +
            "\"X\"=dword:00000001\r\n");
        var clean = file + ".clean";

        try
        {
            Assert.True(RegFileSanitizer.TrySanitize(file, clean, out var kept));
            Assert.Equal(1, kept);
            var text = File.ReadAllText(clean, Encoding.Unicode);
            Assert.Contains("OverlayTestMode", text);
            Assert.DoesNotContain("\"X\"", text);
        }
        finally
        {
            File.Delete(file);
            if (File.Exists(clean)) File.Delete(clean);
        }
    }

    [Fact]
    public void Accepts_vbs_values_in_exported_keys()
    {
        var file = WriteReg(
            "Windows Registry Editor Version 5.00\r\n\r\n" +
            "[HKEY_LOCAL_MACHINE\\SYSTEM\\CurrentControlSet\\Control\\DeviceGuard]\r\n" +
            "\"EnableVirtualizationBasedSecurity\"=dword:00000001\r\n" +
            "\"RequirePlatformSecurityFeatures\"=dword:00000001\r\n\r\n" +
            "[HKEY_LOCAL_MACHINE\\SYSTEM\\CurrentControlSet\\Control\\DeviceGuard\\Scenarios\\HypervisorEnforcedCodeIntegrity]\r\n" +
            "\"Enabled\"=dword:00000001\r\n\r\n" +
            "[HKEY_LOCAL_MACHINE\\SYSTEM\\CurrentControlSet\\Control\\Lsa]\r\n" +
            "\"LsaCfgFlags\"=dword:00000001\r\n");

        try
        {
            Assert.True(BackupService.TestRegSafe(file));
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void Rejects_dangerous_value_name_in_exported_key()
    {
        var file = WriteReg(
            "Windows Registry Editor Version 5.00\r\n\r\n" +
            "[HKEY_LOCAL_MACHINE\\SYSTEM\\CurrentControlSet\\Services\\DiagTrack]\r\n" +
            "\"ImagePath\"=\"cmd.exe /c malicioso\"\r\n");

        try
        {
            Assert.False(BackupService.TestRegSafe(file));
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void Rejects_value_deletion()
    {
        var file = WriteReg(
            "Windows Registry Editor Version 5.00\r\n\r\n" +
            "[HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows\\Dwm]\r\n" +
            "\"OverlayTestMode\"=-\r\n");

        try
        {
            Assert.False(BackupService.TestRegSafe(file));
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void Rejects_wrong_header()
    {
        var file = WriteReg(
            "Windows Registry Editor Version 4.00\r\n\r\n" +
            "[HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows\\Dwm]\r\n");

        try
        {
            Assert.False(BackupService.TestRegSafe(file));
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void Rejects_key_deletion()
    {
        var file = WriteReg(
            "Windows Registry Editor Version 5.00\r\n\r\n" +
            "[-HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows\\Dwm]\r\n");

        try
        {
            Assert.False(BackupService.TestRegSafe(file));
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void Rejects_keys_outside_the_export_whitelist()
    {
        var file = WriteReg(
            "Windows Registry Editor Version 5.00\r\n\r\n" +
            "[HKEY_CURRENT_USER\\Software\\NotExportedByTheProgram]\r\n" +
            "\"X\"=dword:00000001\r\n");

        try
        {
            Assert.False(BackupService.TestRegSafe(file));
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void Rejects_mixed_allowed_and_disallowed_keys()
    {
        var file = WriteReg(
            "Windows Registry Editor Version 5.00\r\n\r\n" +
            "[HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows\\Dwm]\r\n" +
            "\"X\"=dword:00000001\r\n\r\n" +
            "[HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run]\r\n" +
            "\"Evil\"=\"calc.exe\"\r\n");

        try
        {
            Assert.False(BackupService.TestRegSafe(file));
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void Rejects_empty_file()
    {
        var file = WriteReg("");
        try
        {
            Assert.False(BackupService.TestRegSafe(file));
        }
        finally
        {
            File.Delete(file);
        }
    }

    private static string WriteReg(string content)
    {
        var file = Path.Combine(Path.GetTempPath(), "cs2opt-" + Guid.NewGuid().ToString("N") + ".reg");
        File.WriteAllText(file, content, new UTF8Encoding(true));
        return file;
    }
}
