using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace CS2Optimizer.App;

public partial class MainWindow : Window
{
    private const double DesiredWidth = 1200;
    private const double DesiredHeight = 800;

    public MainWindow()
    {
        InitializeComponent();
        ApplyPlacement();
        SourceInitialized += (_, _) => ApplyDarkTitleBar();
        Closing += OnClosing;
    }

    /// <summary>
    /// Pinta a barra de título nativa de escuro. Sem isso a janela abre com a barra
    /// clara do Windows, que destoa do resto. No Windows 11 dá para usar a cor exata
    /// do app; em versões antigas fica só a barra escura padrão.
    /// </summary>
    private void ApplyDarkTitleBar()
    {
        try
        {
            var handle = new WindowInteropHelper(this).Handle;
            if (handle == IntPtr.Zero) return;

            var ligado = 1;
            _ = DwmSetWindowAttribute(handle, 20, ref ligado, sizeof(int)); // DWMWA_USE_IMMERSIVE_DARK_MODE (20H1+)
            _ = DwmSetWindowAttribute(handle, 19, ref ligado, sizeof(int)); // mesmo atributo em builds antigos

            var titulo = ToColorRef(0x0F, 0x0F, 0x16);   // igual ao SurfaceBrush
            _ = DwmSetWindowAttribute(handle, 35, ref titulo, sizeof(int)); // DWMWA_CAPTION_COLOR

            var texto = ToColorRef(0xE4, 0xE4, 0xE7);    // igual ao TextBrush
            _ = DwmSetWindowAttribute(handle, 36, ref texto, sizeof(int)); // DWMWA_TEXT_COLOR

            var borda = ToColorRef(0x1A, 0x1A, 0x22);
            _ = DwmSetWindowAttribute(handle, 34, ref borda, sizeof(int)); // DWMWA_BORDER_COLOR
        }
        catch
        {
            // Windows sem suporte a esses atributos: segue com a barra padrão.
        }
    }

    private static int ToColorRef(byte r, byte g, byte b) => r | (g << 8) | (b << 16);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    /// <summary>
    /// Fechar no meio de um backup/aplicação pode deixar o estado pela metade.
    /// Pergunta antes e, se o usuário desistir, cancela o fechamento.
    /// </summary>
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (DataContext is ViewModels.MainViewModel { IsBusy: true })
        {
            var answer = MessageBox.Show(this,
                "Há uma operação em andamento (backup ou aplicação de ajustes).\n\n" +
                "Fechar agora pode deixar o estado pela metade. Fechar mesmo assim?",
                "CS2 / Windows Optimizer",
                MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);

            if (answer != MessageBoxResult.Yes)
            {
                e.Cancel = true;
                return;
            }
        }

        WindowPlacement.From(this).Save();
    }

    /// <summary>
    /// Abre em janela normal — nada de tela cheia. Lembra só o TAMANHO da sessão
    /// anterior e sempre centraliza na tela principal: o CenterScreen do WPF pode
    /// usar o monitor onde está o mouse e abrir "na esquerda" em multi-monitor.
    /// </summary>
    private void ApplyPlacement()
    {
        var area = SystemParameters.WorkArea;

        // Em telas pequenas o mínimo encolhe junto, senão a janela não caberia.
        MinWidth = Math.Min(MinWidth, Math.Round(area.Width * 0.9));
        MinHeight = Math.Min(MinHeight, Math.Round(area.Height * 0.9));

        var saved = WindowPlacement.Load()?.Clamp(MinWidth, MinHeight);
        if (saved is not null)
        {
            Width = saved.Width;
            Height = saved.Height;
        }
        else
        {
            Width = Math.Max(MinWidth, Math.Min(DesiredWidth, area.Width * 0.94));
            Height = Math.Max(MinHeight, Math.Min(DesiredHeight, area.Height * 0.94));
        }

        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = Math.Round(area.Left + (area.Width - Width) / 2);
        Top = Math.Round(area.Top + (area.Height - Height) / 2);
    }
}
