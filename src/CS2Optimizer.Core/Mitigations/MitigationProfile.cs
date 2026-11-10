namespace CS2Optimizer.Core.Mitigations;

public enum MitigationSecurity
{
    Padrao,
    Maior,
    Reduzida
}

public sealed record MitigationProfile(
    string Key,
    string Name,
    string Summary,
    string Explanation,
    uint Value,
    bool Erase,
    IReadOnlyList<string> AppliesTo,
    bool MinVm,
    MitigationSecurity Security)
{
    public string SecurityDisplay => Security switch
    {
        MitigationSecurity.Padrao => "padrão do Windows",
        MitigationSecurity.Maior => "MAIS seguro que o padrão (custa desempenho)",
        MitigationSecurity.Reduzida => "MENOS seguro que o padrão (INSEGURO)",
        _ => Security.ToString()
    };
}

public sealed record MitigationResolution(
    string? Error,
    IReadOnlyList<string> Keys,
    string Name,
    string Summary,
    uint Value,
    bool Erase,
    MitigationSecurity Security,
    IReadOnlyList<string> AppliesTo,
    bool MinVm,
    string Explanation)
{
    public bool Success => string.IsNullOrEmpty(Error);

    public static MitigationResolution Fail(string error) => new(
        error, Array.Empty<string>(), "", "", 0, false,
        MitigationSecurity.Padrao, Array.Empty<string>(), false, "");
}

public sealed record MitigationState(
    string Cpu,
    string Vendor,
    long? Override,
    long? Mask,
    string? MinVm,
    long? Value,
    bool IsDefault,
    bool IsEffective,
    bool IsReduced,
    string Warning,
    IReadOnlyList<string> MatchingProfiles,
    IReadOnlyList<string> Bits);
