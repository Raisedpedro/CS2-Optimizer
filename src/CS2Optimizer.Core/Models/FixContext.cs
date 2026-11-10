using CS2Optimizer.Core.Mitigations;

namespace CS2Optimizer.Core.Models;

/// <summary>
/// Tudo o que o diagnóstico leu da máquina. O catálogo de correções só usa este
/// retrato, para que a aplicação e os testes sejam determinísticos.
/// </summary>
public sealed class FixContext
{
    public string OsCaption { get; set; } = "";
    public int BuildNumber { get; set; }
    public bool IsWin11 => BuildNumber >= 22000;
    public string Board { get; set; } = "";
    public string Machine { get; set; } = "";
    public string BiosVersion { get; set; } = "";
    public string BiosManufacturer { get; set; } = "";
    public DateTime? BiosDate { get; set; }
    public bool IsLaptop { get; set; }
    public bool HasBattery { get; set; }

    public CpuInfo Cpu { get; set; } = new("desconhecida", "", 0, 0, 0, 0);
    public IReadOnlyList<RamModule> Ram { get; set; } = Array.Empty<RamModule>();
    public IReadOnlyList<GpuInfo> Gpus { get; set; } = Array.Empty<GpuInfo>();
    public bool HasNvidia { get; set; }
    public bool HasAmd { get; set; }
    public IReadOnlyList<VolumeInfo> Volumes { get; set; } = Array.Empty<VolumeInfo>();
    public IReadOnlyList<char> SsdLetters { get; set; } = Array.Empty<char>();

    public SteamInfo Steam { get; set; } = new(null, Array.Empty<string>(), null, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());
    public string? Cs2Exe => Steam.Cs2Exe;
    public bool FsoDisabled { get; set; }
    public string? FsoLayerPath { get; set; }
    public IReadOnlyList<string> LayerPaths { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> ShaderPaths { get; set; } = Array.Empty<string>();
    public double ShaderMB { get; set; }
    public double TempMB { get; set; }

    public string DwmPath { get; set; } = @"HKLM\SOFTWARE\Microsoft\Windows\Dwm";
    public int? OverlayTestMode { get; set; }

    public IReadOnlyDictionary<string, string> BcdKeys { get; set; } = new Dictionary<string, string>();
    public string ActivePowerGuid { get; set; } = "";
    public bool OnPerformancePowerPlan { get; set; }
    public int MinAcValue { get; set; } = -1;

    public int? FastBoot { get; set; }
    public int? GameDvrAppCapture { get; set; }
    public int? GameDvrEnabled { get; set; }
    public int? GameMode { get; set; }
    public int? Hags { get; set; }
    public bool BackgroundAppsDisabled { get; set; }
    public string? MouseSpeed { get; set; }
    public int? SystemResponsiveness { get; set; }
    public int? NetworkThrottling { get; set; }
    public string? GpuPreference { get; set; }
    public int? Cs2PriorityClass { get; set; }

    public int? TelemetryServiceStart { get; set; }
    public int? TelemetryWapStart { get; set; }
    public int? TelemetryLevel { get; set; }
    public int? TelemetryCeip { get; set; }
    public int? TelemetryAdId { get; set; }
    public int? TelemetryFeed { get; set; }
    public int? TelemetryWer { get; set; }
    public int? TelemetryVoice { get; set; }
    public string TelemetryCompatTask { get; set; } = "Ausente";

    public MitigationState Mit { get; set; } = new(
        "desconhecida", "Desconhecido", null, null, null, null,
        true, false, false, "", Array.Empty<string>(), Array.Empty<string>());

    public bool HvciActive { get; set; }
    public bool HvciRead { get; set; }
    public IReadOnlyList<string> NvidiaInfoLines { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> RunningOverlayProcesses { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> StartupCommands { get; set; } = Array.Empty<string>();

    public bool IsVendor(string vendor) =>
        string.Equals(Mit.Vendor, vendor, StringComparison.OrdinalIgnoreCase);
}
