using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ByesLauncher.Models;
using ByesLauncher.Services;

namespace ByesLauncher.ViewModels;

public partial class ProfilesViewModel : ObservableObject
{
    private readonly ProfileService _profiles;
    private readonly ConfigService _config;
    private readonly ModpackService _modpacks;

    [ObservableProperty] private ObservableCollection<ProfileInfo> items = new();
    [ObservableProperty] private ProfileInfo? selected;
    [ObservableProperty] private string newProfileName = "";
    [ObservableProperty] private ObservableCollection<ModToggle> availableMods = new();

    public ProfilesViewModel(ProfileService profiles, ConfigService config, ModpackService modpacks)
    {
        _profiles = profiles;
        _config = config;
        _modpacks = modpacks;
        Refresh();
    }

    partial void OnSelectedChanged(ProfileInfo? value)
    {
        if (value == null) { AvailableMods.Clear(); return; }
        _config.Config.ActiveProfile = value.Name;
        _config.Save();
        RebuildModToggles();
    }

    [RelayCommand]
    private void Refresh()
    {
        Items.Clear();
        foreach (var p in _profiles.List())
        {
            if (p.Name == _config.Config.ActiveProfile) p.IsDefault = true;
            Items.Add(p);
        }
        Selected ??= Items.FirstOrDefault(i => i.IsDefault) ?? Items.FirstOrDefault();
        RebuildModToggles();
    }

    [RelayCommand]
    private void Create()
    {
        if (string.IsNullOrWhiteSpace(NewProfileName)) return;
        var p = _profiles.Create(NewProfileName);
        Items.Add(p);
        Selected = p;
        NewProfileName = "";
    }

    [RelayCommand]
    private void Delete()
    {
        if (Selected == null) return;
        if (MessageBox.Show($"Delete profile '{Selected.Name}'?", "Confirm", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        _profiles.Delete(Selected);
        Items.Remove(Selected);
        Selected = Items.FirstOrDefault();
    }

    private void RebuildModToggles()
    {
        AvailableMods.Clear();
        if (Selected == null) return;
        var active = _config.Config.ProfileModOverrides.TryGetValue(Selected.Name, out var list) ? list : new();
        foreach (var mod in _modpacks.DiscoverInstalledMods())
        {
            var toggle = new ModToggle(mod, active.Contains(mod, StringComparer.OrdinalIgnoreCase));
            toggle.PropertyChanged += (_, _) => PersistToggles();
            AvailableMods.Add(toggle);
        }
    }

    private void PersistToggles()
    {
        if (Selected == null) return;
        var enabled = AvailableMods.Where(m => m.Enabled).Select(m => m.Name).ToList();
        _config.Config.ProfileModOverrides[Selected.Name] = enabled;
        _config.Save();
    }
}

public partial class ModToggle : ObservableObject
{
    public string Name { get; }
    [ObservableProperty] private bool enabled;
    public ModToggle(string name, bool enabled) { Name = name; this.enabled = enabled; }
}
