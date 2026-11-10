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
    public void Accepts_hkcu_exported_key_and_subkey()
    {
        var file = WriteReg(
            "Windows Registry Editor Version 5.00\r\n\r\n" +
            "[HKEY_CURRENT_USER\\Control Panel\\Mouse]\r\n" +
            "\"MouseSpeed\"=\"0\"\r\n\r\n" +
            "[HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows\\Dwm\\SubKey]\r\n" +
            "\"X\"=dword:00000001\r\n");

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
