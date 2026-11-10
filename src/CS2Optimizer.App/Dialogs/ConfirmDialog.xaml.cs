using System.Windows;

namespace CS2Optimizer.App.Dialogs;

public partial class ConfirmDialog : Window
{
    public ConfirmDialog(string title, string message, string yesText = "Sim",
        string noText = "Não", string? cancelText = null, bool defaultNo = true)
    {
        InitializeComponent();
        Title = title;
        TitleText.Text = title;
        MessageText.Text = message;
        YesButton.Content = yesText;
        NoButton.Content = noText;

        if (string.IsNullOrWhiteSpace(cancelText))
            CancelButton.Visibility = Visibility.Collapsed;
        else
            CancelButton.Content = cancelText;

        Loaded += (_, _) =>
        {
            if (defaultNo) NoButton.Focus();
            else YesButton.Focus();
        };
    }

    public bool? Result { get; private set; }

    private void Yes_Click(object sender, RoutedEventArgs e)
    {
        Result = true;
        DialogResult = true;
    }

    private void No_Click(object sender, RoutedEventArgs e)
    {
        Result = false;
        DialogResult = false;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Result = null;
        DialogResult = false;
    }
}
