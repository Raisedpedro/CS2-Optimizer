using System;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace CS2Optimizer.App;

/// <summary>
/// Tamanho e posição da janela principal, lembrados entre sessões.
/// O arquivo fica junto do programa (janela.json), para o app ser portátil.
/// </summary>
internal sealed class WindowPlacement
{
    public double Left { get; set; }
    public double Top { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }

    private static string FilePath => AppPaths.WindowPlacementFile;

    /// <summary>Lê o tamanho guardado; qualquer problema devolve null e a janela abre no padrão.</summary>
    public static WindowPlacement? Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return null;

            var placement = JsonSerializer.Deserialize<WindowPlacement>(File.ReadAllText(FilePath));
            if (placement is null) return null;
            if (!double.IsFinite(placement.Left) || !double.IsFinite(placement.Top) ||
                !double.IsFinite(placement.Width) || !double.IsFinite(placement.Height)) return null;
            if (placement.Width < 320 || placement.Height < 240) return null;

            return placement;
        }
        catch
        {
            // Arquivo corrompido ou sem acesso não pode impedir o programa de abrir.
            return null;
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this));
        }
        catch
        {
            // Sem permissão de escrita: segue sem lembrar o tamanho.
        }
    }

    /// <summary>Tira a medida da janela; se estiver maximizada, usa o retângulo de restauração.</summary>
    public static WindowPlacement From(Window window)
    {
        var bounds = window.WindowState == WindowState.Normal
            ? new Rect(window.Left, window.Top, window.Width, window.Height)
            : window.RestoreBounds;

        return new WindowPlacement
        {
            Left = bounds.Left,
            Top = bounds.Top,
            Width = bounds.Width,
            Height = bounds.Height
        };
    }

    /// <summary>
    /// Ajusta a medida lembrada para caber na tela atual (troca de monitor, resolução
    /// menor) e mantém uma faixa da barra de título sempre alcançável.
    /// </summary>
    public WindowPlacement Clamp(double minWidth, double minHeight)
    {
        var screenLeft = SystemParameters.VirtualScreenLeft;
        var screenTop = SystemParameters.VirtualScreenTop;
        var screenRight = screenLeft + SystemParameters.VirtualScreenWidth;
        var screenBottom = screenTop + SystemParameters.VirtualScreenHeight;

        var width = Math.Clamp(Width, minWidth, Math.Max(minWidth, screenRight - screenLeft));
        var height = Math.Clamp(Height, minHeight, Math.Max(minHeight, screenBottom - screenTop));
        var left = Math.Clamp(Left, screenLeft - width + 160, screenRight - 160);
        var top = Math.Clamp(Top, screenTop, screenBottom - 80);

        return new WindowPlacement { Left = left, Top = top, Width = width, Height = height };
    }
}
