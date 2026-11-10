using System.Windows;
using CS2Optimizer.App.ViewModels;

namespace CS2Optimizer.App.Dialogs;

public partial class MessageDialog : Window
{
    public MessageDialog(string title, string message, bool error = false)
    {
        InitializeComponent();
        Title = title;
        TitleText.Text = title;
        MessageText.Text = message;
        if (error) TitleText.Foreground = UiBrushes.Risky;
    }

    private void Ok_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
