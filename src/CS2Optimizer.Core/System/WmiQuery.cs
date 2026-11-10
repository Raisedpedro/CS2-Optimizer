using System.Management;

namespace CS2Optimizer.Core.Hardware;

internal static class WmiQuery
{
    public const string CimV2 = @"\\.\root\cimv2";
    public const string Storage = @"\\.\root\Microsoft\Windows\Storage";
    public const string DeviceGuard = @"\\.\root\Microsoft\Windows\DeviceGuard";

    public static List<Dictionary<string, object?>> Query(string wql, string scope = CimV2)
    {
        var rows = new List<Dictionary<string, object?>>();
        try
        {
            using var searcher = new ManagementObjectSearcher(new ManagementScope(scope), new ObjectQuery(wql));
            using var results = searcher.Get();
            foreach (ManagementObject item in results)
            {
                using (item)
                {
                    var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    foreach (PropertyData property in item.Properties)
                        row[property.Name] = property.Value;
                    rows.Add(row);
                }
            }
        }
        catch
        {
            // WMI indisponível/classe ausente: o chamador trata a lista vazia.
        }

        return rows;
    }

    public static string Str(IReadOnlyDictionary<string, object?> row, string name)
        => row.TryGetValue(name, out var value) && value is not null
            ? Convert.ToString(value)?.Trim() ?? ""
            : "";

    public static int? Int(IReadOnlyDictionary<string, object?> row, string name)
    {
        if (!row.TryGetValue(name, out var value) || value is null) return null;
        try { return Convert.ToInt32(value); } catch { return null; }
    }

    public static uint? UInt(IReadOnlyDictionary<string, object?> row, string name)
    {
        if (!row.TryGetValue(name, out var value) || value is null) return null;
        try { return Convert.ToUInt32(value); } catch { return null; }
    }

    public static ulong? ULong(IReadOnlyDictionary<string, object?> row, string name)
    {
        if (!row.TryGetValue(name, out var value) || value is null) return null;
        try { return Convert.ToUInt64(value); } catch { return null; }
    }

    public static bool Bool(IReadOnlyDictionary<string, object?> row, string name)
    {
        if (!row.TryGetValue(name, out var value) || value is null) return false;
        try { return Convert.ToBoolean(value); } catch { return false; }
    }

    public static IEnumerable<ushort> UShorts(IReadOnlyDictionary<string, object?> row, string name)
    {
        if (!row.TryGetValue(name, out var value) || value is not Array array) yield break;
        foreach (var item in array)
        {
            ushort parsed;
            try { parsed = Convert.ToUInt16(item); } catch { continue; }
            yield return parsed;
        }
    }
}
