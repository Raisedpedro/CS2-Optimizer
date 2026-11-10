using System.Windows;
using CS2Optimizer.Core.Engine;

namespace CS2Optimizer.App.Dialogs;

public partial class RestorePointWindow : Window
{
    public RestorePointWindow()
    {
        InitializeComponent();
    }

    public RestorePointChoice Choice { get; private set; } = RestorePointChoice.Cancel;

    private void Yes_Click(object sender, RoutedEventArgs e)
    {
        Choice = RestorePointChoice.Yes;
        DialogResult = true;
    }

    private void No_Click(object sender, RoutedEventArgs e)
    {
        Choice = RestorePointChoice.No;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Choice = RestorePointChoice.Cancel;
        DialogResult = false;
    }
}
