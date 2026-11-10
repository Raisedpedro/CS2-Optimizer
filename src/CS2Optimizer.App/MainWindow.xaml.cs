using System;
using System.ComponentModel;
using System.Windows;

namespace CS2Optimizer.App;

public partial class MainWindow : Window
{
    private const double DesiredWidth = 1200;
    private const double DesiredHeight = 800;

    public MainWindow()
    {
        InitializeComponent();
        ApplyPlacement();
        Closing += OnClosing;
    }

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
