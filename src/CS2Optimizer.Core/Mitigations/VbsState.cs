namespace CS2Optimizer.Core.Mitigations;

/// <summary>
/// Estado da Segurança baseada em virtualização (VBS) e da Integridade de
/// memória (HVCI). Lê o BCD e as chaves que o programa sabe alterar; não é uma
/// medição de desempenho, só o retrato da configuração.
/// </summary>
public sealed record VbsState(
    string HypervisorLaunchType,
    int? EnableVirtualizationBasedSecurity,
    int? RequirePlatformSecurityFeatures,
    int? HvciEnabled,
    int? KernelShadowStacksEnabled,
    int? CredentialGuardEnabled,
    int? LsaCfgFlags,
    bool Running)
{
    public static VbsState Unknown { get; } = new(
        "", null, null, null, null, null, null, false);

    public bool BcdOff => string.Equals(
        HypervisorLaunchType, "off", StringComparison.OrdinalIgnoreCase);

    public bool RegistryOff =>
        EnableVirtualizationBasedSecurity == 0 &&
        RequirePlatformSecurityFeatures == 0 &&
        HvciEnabled == 0 &&
        KernelShadowStacksEnabled == 0 &&
        CredentialGuardEnabled == 0 &&
        LsaCfgFlags == 0;

    /// <summary>Configuração completa para deixar o VBS desligado após reiniciar.</summary>
    public bool ConfiguredOff => BcdOff && RegistryOff;

    /// <summary>Já desligou no registro, mas o Windows ainda está rodando com VBS.</summary>
    public bool NeedsReboot => Running && ConfiguredOff;

    /// <summary>Verdadeiro quando ainda há algo para desligar.</summary>
    public bool IsEnabled => !ConfiguredOff;
}
