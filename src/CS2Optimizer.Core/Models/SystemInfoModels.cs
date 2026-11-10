namespace CS2Optimizer.Core.Models;

public sealed record CpuInfo(
    string Name,
    string Manufacturer,
    int Cores,
    int LogicalProcessors,
    uint MaxClockMHz,
    ushort Architecture);

public sealed record RamModule(
    string Locator,
    ulong CapacityBytes,
    uint SpeedMHz,
    uint ConfiguredMHz,
    string Manufacturer,
    uint SmbiosMemoryType);

public sealed record GpuInfo(
    string Name,
    string DriverVersion,
    uint HorizontalResolution,
    uint VerticalResolution,
    uint RefreshRate);

public sealed record VolumeInfo(
    char Letter,
    string MediaType,
    ulong TotalBytes,
    ulong FreeBytes,
    double FreePercent);

public sealed record SteamInfo(
    string? SteamPath,
    IReadOnlyList<string> Libraries,
    string? Cs2Exe,
    IReadOnlyList<string> Userdata730,
    IReadOnlyList<string> SteamShaderPaths,
    IReadOnlyList<string> SteamCachePaths);

public sealed record BackupInfo(string Path, string Name, DateTime LastWrite, int FileCount, long TotalBytes)
{
    public string LastWriteDisplay => LastWrite.ToString("dd/MM/yyyy HH:mm");
    public string SizeDisplay => TotalBytes >= 1024 * 1024
        ? $"{TotalBytes / 1024d / 1024d:0.0} MB"
        : $"{TotalBytes / 1024d:0.0} KB";
    public string Summary => $"{FileCount} arquivo(s) · {SizeDisplay}";
}
