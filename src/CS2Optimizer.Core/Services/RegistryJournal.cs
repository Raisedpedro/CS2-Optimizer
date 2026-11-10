using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;
using CS2Optimizer.Core.Models;

namespace CS2Optimizer.Core.Services;

public sealed class JournalEntry
{
    public string Path { get; set; } = "";
    public string Name { get; set; } = "";

    [JsonPropertyName("Existia")]
    public bool Existed { get; set; }

    [JsonPropertyName("Tipo")]
    public string? Kind { get; set; }

    [JsonPropertyName("Valor")]
    public object? Value { get; set; }
}

/// <summary>
/// Guarda o valor ANTERIOR de cada item de registro alterado. No desfazer,
/// percorre do mais novo ao mais antigo: regrava o valor antigo ou apaga o que
/// o programa criou (seção 5.2).
/// </summary>
public sealed class RegistryJournal
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly List<JournalEntry> _entries = new();

    public int Count => _entries.Count;

    public IReadOnlyList<JournalEntry> Entries => _entries;

    public void AddSnapshot(string path, string name, RegistryValueSnapshot snapshot)
    {
        string normalized;
        try
        {
            normalized = RegistryService.ParsePath(path).Normalized;
        }
        catch
        {
            return;
        }

        var kindName = snapshot.Kind is { } k ? RegistryService.KindToJournalName(k) : null;
        object? value = snapshot.Value;
        if (snapshot.Kind is { } kind && !RegistryService.IsSupportedKind(kind))
        {
            // O desfazer ignora tipos não suportados; guardamos o marcador para não
            // fingir que o valor não existia.
            value = null;
        }

        _entries.Add(new JournalEntry
        {
            Path = normalized,
            Name = name,
            Existed = snapshot.Exists,
            Kind = kindName,
            Value = value
        });
    }

    public void Clear() => _entries.Clear();

    /// <summary>Acrescenta as entradas pendentes ao journal.json da pasta de backup.</summary>
    public bool SaveTo(string backupDirectory, Action<string, LogLevel>? log = null)
    {
        if (_entries.Count == 0) return false;

        try
        {
            Directory.CreateDirectory(backupDirectory);
            var file = System.IO.Path.Combine(backupDirectory, "journal.json");
            var all = LoadFile(file);
            all.AddRange(_entries);
            File.WriteAllText(file, JsonSerializer.Serialize(all, JsonOptions), System.Text.Encoding.UTF8);
            _entries.Clear();
            return true;
        }
        catch (Exception ex)
        {
            log?.Invoke("Não consegui gravar o journal de desfazer: " + ex.Message, LogLevel.Warn);
            return false;
        }
    }

    public static List<JournalEntry> LoadFile(string file)
    {
        try
        {
            if (!File.Exists(file)) return new List<JournalEntry>();
            var json = File.ReadAllText(file, System.Text.Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(json)) return new List<JournalEntry>();
            return JsonSerializer.Deserialize<List<JournalEntry>>(json, JsonOptions) ?? new List<JournalEntry>();
        }
        catch
        {
            return new List<JournalEntry>();
        }
    }

    /// <summary>Desfaz o journal de uma pasta de backup. Devolve quantos valores restaurou.</summary>
    public static int RestoreFrom(string backupDirectory, IRegistryAccess registry, Action<string, LogLevel>? log = null)
    {
        var file = System.IO.Path.Combine(backupDirectory, "journal.json");
        if (!File.Exists(file)) return 0;

        var entries = LoadFile(file);
        entries.Reverse(); // do mais novo para o mais antigo
        var restored = 0;

        foreach (var entry in entries)
        {
            try
            {
                var parsed = RegistryService.ParsePath(entry.Path);

                if (entry.Existed)
                {
                    var kind = RegistryService.JournalNameToKind(entry.Kind);
                    if (kind is null) continue;
                    var value = ConvertValue(entry, kind.Value);
                    if (value is null) continue;
                    registry.SetValueRaw(parsed.Normalized, entry.Name, value, kind.Value);
                }
                else
                {
                    registry.DeleteValueRaw(parsed.Normalized, entry.Name);
                }

                restored++;
            }
            catch (Exception ex)
            {
                log?.Invoke($"  falhou ao restaurar {entry.Path} : {entry.Name} ({ex.Message})", LogLevel.Warn);
            }
        }

        log?.Invoke($"  journal: {restored} valor(es) de registro devolvido(s) ao estado anterior " +
                    "(valores criados pelo programa foram apagados).", LogLevel.Ok);
        return restored;
    }

    private static object? ConvertValue(JournalEntry entry, RegistryValueKind kind)
    {
        var raw = entry.Value;
        if (raw is JsonElement je)
        {
            raw = je.ValueKind switch
            {
                JsonValueKind.Number when je.TryGetInt64(out var l) => l,
                JsonValueKind.Number when je.TryGetDouble(out var d) => d,
                JsonValueKind.String => je.GetString(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => je.ToString()
            };
        }

        if (raw is null) return null;

        return kind switch
        {
            RegistryValueKind.DWord => Convert.ToInt32(raw),
            RegistryValueKind.QWord => Convert.ToInt64(raw),
            RegistryValueKind.String => raw as string ?? Convert.ToString(raw),
            RegistryValueKind.ExpandString => raw as string ?? Convert.ToString(raw),
            _ => null
        };
    }
}
