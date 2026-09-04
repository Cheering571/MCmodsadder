using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MCModPlus.Models;
using MCModPlus.Providers;
using MCModPlus.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MCModPlus.ViewModels;

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

    [ObservableProperty]
    private string _curseForgeApiKey = string.Empty;

    [ObservableProperty]
    private bool _isCurseForgeApiEditing;

    [ObservableProperty]
    private bool _isCurseForgeApiClearPending;

    public string VersionText => "MCMod++ v1.1.0";

    public string DataDirText => SettingsService.DataDir;

    public SettingsViewModel(SettingsService settings)
    {
        _settings = settings;
        _useMirror = settings.Current.UseMirror;
        _downloadConcurrency = settings.Current.DownloadConcurrency;
        _backupEnabled = settings.Current.BackupEnabled;
        _autoInstallDependencies = settings.Current.AutoInstallDependencies;
        _curseForgeApiKey = settings.Current.CurseForgeApiKey;
    }

    public string CurseForgeApiPlaceholder => "默认使用内置api，有需求请手动修改";

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

    [RelayCommand]
    private void ToggleCurseForgeApiEdit()
    {
        if (IsCurseForgeApiEditing)
        {
            CurseForgeApiKey = CurseForgeApiKey.Trim();
            SaveCurseForgeApiKey(CurseForgeApiKey);
            IsCurseForgeApiEditing = false;
            IsCurseForgeApiClearPending = false;
            return;
        }

        IsCurseForgeApiClearPending = false;
        CurseForgeApiKey = _settings.Current.CurseForgeApiKey;
        IsCurseForgeApiEditing = true;
    }

    [RelayCommand]
    private void ClearCurseForgeApi()
    {
        if (!IsCurseForgeApiClearPending)
        {
            IsCurseForgeApiEditing = false;
            IsCurseForgeApiClearPending = true;
            return;
        }

        CurseForgeApiKey = string.Empty;
        SaveCurseForgeApiKey(CurseForgeApiKey);
        IsCurseForgeApiClearPending = false;
        IsCurseForgeApiEditing = false;
    }

    public void CancelPendingCurseForgeAction()
    {
        IsCurseForgeApiClearPending = false;
        if (IsCurseForgeApiEditing)
        {
            CurseForgeApiKey = _settings.Current.CurseForgeApiKey;
            IsCurseForgeApiEditing = false;
        }
    }

    private void SaveCurseForgeApiKey(string key)
    {
        _settings.Current.CurseForgeApiKey = key;
        _settings.Save();
        App.Services.GetRequiredService<CurseForgeProvider>().SetApiKey(key);
    }
}
