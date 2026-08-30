using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using McModsAdder.Models;
using McModsAdder.Services;
using McModsAdder.Views;
using Microsoft.Win32;

namespace McModsAdder.ViewModels;

public partial class ProfilesViewModel : ObservableObject
{
    private readonly ProfileService _profileService;
    private readonly AppState _appState;
    private readonly NavigationService _nav;

    [ObservableProperty]
    private ObservableCollection<ModProfile> _profiles = new();

    [ObservableProperty]
    private bool _hasProfiles;

    [ObservableProperty]
    private string _messageText = string.Empty;

    public ProfilesViewModel(ProfileService profileService, AppState appState, NavigationService nav)
    {
        _profileService = profileService;
        _appState = appState;
        _nav = nav;
    }

    public void LoadData()
    {
        _profileService.LoadAll();
        Profiles = new ObservableCollection<ModProfile>(_profileService.Profiles);
        HasProfiles = Profiles.Count > 0;
    }

    [RelayCommand]
    private void Create()
    {
        var dialog = new InputDialog("新建配置表", "配置表名称：", "例如：我的常用辅助 mod");
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.InputText))
        {
            var profile = _profileService.Create(dialog.InputText.Trim());
            LoadData();
            OpenEditor(profile);
        }
    }

    [RelayCommand]
    private void Edit(ModProfile profile) => OpenEditor(profile);

    private void OpenEditor(ModProfile profile)
    {
        _appState.CurrentProfile = profile;
        _nav.Navigate<ProfileEditorPage>();
    }

    [RelayCommand]
    private void Rename(ModProfile profile)
    {
        var dialog = new InputDialog("重命名配置表", "新名称：", profile.Name);
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.InputText))
        {
            profile.Name = dialog.InputText.Trim();
            _profileService.Save(profile);
            LoadData();
        }
    }

    [RelayCommand]
    private void Delete(ModProfile profile)
    {
        var result = MessageBox.Show(
            $"确定删除配置表「{profile.Name}」吗？此操作不可恢复。",
            "删除确认", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result == MessageBoxResult.Yes)
        {
            _profileService.Delete(profile);
            LoadData();
        }
    }

    [RelayCommand]
    private void Export(ModProfile profile)
    {
        var dialog = new SaveFileDialog
        {
            Title = "导出配置表",
            FileName = profile.Name + ".json",
            Filter = "配置表文件 (*.json)|*.json"
        };
        if (dialog.ShowDialog() == true)
        {
            try
            {
                _profileService.Export(profile, dialog.FileName);
                MessageText = $"已导出到 {dialog.FileName}";
            }
            catch (Exception ex)
            {
                MessageText = $"导出失败：{ex.Message}";
            }
        }
    }

    [RelayCommand]
    private void Import()
    {
        var dialog = new OpenFileDialog
        {
            Title = "导入配置表",
            Filter = "配置表文件 (*.json)|*.json"
        };
        if (dialog.ShowDialog() == true)
        {
            var profile = _profileService.Import(dialog.FileName);
            if (profile != null)
            {
                LoadData();
                MessageText = $"已导入配置表「{profile.Name}」（{profile.Entries.Count} 个 mod）";
            }
            else
            {
                MessageText = "导入失败：文件格式不正确";
            }
        }
    }
}
