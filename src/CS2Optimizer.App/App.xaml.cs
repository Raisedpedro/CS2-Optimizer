using System;
using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Media;
using CS2Optimizer.Core.Cli;
using CS2Optimizer.Core.Engine;

namespace CS2Optimizer.App;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            var services = AppServices.Create();

            // A fonte embutida (Inter) não é instalada no Windows: se o pack URI do
            // XAML não resolver, o WPF cai no Segoe calado. Aqui ela é carregada das
            // próprias resources do assembly e registrada como AppFont.
            var fonteDoApp = TryLoadBundledFont();

            if (e.Args.Length > 0)
            {
                // Modo CLI: não abre a janela.
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                var runner = new ConsoleRunner(services.Engine);
                var code = await runner.RunAsync(e.Args);
                Shutdown(code);
                return;
            }

            var window = new MainWindow();
            if (fonteDoApp is not null) window.FontFamily = fonteDoApp;
            var interaction = new Dialogs.WpfInteraction();
            var viewModel = new ViewModels.MainViewModel(services, interaction);
            window.DataContext = viewModel;
            MainWindow = window;
            window.Show();

            await viewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            TryWriteStartupError(ex);
            MessageBox.Show("Falha ao iniciar o CS2 / Windows Optimizer:\n\n" + ex,
                "CS2 / Windows Optimizer", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    /// <summary>
    /// Carrega a fonte embutida (Inter) e a registra como <c>AppFont</c>.
    /// O WPF não resolve fontes embutidas por pack URI de forma confiável, então
    /// os arquivos são copiados para <c>%LOCALAPPDATA%\CS2-Optimizer\fonts</c>.
    /// Como o app roda elevado, a pasta é gravável: os arquivos são SEMPRE
    /// reescritos a partir do recurso e conferidos por hash antes de serem
    /// usados — um OTF plantado ali nunca é carregado.
    /// </summary>
    private static FontFamily? TryLoadBundledFont()
    {
        var esperados = new (string Nome, string Hash)[]
        {
            ("Inter-Regular.otf", "D4F2B9E148059A15F014CB0F0B8FEA8CD11BFA447DD483BEDF1B0ADC0E2BA799"),
            ("Inter-Medium.otf", "6C222ED2C3660524DD0C14FB745E00FEE65C5361AB28F60FBA6730BF4E648763"),
            ("Inter-SemiBold.otf", "0A4D30778FA2DC239368D90FD854B88B27E7EDE737450DA72686A90ECA66F02C"),
            ("Inter-Bold.otf", "9FC6261E817D0B1B2BF1953D8799BB1C837D11AEEBBF83AE352E7B4D6DE6425D")
        };

        try
        {
            var pasta = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CS2-Optimizer", "fonts");
            Directory.CreateDirectory(pasta);

            foreach (var (nome, hash) in esperados)
            {
                var recurso = Application.GetResourceStream(new Uri($"Assets/Fonts/{nome}", UriKind.Relative));
                if (recurso is null) return null;
                var destino = Path.Combine(pasta, nome);

                using var origem = recurso.Stream;
                using var memory = new MemoryStream();
                origem.CopyTo(memory);
                var bytes = memory.ToArray();
                var atual = Convert.ToHexString(SHA256.HashData(bytes));
                if (!string.Equals(atual, hash, StringComparison.OrdinalIgnoreCase)) return null;

                File.WriteAllBytes(destino, bytes);
            }

            foreach (FontFamily familia in Fonts.GetFontFamilies(pasta + Path.DirectorySeparatorChar))
            {
                if (familia.Source.IndexOf("Inter", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Current.Resources["AppFont"] = familia;
                    return familia;
                }
            }
        }
        catch
        {
            // Sem a fonte o app abre normalmente com a fonte do sistema.
        }

        return null;
    }

    private static void TryWriteStartupError(Exception ex)
    {
        try
        {
            System.IO.File.WriteAllText(
                System.IO.Path.Combine(AppPaths.BaseDirectory, "startup-error.txt"),
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine + ex);
        }
        catch
        {
            // Falha ao gravar diagnóstico não deve esconder o erro original.
        }
    }
}
