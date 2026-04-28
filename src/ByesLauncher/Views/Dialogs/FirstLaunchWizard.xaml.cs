using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using ByesLauncher.Services;

namespace ByesLauncher.Views.Dialogs;

public partial class FirstLaunchWizard
{
    private readonly ConfigService _config;
    private readonly ProfileService _profiles;

    public FirstLaunchWizard()
    {
        InitializeComponent();
        _config   = App.Services.GetRequiredService<ConfigService>();
        _profiles = App.Services.GetRequiredService<ProfileService>();

        // Pre-fill with auto-detected path
        ArmaPathBox.Text = _config.Config.Arma2OaPath;
        UpdatePathStatus();
        ArmaPathBox.TextChanged += (_, _) => UpdatePathStatus();

        // Profile list
        var profiles = _profiles.List();
        foreach (var p in profiles) ProfileList.Items.Add(p.Name);
        if (profiles.Count > 0) ProfileList.SelectedIndex = 0;
        else { CreateNew.IsChecked = true; UseExisting.IsEnabled = false; }
    }

    private void UpdatePathStatus()
    {
        var p = ArmaPathBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(p))
        {
            PathStatus.Text = "Pick the folder that contains arma2oa.exe.";
            NextBtn.IsEnabled = false;
            return;
        }
        var exe1 = Path.Combine(p, "arma2oa.exe");
        var exe2 = Path.Combine(p, "ArmA2OA.exe");
        if (File.Exists(exe1) || File.Exists(exe2))
        {
            PathStatus.Text = $"✓ Looks good. Found Arma 2 executable at:\n   {(File.Exists(exe1) ? exe1 : exe2)}";
            NextBtn.IsEnabled = true;
        }
        else
        {
            PathStatus.Text = "× No arma2oa.exe in that folder. Pick the install root, e.g.:\n   C:\\Program Files (x86)\\Steam\\steamapps\\common\\Arma 2 Operation Arrowhead";
            NextBtn.IsEnabled = false;
        }
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Arma 2 executable|arma2oa.exe;ArmA2OA.exe;arma2oa_be.exe",
            Title  = "Locate arma2oa.exe",
        };
        if (dlg.ShowDialog() == true)
            ArmaPathBox.Text = Path.GetDirectoryName(dlg.FileName) ?? "";
    }

    private bool _onPage2;

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        if (!_onPage2)
        {
            _config.Config.Arma2OaPath = ArmaPathBox.Text.Trim();
            _config.Save();

            PageWelcome.Visibility = Visibility.Collapsed;
            PageProfile.Visibility = Visibility.Visible;
            BackBtn.IsEnabled = true;
            NextBtn.Content = "Finish";
            _onPage2 = true;
            return;
        }

        // Finish — commit profile choice
        string? chosen = null;
        if (CreateNew.IsChecked == true)
        {
            var name = NewProfileBox.Text?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Enter a profile name.", "First launch", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var p = _profiles.Create(name);
            chosen = p.Name;
        }
        else if (ProfileList.SelectedItem is string s)
        {
            chosen = s;
        }
        if (!string.IsNullOrEmpty(chosen)) _config.Config.ActiveProfile = chosen;
        _config.Config.FirstLaunchCompleted = true;
        _config.Save();
        DialogResult = true;
        Close();
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        PageWelcome.Visibility = Visibility.Visible;
        PageProfile.Visibility = Visibility.Collapsed;
        BackBtn.IsEnabled = false;
        NextBtn.Content = "Next";
        _onPage2 = false;
    }
}
