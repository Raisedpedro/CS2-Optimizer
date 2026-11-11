using System;
using System.IO;
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
    /// O WPF não resolve fontes embutidas por pack URI de forma confiável (caía no
    /// Segoe UI sem avisar), então os arquivos são copiados para
    /// <c>%LOCALAPPDATA%\CS2-Optimizer\fonts</c> e lidos dali - nada é instalado no
    /// sistema e o usuário não precisa ter a fonte.
    /// </summary>
    private static FontFamily? TryLoadBundledFont()
    {
        try
        {
            var pasta = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CS2-Optimizer", "fonts");
            Directory.CreateDirectory(pasta);

            var arquivos = new[] { "Inter-Regular.otf", "Inter-Medium.otf", "Inter-SemiBold.otf", "Inter-Bold.otf" };
            foreach (var nome in arquivos)
            {
                var destino = Path.Combine(pasta, nome);
                if (File.Exists(destino) && new FileInfo(destino).Length > 0) continue;

                var recurso = Application.GetResourceStream(new Uri($"Assets/Fonts/{nome}", UriKind.Relative));
                if (recurso is null) continue;

                using var origem = recurso.Stream;
                using var copia = File.Create(destino);
                origem.CopyTo(copia);
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
