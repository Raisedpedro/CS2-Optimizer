using Microsoft.Win32;

namespace CS2Optimizer.Core.Services;

public sealed record RegistryValueSnapshot(bool Exists, RegistryValueKind? Kind, object? Value);

/// <summary>Abstração mínima para permitir testes do journal sem tocar no registro real.</summary>
public interface IRegistryAccess
{
    RegistryValueSnapshot GetValue(string path, string name);
    void SetValueRaw(string path, string name, object value, RegistryValueKind kind);
    void DeleteValueRaw(string path, string name);
    bool KeyExists(string path);
}

/// <summary>
/// Leitura/escrita de registro restrita a HKLM e HKCU, com journal automático antes
/// de cada escrita/remoção (seção 5.2 do plano).
/// </summary>
public sealed class RegistryService : IRegistryAccess
{
    private readonly RegistryJournal _journal;

    public RegistryService(RegistryJournal journal)
    {
        _journal = journal;
    }

    public static (RegistryHive Hive, string SubKey, string Normalized) ParsePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Caminho de registro vazio.", nameof(path));

        var p = path.Trim();
        RegistryHive hive;
        string rest;

        if (p.StartsWith("HKLM:", StringComparison.OrdinalIgnoreCase))
        {
            hive = RegistryHive.LocalMachine;
            rest = p[5..];
        }
        else if (p.StartsWith("HKCU:", StringComparison.OrdinalIgnoreCase))
        {
            hive = RegistryHive.CurrentUser;
            rest = p[5..];
        }
        else if (p.StartsWith("HKLM\\", StringComparison.OrdinalIgnoreCase))
        {
            hive = RegistryHive.LocalMachine;
            rest = p[5..];
        }
        else if (p.StartsWith("HKCU\\", StringComparison.OrdinalIgnoreCase))
        {
            hive = RegistryHive.CurrentUser;
            rest = p[5..];
        }
        else if (p.StartsWith("HKEY_LOCAL_MACHINE\\", StringComparison.OrdinalIgnoreCase))
        {
            hive = RegistryHive.LocalMachine;
            rest = p[19..];
        }
        else if (p.StartsWith("HKEY_CURRENT_USER\\", StringComparison.OrdinalIgnoreCase))
        {
            hive = RegistryHive.CurrentUser;
            rest = p[18..];
        }
        else
        {
            throw new ArgumentException($"Só HKLM e HKCU são aceitos: {path}", nameof(path));
        }

        rest = rest.TrimStart('\\').TrimEnd('\\');
        if (rest.Length == 0)
            throw new ArgumentException($"Chave raiz não é aceita: {path}", nameof(path));

        var normalized = (hive == RegistryHive.LocalMachine ? "HKLM\\" : "HKCU\\") + rest;
        return (hive, rest, normalized);
    }

    public static bool IsSupportedKind(RegistryValueKind kind) =>
        kind is RegistryValueKind.DWord or RegistryValueKind.String
            or RegistryValueKind.QWord or RegistryValueKind.ExpandString;

    public static string KindToJournalName(RegistryValueKind kind) => kind switch
    {
        RegistryValueKind.DWord => "DWord",
        RegistryValueKind.QWord => "QWord",
        RegistryValueKind.ExpandString => "ExpandString",
        RegistryValueKind.String => "String",
        _ => kind.ToString()
    };

    public static RegistryValueKind? JournalNameToKind(string? name) => name switch
    {
        "DWord" => RegistryValueKind.DWord,
        "QWord" => RegistryValueKind.QWord,
        "ExpandString" => RegistryValueKind.ExpandString,
        "String" => RegistryValueKind.String,
        _ => null
    };

    public RegistryValueSnapshot GetValue(string path, string name)
    {
        var (hive, subKey, _) = ParsePath(path);
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using var key = baseKey.OpenSubKey(subKey, writable: false);
            if (key is null)
                return new RegistryValueSnapshot(false, null, null);

            if (!key.GetValueNames().Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase)))
                return new RegistryValueSnapshot(false, null, null);

            var kind = key.GetValueKind(name);
            var value = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            return new RegistryValueSnapshot(true, kind, value);
        }
        catch
        {
            return new RegistryValueSnapshot(false, null, null);
        }
    }

    public long? GetInt64(string path, string name)
    {
        var v = GetValue(path, name);
        if (!v.Exists || v.Value is null) return null;
        try { return Convert.ToInt64(v.Value); }
        catch { return null; }
    }

    public int? GetInt32(string path, string name)
    {
        var v = GetInt64(path, name);
        if (v is null) return null;
        return unchecked((int)v.Value);
    }

    public string? GetString(string path, string name)
    {
        var v = GetValue(path, name);
        if (!v.Exists || v.Value is null) return null;
        return v.Value as string ?? Convert.ToString(v.Value);
    }

    public bool KeyExists(string path)
    {
        var (hive, subKey, _) = ParsePath(path);
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using var key = baseKey.OpenSubKey(subKey, writable: false);
            return key is not null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Grava um valor, registrando o valor anterior no journal.</summary>
    public void SetValue(string path, string name, object value, RegistryValueKind kind = RegistryValueKind.DWord)
    {
        if (!IsSupportedKind(kind))
            throw new ArgumentException($"Tipo de registro não suportado: {kind}", nameof(kind));

        _journal.AddSnapshot(path, name, GetValue(path, name));
        SetValueRaw(path, name, value, kind);
    }

    /// <summary>Remove um valor, registrando o valor anterior no journal.</summary>
    public void DeleteValue(string path, string name)
    {
        _journal.AddSnapshot(path, name, GetValue(path, name));
        DeleteValueRaw(path, name);
    }

    public void SetValueRaw(string path, string name, object value, RegistryValueKind kind)
    {
        var (hive, subKey, _) = ParsePath(path);
        using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
        using var key = baseKey.CreateSubKey(subKey, writable: true)
            ?? throw new InvalidOperationException($"Não consegui abrir/criar {path}");
        key.SetValue(name, value, kind);
    }

    public void DeleteValueRaw(string path, string name)
    {
        var (hive, subKey, _) = ParsePath(path);
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using var key = baseKey.OpenSubKey(subKey, writable: true);
            key?.DeleteValue(name, throwOnMissingValue: false);
        }
        catch
        {
            // Remover o que não existe não é erro.
        }
    }
}
