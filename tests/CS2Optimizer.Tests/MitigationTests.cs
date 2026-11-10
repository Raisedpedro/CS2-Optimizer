using Microsoft.Win32;
using CS2Optimizer.Core.Mitigations;
using CS2Optimizer.Core.Models;
using CS2Optimizer.Core.Services;

namespace CS2Optimizer.Tests;

public sealed class MitigationTests
{
    private static readonly CpuInfo Intel = new("Intel Core", "GenuineIntel", 8, 16, 5000, 9);
    private static readonly CpuInfo Amd = new("Ryzen", "AuthenticAMD", 8, 16, 4500, 9);
    private static readonly CpuInfo Arm = new("ARM CPU", "", 8, 8, 3000, 12);

    [Fact]
    public void Resolve_padrao_erases_all_three_values()
    {
        var result = MitigationService.Resolve(new[] { "padrao" });

        Assert.True(result.Success);
        Assert.True(result.Erase);
        Assert.Equal(0u, result.Value);
        Assert.Equal(MitigationSecurity.Padrao, result.Security);
    }

    [Fact]
    public void Resolve_rejects_padrao_combined_with_other_profiles()
    {
        var result = MitigationService.Resolve(new[] { "padrao", "desligar-v2" });

        Assert.False(result.Success);
        Assert.Contains("padrao", result.Error);
    }

    [Fact]
    public void Resolve_rejects_reduced_and_higher_profiles_together()
    {
        var result = MitigationService.Resolve(new[] { "desligar-v2", "intel-bhi" });

        Assert.False(result.Success);
        Assert.Contains("REDUZEM", result.Error);
    }

    [Fact]
    public void Resolve_combines_same_vendor_profiles_by_or()
    {
        var result = MitigationService.Resolve(new[] { "intel-full", "intel-bhi" });

        Assert.True(result.Success);
        Assert.Equal(72u | 8388608u, result.Value);
        Assert.True(result.MinVm);
        Assert.Equal(MitigationSecurity.Maior, result.Security);
        Assert.Contains("Intel", result.AppliesTo);
    }

    [Fact]
    public void Resolve_accepts_multiple_profiles_in_one_string()
    {
        var result = MitigationService.Resolve(new[] { "desligar-v2,desligar-tudo" });

        Assert.True(result.Success);
        Assert.Equal(3u, result.Value);
        Assert.Equal(MitigationSecurity.Reduzida, result.Security);
    }

    [Fact]
    public void Resolve_rejects_profiles_from_different_vendors()
    {
        var result = MitigationService.Resolve(new[] { "intel-full", "amd-btc" });

        Assert.False(result.Success);
        Assert.Contains("fabricantes diferentes", result.Error);
    }

    [Fact]
    public void Resolve_rejects_unknown_profile()
    {
        var result = MitigationService.Resolve(new[] { "nao-existe" });

        Assert.False(result.Success);
        Assert.Contains("desconhecido", result.Error);
    }

    [Fact]
    public void DecodeState_all_values_absent_is_windows_default()
    {
        var state = MitigationService.DecodeState(Intel, Missing(), Missing(), Missing());

        Assert.True(state.IsDefault);
        Assert.False(state.IsEffective);
        Assert.False(state.IsReduced);
        Assert.Equal("Intel", state.Vendor);
        Assert.Empty(state.MatchingProfiles);
        Assert.Equal("", state.Warning);
    }

    [Fact]
    public void DecodeState_override_3_with_mask_3_is_reduced_and_decodes_bits()
    {
        var state = MitigationService.DecodeState(
            Intel,
            new RegistryValueSnapshot(true, RegistryValueKind.DWord, 3),
            new RegistryValueSnapshot(true, RegistryValueKind.DWord, 3),
            Missing());

        Assert.False(state.IsDefault);
        Assert.True(state.IsEffective);
        Assert.True(state.IsReduced);
        Assert.Equal(3L, state.Override);
        Assert.Contains("desligar-tudo", state.MatchingProfiles);
        Assert.Contains(state.Bits, bit => bit.Contains("Spectre v2"));
        Assert.Contains(state.Bits, bit => bit.Contains("Meltdown"));
    }

    [Fact]
    public void DecodeState_missing_mask_warns_and_is_not_effective()
    {
        var state = MitigationService.DecodeState(
            Intel,
            new RegistryValueSnapshot(true, RegistryValueKind.DWord, 3),
            Missing(),
            Missing());

        Assert.False(state.IsEffective);
        Assert.False(state.IsReduced);
        Assert.Contains("AUSENTE", state.Warning);
    }

    [Fact]
    public void DecodeState_zero_mask_warns_and_is_not_effective()
    {
        var state = MitigationService.DecodeState(
            Intel,
            new RegistryValueSnapshot(true, RegistryValueKind.DWord, 3),
            new RegistryValueSnapshot(true, RegistryValueKind.DWord, 0),
            Missing());

        Assert.False(state.IsEffective);
        Assert.False(state.IsReduced);
        Assert.Contains("provavelmente", state.Warning);
    }

    [Fact]
    public void DecodeState_reads_minvm_and_is_no_longer_default()
    {
        var state = MitigationService.DecodeState(
            Amd,
            Missing(),
            Missing(),
            new RegistryValueSnapshot(true, RegistryValueKind.String, "1.0"));

        Assert.False(state.IsDefault);
        Assert.Equal("1.0", state.MinVm);
        Assert.Equal("AMD", state.Vendor);
    }

    [Theory]
    [InlineData("GenuineIntel", 9, "Intel")]
    [InlineData("AuthenticAMD", 9, "AMD")]
    [InlineData("", 12, "ARM")]
    [InlineData("Outro", 9, "Desconhecido")]
    public void DetectVendor_uses_manufacturer_and_architecture(string manufacturer, ushort architecture, string expected)
    {
        var cpu = new CpuInfo("CPU", manufacturer, 4, 8, 3000, architecture);
        Assert.Equal(expected, MitigationService.DetectVendor(cpu));
    }

    private static RegistryValueSnapshot Missing() => new(false, null, null);
}
