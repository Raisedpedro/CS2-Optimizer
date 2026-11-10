using System.Windows;
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
