using System.Net.Http;
using System.Windows;
using McModsAdder.Providers;
using McModsAdder.Services;
using McModsAdder.ViewModels;
using McModsAdder.Views;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui.Appearance;

namespace McModsAdder;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;
    private static Mutex? _singleInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        _singleInstanceMutex = new Mutex(true, "McModsAdder.SingleInstance", out var createdNew);
        if (!createdNew)
        {
            Shutdown();
            return;
        }

        base.OnStartup(e);

        // 全局异常日志，便于诊断崩溃
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            WriteCrashLog(args.ExceptionObject as Exception);
        DispatcherUnhandledException += (_, args) =>
        {
            WriteCrashLog(args.Exception);
            args.Handled = true;
            MessageBox.Show($"发生错误：{args.Exception.Message}\n详情已写入 crash.log",
                "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        };

        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        Services.GetRequiredService<SettingsService>().Load();

        // 将 Fluent 强调色设为品牌绿（草方块绿）
        ApplicationAccentColorManager.Apply(
            System.Windows.Media.Color.FromRgb(0x3B, 0xA5, 0x5D),
            ApplicationTheme.Dark);

        var mainWindow = Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // 基础服务
        services.AddSingleton<SettingsService>();
        services.AddSingleton<AppState>();
        services.AddSingleton<NavigationService>();
        services.AddSingleton<InstanceScanner>();
        services.AddSingleton<ProfileService>();

        // Mod 来源（二期可在此追加 CurseForgeProvider）
        services.AddSingleton(new HttpClient());
        services.AddSingleton<IModProvider, ModrinthProvider>();

        services.AddSingleton<ModJarAnalyzer>();
        services.AddSingleton<ModInstaller>();

        // ViewModels
        services.AddTransient<InstancesViewModel>();
        services.AddTransient<InstanceDetailViewModel>();
        services.AddTransient<ProfilesViewModel>();
        services.AddTransient<ProfileEditorViewModel>();
        services.AddTransient<ModDetailViewModel>();
        services.AddTransient<InstallViewModel>();
        services.AddTransient<SettingsViewModel>();

        // Pages
        services.AddTransient<InstancesPage>();
        services.AddTransient<InstanceDetailPage>();
        services.AddTransient<ProfilesPage>();
        services.AddTransient<ProfileEditorPage>();
        services.AddTransient<InstallPage>();
        services.AddTransient<SettingsPage>();

        services.AddTransient<MainWindow>();
    }

    private static void WriteCrashLog(Exception? ex)
    {
        try
        {
            System.IO.Directory.CreateDirectory(SettingsService.DataDir);
            System.IO.File.AppendAllText(
                System.IO.Path.Combine(SettingsService.DataDir, "crash.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\n\n");
        }
        catch
        {
            // 日志失败不再抛出
        }
    }
}
