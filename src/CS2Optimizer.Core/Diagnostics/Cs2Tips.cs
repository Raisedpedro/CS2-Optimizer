using System.Text;
using CS2Optimizer.Core.Models;

namespace CS2Optimizer.Core.Diagnostics;

public static class Cs2Tips
{
    public static IReadOnlyList<string> All { get; } = new[]
    {
        "Vídeo: TELA CHEIA (em modo janela, principalmente em 4:3, a imagem fica embaçada); resolução nativa ou a mesma do Windows; taxa de atualização no máximo.",
        "Configurações > Jogo: ative \"Pressionar para falar simplificado\" se usa push-to-talk. Desative avatares animados, se a sua versão do CS2 tiver essa opção.",
        "Max. FPS (fps_max): o padrão do CS2 é 400. Deixe em 0 (sem limite) a menos que queira reduzir consumo/temperatura; limitar abaixo do seu FPS típico só piora a fluidez.",
        "NVIDIA Reflex: \"Enable\" já basta. \"Enable + Boost\" só ajuda quando a CPU é o limite (GPU abaixo de ~100%); com a GPU em 99% não muda o FPS e só gasta mais energia.",
        "Aumentar contraste dos jogadores: opcional (dá contorno em jogadores distantes), mas custa FPS.",
        "Antialiasing: CMAA2 em Full HD nativo; MSAA 4x em resoluções baixas (ex.: 1280x960 4:3), onde há poucos pixels.",
        "Qualidade de sombras: Baixa. Detalhe de texturas: Alto se sobrar VRAM. Sombras dinâmicas: Todas (vantagem de ver a sombra antes) ou só Sol para FPS máximo.",
        "Shaders: Baixo | Partículas: Baixo | Oclusão de ambiente: Desativada | HDR: Qualidade (Desempenho deixa a imagem granulada) | FidelityFX Super Resolution: SEMPRE desativado.",
        "Para saber se a GPU limita: teste na sua resolução e na menor possível; se o FPS subir muito, a GPU era o limite.",
        "Engasgos mesmo com FPS alto? Teste com G-Sync: G-Sync ligado (janela e tela cheia), V-Sync LIGADO no perfil do CS2 no painel NVIDIA, Reflex ligado e V-Sync DESLIGADO no jogo. Pode somar input lag; se preferir FPS solto, mantenha sem.",
        "Cache de shaders: NÃO limpe por rotina - apagar faz o CS2 recompilar e travar de novo. Limpe só depois de trocar o driver de vídeo ou se aparecerem falhas gráficas.",
        "Diagnóstico: PresentMon -> o jogo deve estar em \"Hardware: Independent Flip\". MSI Afterburner -> veja se o Power Limit da GPU bate (se sim, undervolt resolve)."
    };

    public static string BuildChecklist(IReadOnlyList<string> manual, bool needReboot)
    {
        var sb = new StringBuilder();
        sb.AppendLine("CHECKLIST MANUAL - o script não consegue fazer/verificar");
        sb.AppendLine();
        if (manual.Count == 0) sb.AppendLine("(nada pendente)");
        foreach (var item in manual) sb.AppendLine("  [ ] " + item);
        sb.AppendLine();
        sb.AppendLine("DENTRO DO CS2");
        sb.AppendLine();
        foreach (var tip in All) sb.AppendLine("  - " + tip);
        if (needReboot)
        {
            sb.AppendLine();
            sb.AppendLine("REINICIE O PC para concluir as alterações (BCD / MPO / HAGS / mouse).");
        }
        return sb.ToString();
    }

    public static string BuildReport(IReadOnlyList<DiagnosticEntry> entries, IReadOnlyList<string> manual)
    {
        var sb = new StringBuilder();
        foreach (var entry in entries)
            sb.AppendLine($"[{entry.Kind.ToString().ToLowerInvariant()}] {entry.Text}");
        sb.AppendLine();
        sb.AppendLine("CHECKLIST MANUAL");
        foreach (var item in manual) sb.AppendLine(item);
        return sb.ToString();
    }
}
