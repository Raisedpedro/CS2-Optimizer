using System.Text;
using Microsoft.Win32;
using CS2Optimizer.Core.Services;

namespace CS2Optimizer.Tests;

public sealed class RegistryJournalTests
{
    [Fact]
    public void SaveTo_uses_prototype_property_names_and_clears_pending_entries()
    {
        var dir = NewTempDirectory();
        try
        {
            var journal = new RegistryJournal();
            journal.AddSnapshot(@"HKLM\SOFTWARE\CS2OptimizerTest", "A",
                new RegistryValueSnapshot(true, RegistryValueKind.DWord, 10));
            journal.AddSnapshot(@"HKCU\Software\CS2OptimizerTest", "B",
                new RegistryValueSnapshot(false, null, null));
            journal.AddSnapshot(@"HKLM\SOFTWARE\CS2OptimizerTest", "C",
                new RegistryValueSnapshot(true, RegistryValueKind.String, "abc"));

            Assert.True(journal.SaveTo(dir));
            Assert.Equal(0, journal.Count);

            var file = Path.Combine(dir, "journal.json");
            var json = File.ReadAllText(file, Encoding.UTF8);

            Assert.Contains("\"Existia\": true", json);
            Assert.Contains("\"Tipo\": \"DWord\"", json);
            Assert.Contains("\"Valor\": 10", json);
            Assert.DoesNotContain("\"Existed\"", json);
            Assert.DoesNotContain("\"Kind\"", json);

            var entries = RegistryJournal.LoadFile(file);
            Assert.Equal(3, entries.Count);
            Assert.Equal("A", entries[0].Name);
            Assert.True(entries[0].Existed);
            Assert.Equal("DWord", entries[0].Kind);
            Assert.Equal("abc", entries[2].Value?.ToString());
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void RestoreFrom_applies_newest_entry_first_and_deletes_created_values()
    {
        var dir = NewTempDirectory();
        try
        {
            var journal = new RegistryJournal();
            journal.AddSnapshot(@"HKLM\SOFTWARE\CS2OptimizerTest", "A",
                new RegistryValueSnapshot(true, RegistryValueKind.DWord, 10));
            journal.AddSnapshot(@"HKCU\Software\CS2OptimizerTest", "B",
                new RegistryValueSnapshot(false, null, null));
            journal.AddSnapshot(@"HKLM\SOFTWARE\CS2OptimizerTest", "C",
                new RegistryValueSnapshot(true, RegistryValueKind.String, "abc"));
            journal.SaveTo(dir);

            var registry = new RecordingRegistry();
            var restored = RegistryJournal.RestoreFrom(dir, registry);

            Assert.Equal(3, restored);
            Assert.Equal(3, registry.Operations.Count);
            Assert.StartsWith("SET HKLM\\SOFTWARE\\CS2OptimizerTest\\C", registry.Operations[0]);
            Assert.StartsWith("DEL HKCU\\Software\\CS2OptimizerTest\\B", registry.Operations[1]);
            Assert.StartsWith("SET HKLM\\SOFTWARE\\CS2OptimizerTest\\A", registry.Operations[2]);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void AddSnapshot_ignores_invalid_registry_paths()
    {
        var journal = new RegistryJournal();
        journal.AddSnapshot("C:\\Windows\\System32", "x",
            new RegistryValueSnapshot(true, RegistryValueKind.String, "x"));

        Assert.Equal(0, journal.Count);
    }

    private static string NewTempDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cs2opt-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private sealed class RecordingRegistry : IRegistryAccess
    {
        public List<string> Operations { get; } = new();

        public RegistryValueSnapshot GetValue(string path, string name) =>
            new(false, null, null);

        public void SetValueRaw(string path, string name, object value, RegistryValueKind kind) =>
            Operations.Add($"SET {path}\\{name}={value}");

        public void DeleteValueRaw(string path, string name) =>
            Operations.Add($"DEL {path}\\{name}");

        public bool KeyExists(string path) => true;
    }
}
