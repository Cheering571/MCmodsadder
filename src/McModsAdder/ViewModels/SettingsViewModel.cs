using CommunityToolkit.Mvvm.ComponentModel;
using McModsAdder.Services;

namespace McModsAdder.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settings;

    [ObservableProperty]
    private bool _useMirror;

    [ObservableProperty]
    private double _downloadConcurrency;

    [ObservableProperty]
    private bool _backupEnabled;

    [ObservableProperty]
    private bool _autoInstallDependencies;

    public string VersionText => "McModsAdder v1.0.0";

    public string DataDirText => SettingsService.DataDir;

    public SettingsViewModel(SettingsService settings)
    {
        _settings = settings;
        _useMirror = settings.Current.UseMirror;
        _downloadConcurrency = settings.Current.DownloadConcurrency;
        _backupEnabled = settings.Current.BackupEnabled;
        _autoInstallDependencies = settings.Current.AutoInstallDependencies;
    }

    partial void OnUseMirrorChanged(bool value)
    {
        _settings.Current.UseMirror = value;
        _settings.Save();
    }

    partial void OnDownloadConcurrencyChanged(double value)
    {
        _settings.Current.DownloadConcurrency = Math.Clamp((int)value, 1, 16);
        _settings.Save();
    }

    partial void OnBackupEnabledChanged(bool value)
    {
        _settings.Current.BackupEnabled = value;
        _settings.Save();
    }

    partial void OnAutoInstallDependenciesChanged(bool value)
    {
        _settings.Current.AutoInstallDependencies = value;
        _settings.Save();
    }
}
