using System.ComponentModel;
using System.Windows;
using WeaselStudio.Models;
using WeaselStudio.Services;
using WeaselStudio.ViewModels;

namespace WeaselStudio;

public partial class App : Application
{
    private SyncSettingsStore? _settingsStore;
    private ClipboardSyncService? _syncService;
    private TrayService? _tray;
    private MainWindow? _mainWindow;

    public bool IsExiting { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(
                args.Exception.Message,
                "程序错误",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        var userDirIndex = Array.IndexOf(e.Args, "--user-dir");
        if (userDirIndex >= 0 && userDirIndex + 1 < e.Args.Length)
        {
            Environment.SetEnvironmentVariable(
                "WEASEL_STUDIO_USER_DIR",
                e.Args[userDirIndex + 1]);
        }

        var startHidden = e.Args.Any(
            arg => string.Equals(arg, "--background", StringComparison.OrdinalIgnoreCase));

        _settingsStore = new SyncSettingsStore();
        _syncService = new ClipboardSyncService();
        _syncService.StatusChanged += OnSyncStatusChanged;

        try
        {
            _tray = new TrayService(
                OpenSettings,
                ToggleSync,
                SyncNow,
                ExitApplication);
        }
        catch
        {
            _tray = null;
        }

        var settings = _settingsStore.Load();
        if (settings.Enabled && !string.IsNullOrWhiteSpace(settings.ServerUrl))
            _syncService.Start(settings);

        _mainWindow = new MainWindow
        {
            DataContext = new MainViewModel(_syncService, _settingsStore)
        };
        _mainWindow.Closing += OnMainWindowClosing;
        MainWindow = _mainWindow;

        _tray?.UpdateStatus(
            _syncService.IsRunning ? "同步运行中" : "同步未启动",
            _syncService.IsRunning);

        if (startHidden && settings.MinimizeToTray && _tray is not null)
        {
            _tray.ShowBalloon("已最小化到托盘运行，双击托盘图标打开设置。");
        }
        else
        {
            _mainWindow.Show();
        }
    }

    private void OnSyncStatusChanged(object? sender, SyncStatusEventArgs e)
    {
        if (Dispatcher.HasShutdownStarted)
            return;

        Dispatcher.BeginInvoke(() =>
        {
            _tray?.UpdateStatus(e.Message, _syncService?.IsRunning ?? false);
        });
    }

    private void OpenSettings()
    {
        if (_mainWindow is null)
            return;

        if (!_mainWindow.IsVisible)
            _mainWindow.Show();

        if (_mainWindow.WindowState == WindowState.Minimized)
            _mainWindow.WindowState = WindowState.Normal;

        _mainWindow.Activate();
    }

    private void ToggleSync()
    {
        if (_syncService is null || _settingsStore is null)
            return;

        if (_syncService.IsRunning)
        {
            _syncService.Stop();
            _tray?.UpdateStatus("同步已暂停", false);
            return;
        }

        var settings = _settingsStore.Load();
        _syncService.Start(settings);
        _tray?.UpdateStatus(
            _syncService.IsRunning ? "同步运行中" : "同步未启动",
            _syncService.IsRunning);
    }

    private void SyncNow()
    {
        _ = _syncService?.PullNowAsync();
    }

    private void ExitApplication()
    {
        IsExiting = true;

        _syncService?.Dispose();
        _syncService = null;

        _tray?.Dispose();
        _tray = null;

        Shutdown();
    }

    private void OnMainWindowClosing(object? sender, CancelEventArgs e)
    {
        if (IsExiting)
            return;

        var settings = _settingsStore?.Load() ?? new SyncSettings();

        if (settings.MinimizeToTray && _tray is not null)
        {
            e.Cancel = true;
            _mainWindow?.Hide();
            _tray.ShowBalloon("小狼毫设置中心仍在后台运行，可从托盘退出。");
            return;
        }

        IsExiting = true;
        _syncService?.Dispose();
        _syncService = null;
        _tray?.Dispose();
        _tray = null;
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        IsExiting = true;

        _syncService?.Dispose();
        _syncService = null;

        _tray?.Dispose();
        _tray = null;

        base.OnExit(e);
    }
}
