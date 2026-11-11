using System.Windows;
using CS2Optimizer.App.ViewModels;
using CS2Optimizer.Core.Models;

namespace CS2Optimizer.App.Dialogs;

public partial class BatchConfirmWindow : Window
{
    public BatchConfirmWindow(IReadOnlyList<FixDefinition> fixes, bool willBackup = true)
    {
        InitializeComponent();
        Fixes = fixes.Select(f => new FixItemViewModel(f)).ToArray();
        DataContext = this;

        // Limpeza de cache não grava registro/BCD: prometer backup ali seria mentira.
        if (!willBackup && FindName("BackupNote") is System.Windows.Controls.TextBlock nota)
            nota.Text = "Só caches: nada de registro ou BCD é alterado, então o programa não cria backup.";

        Loaded += (_, _) => CancelButtonFocus();
    }

    public IReadOnlyList<FixItemViewModel> Fixes { get; }
    public bool Confirmed { get; private set; }

    private void CancelButtonFocus()
    {
        // O foco começa em "Cancelar" por segurança; o usuário precisa escolher aplicar.
        var cancel = FindName("CancelButton") as FrameworkElement;
        cancel?.Focus();
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        Confirmed = true;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Confirmed = false;
        DialogResult = false;
    }
}
