using System.Windows;

namespace ByesLauncher.Views.Dialogs;

public partial class PasswordPromptDialog
{
    public string ServerName { get; }
    public string? Password { get; private set; }

    public PasswordPromptDialog(string serverName)
    {
        InitializeComponent();
        ServerName = serverName;
        DataContext = this;
        Loaded += (_, _) => PwBox.Focus();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        Password = PwBox.Password;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
