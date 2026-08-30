using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using McModsAdder.Services;

namespace McModsAdder.ViewModels;

public partial class ModDetailViewModel : ObservableObject
{
    public ModSearchResult Result { get; }

    [ObservableProperty]
    private BitmapImage? _icon;

    public ModDetailViewModel(ModSearchResult result)
    {
        Result = result;
        _ = LoadIconAsync();
    }

    private async Task LoadIconAsync()
    {
        Icon = await ImageLoader.GetAsync(Result.IconUrl);
    }
}
