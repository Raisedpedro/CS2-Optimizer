using System.Diagnostics;
using System.Text.RegularExpressions;
using CS2Optimizer.Core.Mitigations;
using CS2Optimizer.Core.Models;
using CS2Optimizer.Core.Services;

namespace CS2Optimizer.Core.Hardware;

/// <summary>Lê a máquina (hardware, Steam/CS2, registro, BCD, energia) sem alterar nada.</summary>
public sealed class SystemScanner
{
    private static readonly string[] OverlayProcesses =
    {
        "Discord", "GameOverlayUI", "RTSS", "MSIAfterburner", "Overwolf", "OverwolfBrowser",
        "medal", "obs64", "nvsphelper64", "GeForceExperience", "chrome", "msedge",
        "RadeonSoftware", "iCUE", "SignalRgb", "LGHUB", "Razer Synapse 3", "RazerCentralService"
    };

    private static readonly string[] LaptopChassisTypes =
    {
        "8", "9", "10", "11", "14", "30", "31", "32"
    };

    private readonly RegistryService _registry;
    private readonly ExternalProcessRunner _runner;
    private readonly MitigationService _mitigation;
    private readonly AppLogger _log;

    public SystemScanner(RegistryService registry, ExternalProcessRunner runner,
        MitigationService mitigation, AppLogger log)
    {
        _registry = registry;
        _runner = runner;
        _mitigation = mitigation;
        _log = log;
    }

    public async Task<FixContext> ScanAsync(CancellationToken cancellationToken = default)
    {
        var ctx = new FixContext();

        // ---------------- Sistema ----------------
        var os = WmiQuery.Query("SELECT Caption, BuildNumber FROM Win32_OperatingSystem").FirstOrDefault();
        ctx.OsCaption = os is null ? Environment.OSVersion.VersionString : WmiQuery.Str(os, "Caption");
        ctx.BuildNumber = os is null ? Environment.OSVersion.Version.Build : WmiQuery.Int(os, "BuildNumber") ?? 0;

        var board = WmiQuery.Query("SELECT Manufacturer, Product FROM Win32_BaseBoard").FirstOrDefault();
        if (board is not null)
            ctx.Board = $"{WmiQuery.Str(board, "Manufacturer")} {WmiQuery.Str(board, "Product")}".Trim();

        var machine = WmiQuery.Query("SELECT Manufacturer, Model FROM Win32_ComputerSystem").FirstOrDefault();
        if (machine is not null)
            ctx.Machine = $"{WmiQuery.Str(machine, "Manufacturer")} {WmiQuery.Str(machine, "Model")}".Trim();

        var bios = WmiQuery.Query("SELECT SMBIOSBIOSVersion, Manufacturer, ReleaseDate FROM Win32_BIOS").FirstOrDefault();
        if (bios is not null)
        {
            ctx.BiosVersion = WmiQuery.Str(bios, "SMBIOSBIOSVersion");
            ctx.BiosManufacturer = WmiQuery.Str(bios, "Manufacturer");
            ctx.BiosDate = ParseBiosDate(bios.TryGetValue("ReleaseDate", out var rd) ? rd : null);
        }

        var enclosure = WmiQuery.Query("SELECT ChassisTypes FROM Win32_SystemEnclosure").FirstOrDefault();
        if (enclosure is not null)
        {
            ctx.IsLaptop = WmiQuery.UShorts(enclosure, "ChassisTypes")
                .Any(t => LaptopChassisTypes.Contains(t.ToString()));
        }

        ctx.HasBattery = WmiQuery.Query("SELECT Name FROM Win32_Battery").Count > 0;
        if (!ctx.IsLaptop && ctx.HasBattery) ctx.IsLaptop = true;

        // ---------------- CPU ----------------
        var cpuRow = WmiQuery.Query(
            "SELECT Name, Manufacturer, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed, Architecture " +
            "FROM Win32_Processor").FirstOrDefault();
        if (cpuRow is not null)
        {
            ctx.Cpu = new CpuInfo(
                WmiQuery.Str(cpuRow, "Name"),
                WmiQuery.Str(cpuRow, "Manufacturer"),
                WmiQuery.Int(cpuRow, "NumberOfCores") ?? 0,
                WmiQuery.Int(cpuRow, "NumberOfLogicalProcessors") ?? 0,
                WmiQuery.UInt(cpuRow, "MaxClockSpeed") ?? 0,
                (ushort)(WmiQuery.UInt(cpuRow, "Architecture") ?? 0));
        }

        // ---------------- RAM ----------------
        var ramRows = WmiQuery.Query(
            "SELECT DeviceLocator, Capacity, Speed, ConfiguredClockSpeed, Manufacturer, SMBIOSMemoryType " +
            "FROM Win32_PhysicalMemory");
        ctx.Ram = ramRows.Select(r => new RamModule(
            WmiQuery.Str(r, "DeviceLocator"),
            WmiQuery.ULong(r, "Capacity") ?? 0,
            WmiQuery.UInt(r, "Speed") ?? 0,
            WmiQuery.UInt(r, "ConfiguredClockSpeed") ?? 0,
            WmiQuery.Str(r, "Manufacturer"),
            WmiQuery.UInt(r, "SMBIOSMemoryType") ?? 0)).ToArray();

        // ---------------- GPU ----------------
        var gpuRows = WmiQuery.Query(
            "SELECT Name, DriverVersion, CurrentHorizontalResolution, CurrentVerticalResolution, CurrentRefreshRate " +
            "FROM Win32_VideoController");
        ctx.Gpus = gpuRows.Select(g => new GpuInfo(
            WmiQuery.Str(g, "Name"),
            WmiQuery.Str(g, "DriverVersion"),
            WmiQuery.UInt(g, "CurrentHorizontalResolution") ?? 0,
            WmiQuery.UInt(g, "CurrentVerticalResolution") ?? 0,
            WmiQuery.UInt(g, "CurrentRefreshRate") ?? 0)).ToArray();
        ctx.HasNvidia = ctx.Gpus.Any(g => g.Name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase));
        ctx.HasAmd = ctx.Gpus.Any(g =>
            g.Name.Contains("AMD", StringComparison.OrdinalIgnoreCase) ||
            g.Name.Contains("Radeon", StringComparison.OrdinalIgnoreCase));

        // ---------------- Armazenamento ----------------
        var volumes = new List<VolumeInfo>();
        var ssdLetters = new List<char>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.DriveType != DriveType.Fixed || !drive.IsReady) continue;
            try
            {
                var letter = drive.Name[0];
                var total = (ulong)drive.TotalSize;
                var free = (ulong)drive.TotalFreeSpace;
                var percent = total > 0 ? Math.Round(free / (double)total * 100, 1) : 0;
                var media = GetMediaType(letter);
                volumes.Add(new VolumeInfo(letter, media, total, free, percent));
                if (string.Equals(media, "SSD", StringComparison.OrdinalIgnoreCase)) ssdLetters.Add(letter);
            }
            catch { }
        }
        ctx.Volumes = volumes;
        ctx.SsdLetters = ssdLetters;

        // ---------------- Steam / CS2 ----------------
        ctx.Steam = FindSteam();
        ctx.LayerPaths = new[]
        {
            @"HKCU\Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers",
            @"HKLM\Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers"
        };

        if (!string.IsNullOrWhiteSpace(ctx.Cs2Exe))
        {
            foreach (var layer in ctx.LayerPaths)
            {
                var value = _registry.GetString(layer, ctx.Cs2Exe);
                if (value is not null &&
                    value.Contains("DISABLEDXMAXIMIZEDWINDOWEDMODE", StringComparison.OrdinalIgnoreCase))
                {
                    ctx.FsoDisabled = true;
                    ctx.FsoLayerPath = layer;
                }
            }
        }

        var shaderPaths = new List<string>();
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var candidates = new[]
        {
            Path.Combine(localAppData, "D3DSCache"),
            Path.Combine(localAppData, "NVIDIA", "DXCache"),
            Path.Combine(localAppData, "NVIDIA", "GLCache"),
            Path.Combine(localAppData, "AMD", "DxCache"),
            Path.Combine(localAppData, "AMD", "DxcCache"),
            Path.Combine(localAppData, "AMD", "GLCache"),
            Path.Combine(userProfile, "AppData", "LocalLow", "NVIDIA", "PerDriverVersion", "DXCache")
        };
        shaderPaths.AddRange(candidates.Where(Directory.Exists));
        ctx.ShaderPaths = shaderPaths;

        double shaderMb = shaderPaths.Sum(GetFolderSizeMB);
        shaderMb += ctx.Steam.SteamShaderPaths.Sum(GetFolderSizeMB);
        ctx.ShaderMB = Math.Round(shaderMb, 1);

        var temp = Environment.GetEnvironmentVariable("TEMP") ?? Path.GetTempPath();
        ctx.TempMB = GetFolderSizeMB(temp);

        // ---------------- MPO / DWM ----------------
        ctx.DwmPath = @"HKLM\SOFTWARE\Microsoft\Windows\Dwm";
        ctx.OverlayTestMode = _registry.GetInt32(ctx.DwmPath, "OverlayTestMode");

        // ---------------- BCD / timer ----------------
        ctx.BcdKeys = await ReadBcdKeysAsync(cancellationToken);

        // ---------------- Energia / Game Bar ----------------
        await ReadPowerPlanAsync(ctx, cancellationToken);

        ctx.FastBoot = Int(@"HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Power", "HiberbootEnabled");
        ctx.GameDvrAppCapture = Int(@"HKCU\Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled");
        ctx.GameDvrEnabled = Int(@"HKCU\System\GameConfigStore", "GameDVR_Enabled");
        ctx.GameMode = Int(@"HKCU\Software\Microsoft\GameBar", "AutoGameModeEnabled");
        ctx.Hags = Int(@"HKLM\SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode");

        var bgPolicy = Int(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\AppPrivacy", "LetAppsRunInBackground");
        var bgUser = Int(@"HKCU\Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", "GlobalUserDisabled");
        ctx.BackgroundAppsDisabled = ctx.IsWin11 ? bgPolicy == 2 : bgUser == 1 || bgPolicy == 2;

        ctx.MouseSpeed = _registry.GetString(@"HKCU\Control Panel\Mouse", "MouseSpeed");
        ctx.SystemResponsiveness = Int(
            @"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "SystemResponsiveness");
        ctx.NetworkThrottling = Int(
            @"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "NetworkThrottlingIndex");
        ctx.GpuPreference = string.IsNullOrWhiteSpace(ctx.Cs2Exe)
            ? null
            : _registry.GetString(@"HKCU\Software\Microsoft\DirectX\UserGpuPreferences", ctx.Cs2Exe);
        ctx.Cs2PriorityClass = Int(
            @"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\cs2.exe\PerfOptions",
            "CpuPriorityClass");

        // ---------------- Telemetria ----------------
        ctx.TelemetryServiceStart = Int(@"HKLM\SYSTEM\CurrentControlSet\Services\DiagTrack", "Start");
        ctx.TelemetryWapStart = Int(@"HKLM\SYSTEM\CurrentControlSet\Services\dmwappushservice", "Start");
        ctx.TelemetryLevel = Int(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry");
        ctx.TelemetryCeip = Int(@"HKLM\SOFTWARE\Policies\Microsoft\SQMClient\Windows", "CEIPEnable");
        ctx.TelemetryAdId = Int(@"HKCU\Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled");
        ctx.TelemetryFeed = Int(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\System", "EnableActivityFeed");
        ctx.TelemetryWer = Int(@"HKLM\SOFTWARE\Microsoft\Windows\Windows Error Reporting", "Disabled");
        ctx.TelemetryVoice = Int(@"HKCU\Software\Microsoft\Speech_OneCore\Settings\OnlineSpeechPrivacy", "HasAccepted");
        ctx.TelemetryCompatTask = await GetScheduledTaskStateAsync(
            @"\Microsoft\Windows\Application Experience\", "Microsoft Compatibility Appraiser", cancellationToken);

        // ---------------- Mitigações ----------------
        ctx.Mit = _mitigation.GetState(ctx.Cpu);

        // ---------------- VBS / HVCI ----------------
        var deviceGuard = WmiQuery.Query("SELECT SecurityServicesRunning FROM Win32_DeviceGuard", WmiQuery.DeviceGuard);
        ctx.HvciRead = deviceGuard.Count > 0;
        ctx.HvciActive = deviceGuard.Any(row =>
        {
            if (!row.TryGetValue("SecurityServicesRunning", out var value) || value is not Array array)
                return false;
            return array.Cast<object>().Any(v =>
            {
                try { return Convert.ToInt32(v) == 2; } catch { return false; }
            });
        });

        // ---------------- Processos / inicialização ----------------
        var running = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                if (OverlayProcesses.Contains(process.ProcessName, StringComparer.OrdinalIgnoreCase))
                    running.Add(process.ProcessName);
            }
        }
        ctx.RunningOverlayProcesses = running.OrderBy(x => x).ToArray();

        var startup = WmiQuery.Query("SELECT Name, Command FROM Win32_StartupCommand");
        ctx.StartupCommands = startup.Select(r =>
            $"{WmiQuery.Str(r, "Name")} | {WmiQuery.Str(r, "Command")}").ToArray();

        // ---------------- NVIDIA (best-effort) ----------------
        if (ctx.HasNvidia)
            ctx.NvidiaInfoLines = await ReadNvidiaAsync(cancellationToken);

        return ctx;
    }

    private int? Int(string path, string name) => _registry.GetInt32(path, name);

    private static DateTime? ParseBiosDate(object? value)
    {
        if (value is DateTime dt) return dt;
        if (value is string s)
        {
            try { return System.Management.ManagementDateTimeConverter.ToDateTime(s); }
            catch { }
        }
        return null;
    }

    private async Task<Dictionary<string, string>> ReadBcdKeysAsync(CancellationToken cancellationToken)
    {
        var keys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var result = await _runner.RunAsync("bcdedit.exe", new[] { "/enum", "{current}" },
                "Lendo BCD", cancellationToken: cancellationToken);
            var text = (result.StandardOutput ?? "") + "\n" + (result.StandardError ?? "");
            foreach (var key in new[] { "useplatformclock", "useplatformtick", "disabledynamictick", "tscsyncpolicy" })
            {
                var match = Regex.Match(text, $@"(?im)^\s*{Regex.Escape(key)}\s+(\S+)");
                if (match.Success) keys[key] = match.Groups[1].Value;
            }
        }
        catch { }
        return keys;
    }

    private async Task ReadPowerPlanAsync(FixContext ctx, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _runner.RunAsync("powercfg.exe", new[] { "/getactivescheme" },
                "Lendo plano de energia", cancellationToken: cancellationToken);
            var text = (result.StandardOutput ?? "").Trim();
            var guid = Regex.Match(text,
                "[0-9a-fA-F]{8}-([0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}").Value;
            ctx.ActivePowerGuid = guid;

            var byName = Regex.IsMatch(text,
                "Alto desempenho|High performance|Desempenho m.ximo|Ultimate",
                RegexOptions.IgnoreCase);

            var minAc = -1;
            if (!string.IsNullOrWhiteSpace(guid))
            {
                var query = await _runner.RunAsync("powercfg.exe",
                    new[] { "/query", guid, "SUB_PROCESSOR", "PROCTHROTTLEMIN" },
                    "Lendo energia da CPU", cancellationToken: cancellationToken);
                var qtext = (query.StandardOutput ?? "") + "\n" + (query.StandardError ?? "");
                var matches = Regex.Matches(qtext, "0x([0-9a-fA-F]{8})");
                if (matches.Count >= 2)
                {
                    try { minAc = Convert.ToInt32(matches[^2].Groups[1].Value, 16); }
                    catch { minAc = -1; }
                }
            }

            ctx.MinAcValue = minAc;
            ctx.OnPerformancePowerPlan = byName || minAc == 100;
        }
        catch { }
    }

    private async Task<string> GetScheduledTaskStateAsync(string taskPath, string taskName,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _runner.RunAsync("schtasks.exe",
                new[] { "/query", "/tn", taskPath + taskName, "/xml" },
                "Lendo tarefa agendada", cancellationToken: cancellationToken);
            if (!result.Started || result.ExitCode != 0) return "Ausente";
            return Regex.IsMatch(result.StandardOutput ?? "",
                @"<Enabled>\s*false\s*</Enabled>", RegexOptions.IgnoreCase)
                ? "Disabled"
                : "Ready";
        }
        catch
        {
            return "Ausente";
        }
    }

    private async Task<IReadOnlyList<string>> ReadNvidiaAsync(CancellationToken cancellationToken)
    {
        var lines = new List<string>();
        try
        {
            var nvsmi = ExternalProcessRunner.FindOnPath("nvidia-smi.exe");
            if (nvsmi is null) return lines;

            var query = await _runner.RunAsync(nvsmi, new[]
            {
                "--query-gpu=pcie.link.gen.current,pcie.link.gen.max,pcie.link.width.current,power.limit",
                "--format=csv,noheader"
            }, "Lendo NVIDIA", cancellationToken: cancellationToken);
            if (query.Started && query.ExitCode == 0 && !string.IsNullOrWhiteSpace(query.StandardOutput))
                lines.Add("NVIDIA PCIe/power (gen atual, gen max, largura, power limit): " +
                          query.StandardOutput.Trim());

            var memory = await _runner.RunAsync(nvsmi, new[] { "-q", "-d", "MEMORY" },
                "Lendo BAR1", cancellationToken: cancellationToken);
            var match = Regex.Match(memory.StandardOutput ?? "",
                @"(?s)BAR1 Memory Usage\s+Total\s+:\s+(\d+)\s+MiB");
            if (match.Success && int.TryParse(match.Groups[1].Value, out var bar))
            {
                lines.Add(bar > 512
                    ? $"Resizable BAR: ATIVO (BAR1 = {bar} MiB). Em NVIDIA o CS2 geralmente ignora - sem problema."
                    : $"Resizable BAR: desativado (BAR1 = {bar} MiB).");
            }
        }
        catch { }
        return lines;
    }

    private static SteamInfo FindSteam()
    {
        string? steamPath = null;
        try
        {
            using var hkcu = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            steamPath = hkcu?.GetValue("SteamPath") as string;
        }
        catch { }

        if (string.IsNullOrWhiteSpace(steamPath))
        {
            try
            {
                using var hklm = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam");
                steamPath = hklm?.GetValue("InstallPath") as string;
            }
            catch { }
        }

        if (string.IsNullOrWhiteSpace(steamPath))
        {
            try
            {
                using var hklm = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Valve\Steam");
                steamPath = hklm?.GetValue("InstallPath") as string;
            }
            catch { }
        }

        if (string.IsNullOrWhiteSpace(steamPath))
        {
            try
            {
                var process = Process.GetProcessesByName("steam").FirstOrDefault();
                if (process is not null)
                {
                    using (process)
                    {
                        var file = process.MainModule?.FileName;
                        if (!string.IsNullOrWhiteSpace(file)) steamPath = Path.GetDirectoryName(file);
                    }
                }
            }
            catch { }
        }

        if (string.IsNullOrWhiteSpace(steamPath))
        {
            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            foreach (var candidate in new[]
                     {
                         Path.Combine(programFilesX86, "Steam"),
                         Path.Combine(programFiles, "Steam"),
                         @"C:\Steam", @"D:\Steam", @"D:\SteamLibrary", @"E:\Steam", @"E:\SteamLibrary"
                     })
            {
                if (Directory.Exists(Path.Combine(candidate, "steamapps")))
                {
                    steamPath = candidate;
                    break;
                }
            }
        }

        var libraries = new List<string>();
        if (!string.IsNullOrWhiteSpace(steamPath))
        {
            steamPath = steamPath.Replace('/', '\\');
            libraries.Add(steamPath);

            var vdf = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
            try
            {
                if (File.Exists(vdf))
                {
                    var text = File.ReadAllText(vdf);
                    foreach (Match match in Regex.Matches(text, @"""path""\s+""([^""]+)"""))
                    {
                        var path = match.Groups[1].Value.Replace(@"\\", @"\");
                        if (!libraries.Contains(path, StringComparer.OrdinalIgnoreCase))
                            libraries.Add(path);
                    }
                }
            }
            catch { }
        }

        var uniqueLibraries = libraries
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        string? cs2 = null;
        foreach (var library in uniqueLibraries)
        {
            var candidate = Path.Combine(library,
                "steamapps", "common", "Counter-Strike Global Offensive", "game", "bin", "win64", "cs2.exe");
            if (File.Exists(candidate))
            {
                cs2 = candidate;
                break;
            }
        }

        var shaderPaths = new List<string>();
        foreach (var library in uniqueLibraries)
        {
            var shader = Path.Combine(library, "steamapps", "shadercache", "730");
            if (Directory.Exists(shader)) shaderPaths.Add(shader);
        }

        var userdata = new List<string>();
        if (!string.IsNullOrWhiteSpace(steamPath))
        {
            var userdataRoot = Path.Combine(steamPath, "userdata");
            if (Directory.Exists(userdataRoot))
            {
                try
                {
                    foreach (var account in Directory.GetDirectories(userdataRoot))
                    {
                        var folder = Path.Combine(account, "730");
                        if (Directory.Exists(folder)) userdata.Add(folder);
                    }
                }
                catch { }
            }
        }

        var cachePaths = new List<string>();
        if (!string.IsNullOrWhiteSpace(steamPath))
        {
            foreach (var sub in new[] { "depotcache", Path.Combine("appcache", "httpcache") })
            {
                var path = Path.Combine(steamPath, sub);
                if (Directory.Exists(path)) cachePaths.Add(path);
            }
        }

        return new SteamInfo(steamPath, uniqueLibraries, cs2, userdata, shaderPaths, cachePaths);
    }

    private static string GetMediaType(char letter)
    {
        try
        {
            var partitions = WmiQuery.Query(
                $"SELECT DiskNumber FROM MSFT_Partition WHERE DriveLetter = {(int)letter}",
                WmiQuery.Storage);
            if (partitions.Count == 0) return "Desconhecido";

            var diskNumber = WmiQuery.UInt(partitions[0], "DiskNumber");
            if (diskNumber is null) return "Desconhecido";

            var physical = WmiQuery.Query(
                $"SELECT MediaType FROM MSFT_PhysicalDisk WHERE DeviceId = '{diskNumber}'",
                WmiQuery.Storage);
            if (physical.Count == 0) return "Desconhecido";

            return WmiQuery.UInt(physical[0], "MediaType") switch
            {
                3 => "HDD",
                4 => "SSD",
                5 => "SCM",
                _ => "Desconhecido"
            };
        }
        catch
        {
            return "Desconhecido";
        }
    }

    public static double GetFolderSizeMB(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return 0;
        long total = 0;
        try
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                try { total += new FileInfo(file).Length; } catch { }
            }
        }
        catch { }
        return Math.Round(total / 1024d / 1024d, 1);
    }
}
