using System.IO;
using System.Text;
using System.Windows;
using CS2Optimizer.Core.Engine;
using CS2Optimizer.Core.Models;
using Microsoft.Win32;

namespace CS2Optimizer.App.Dialogs;

public sealed class WpfInteraction : IAppInteraction
{
    private static Window? Owner =>
        Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
        ?? Application.Current?.MainWindow;

    public bool Confirm(string question, FixLevel level)
    {
        return OnUi(() =>
        {
            var defaultNo = level is FixLevel.Risky or FixLevel.Destructive;
            var dialog = new ConfirmDialog("Confirmar", question, defaultNo: defaultNo) { Owner = Owner };
            dialog.ShowDialog();
            return dialog.Result == true;
        });
    }

    public bool ConfirmBatch(IReadOnlyList<FixDefinition> fixes, bool willBackup)
    {
        if (fixes.Count == 0) return false;
        return OnUi(() =>
        {
            var window = new BatchConfirmWindow(fixes, willBackup) { Owner = Owner };
            window.ShowDialog();
            return window.Confirmed;
        });
    }

    public bool ConfirmRisky(FixDefinition fix)
    {
        var message = new StringBuilder();
        message.AppendLine("ITEM INSEGURO: " + fix.Title);
        message.AppendLine();
        message.AppendLine("Estado atual: " + fix.CurrentState);
        message.AppendLine();
        message.AppendLine(fix.Explanation);
        message.AppendLine();
        message.AppendLine("Este item NÃO é recomendado. Ele pode reduzir a segurança ou apagar dados.");
        message.AppendLine("A opção segura é responder \"Não\".");

        return OnUi(() =>
        {
            var dialog = new ConfirmDialog("Item inseguro", message.ToString(),
                yesText: "Sim, aplicar mesmo assim", noText: "Não (recomendado)",
                defaultNo: true) { Owner = Owner };
            dialog.ShowDialog();
            return dialog.Result == true;
        });
    }

    public RestorePointChoice AskRestorePoint()
    {
        return OnUi(() =>
        {
            var window = new RestorePointWindow { Owner = Owner };
            window.ShowDialog();
            return window.Choice;
        });
    }

    public bool ConfirmRestoreBackup(string path)
    {
        var message =
            "Restaurar o backup abaixo?\n\n" + path + "\n\n" +
            "O programa devolve os valores anteriores do registro (só o que ele mesmo alterou), " +
            "restaura o BCD e reativa o plano de energia original. " +
            "Reinicie o PC depois. Deseja continuar?";
        return OnUi(() =>
        {
            var dialog = new ConfirmDialog("Restaurar backup", message, defaultNo: true) { Owner = Owner };
            dialog.ShowDialog();
            return dialog.Result == true;
        });
    }

    public void ShowMessage(string title, string message, bool error = false)
    {
        OnUi(() =>
        {
            var dialog = new MessageDialog(title, message, error) { Owner = Owner };
            dialog.ShowDialog();
        });
    }

    public string? PickFolder(string title, string? initialDirectory = null)
    {
        return OnUi(() =>
        {
            var dialog = new OpenFolderDialog
            {
                Title = title,
                InitialDirectory = !string.IsNullOrWhiteSpace(initialDirectory) &&
                                   Directory.Exists(initialDirectory)
                    ? initialDirectory
                    : "",
                Multiselect = false
            };
            return dialog.ShowDialog(Owner) == true ? dialog.FolderName : null;
        });
    }

    public string? SaveFile(string title, string fileName, string filter, string defaultExtension)
    {
        return OnUi(() =>
        {
            var dialog = new SaveFileDialog
            {
                Title = title,
                FileName = fileName,
                Filter = filter,
                DefaultExt = defaultExtension,
                AddExtension = true,
                OverwritePrompt = true
            };
            return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
        });
    }

    private static void OnUi(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess()) action();
        else dispatcher.Invoke(action);
    }

    private static T OnUi<T>(Func<T> func)
    {
        var dispatcher = Application.Current?.Dispatcher;
        return dispatcher is null || dispatcher.CheckAccess() ? func() : dispatcher.Invoke(func);
    }
}
