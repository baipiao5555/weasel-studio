using System.Windows;
using System.Windows.Input;
using WeaselStudio.Infrastructure;
using WeaselStudio.Models;
using WeaselStudio.Services;

namespace WeaselStudio.ViewModels;

public sealed class ClipboardSyncViewModel : ObservableObject, IReloadable
{
    private readonly ClipboardSyncService _service;
    private readonly SyncSettingsStore _store;
    private readonly Action<string> _setStatus;

    private bool _loading;
    private bool _isDirty;
    private bool _enabled;
    private string _serverUrl = "";
    private string _username = "";
    private string _password = "";
    private int _intervalSeconds = 3;
    private bool _minimizeToTray = true;
    private bool _autoStart;
    private bool _allowPrivateAddresses;
    private bool _isBusy;
    private bool _isRunning;
    private string _statusMessage = "填写服务器地址后保存并启动同步。";
    private string _lastSyncText = "—";

    public ClipboardSyncViewModel(
        ClipboardSyncService service,
        SyncSettingsStore store,
        Action<string> setStatus)
    {
        _service = service;
        _store = store;
        _setStatus = setStatus;

        SaveCommand = new AsyncRelayCommand(SaveAsync);
        TestConnectionCommand = new AsyncRelayCommand(TestConnectionAsync);
        SyncNowCommand = new AsyncRelayCommand(SyncNowAsync);
        ReloadCommand = new RelayCommand(ReloadWithConfirm);

        _service.StatusChanged += OnSyncStatusChanged;
        Reload();
    }

    public bool IsDirty => _isDirty;

    public bool Enabled
    {
        get => _enabled;
        set { if (SetProperty(ref _enabled, value)) MarkDirty(); }
    }

    public string ServerUrl
    {
        get => _serverUrl;
        set { if (SetProperty(ref _serverUrl, value)) MarkDirty(); }
    }

    public string Username
    {
        get => _username;
        set { if (SetProperty(ref _username, value)) MarkDirty(); }
    }

    public string Password
    {
        get => _password;
        set { if (SetProperty(ref _password, value)) MarkDirty(); }
    }

    public int IntervalSeconds
    {
        get => _intervalSeconds;
        set { if (SetProperty(ref _intervalSeconds, value)) MarkDirty(); }
    }

    public bool MinimizeToTray
    {
        get => _minimizeToTray;
        set { if (SetProperty(ref _minimizeToTray, value)) MarkDirty(); }
    }

    public bool AutoStart
    {
        get => _autoStart;
        set { if (SetProperty(ref _autoStart, value)) MarkDirty(); }
    }

    public bool AllowPrivateAddresses
    {
        get => _allowPrivateAddresses;
        set { if (SetProperty(ref _allowPrivateAddresses, value)) MarkDirty(); }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public bool IsRunning
    {
        get => _isRunning;
        private set => SetProperty(ref _isRunning, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string LastSyncText
    {
        get => _lastSyncText;
        private set => SetProperty(ref _lastSyncText, value);
    }

    public ICommand SaveCommand { get; }
    public ICommand TestConnectionCommand { get; }
    public ICommand SyncNowCommand { get; }
    public ICommand ReloadCommand { get; }

    public void Reload()
    {
        _loading = true;

        var settings = _store.Load();
        Enabled = settings.Enabled;
        ServerUrl = settings.ServerUrl;
        Username = settings.Username;
        Password = settings.Password;
        IntervalSeconds = Math.Clamp(settings.IntervalSeconds, 1, 60);
        MinimizeToTray = settings.MinimizeToTray;
        AutoStart = settings.AutoStart;
        AllowPrivateAddresses = settings.AllowPrivateAddresses;

        _loading = false;
        _isDirty = false;
        OnPropertyChanged(nameof(IsDirty));

        IsRunning = _service.IsRunning;
        LastSyncText = _service.LastSyncTime?.ToString("HH:mm:ss") ?? "—";
        StatusMessage = settings.Enabled
            ? "已加载设置。"
            : "同步未启用。";
    }

    private void ReloadWithConfirm()
    {
        if (IsDirty &&
            MessageBox.Show(
                "当前有未保存的修改，重新加载会丢弃它们。继续吗？",
                "重新加载",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        Reload();
    }

    private Task SaveAsync()
    {
        var settings = BuildSettings();

        if (settings.Enabled && string.IsNullOrWhiteSpace(settings.ServerUrl))
        {
            StatusMessage = "启用同步前请填写服务器地址。";
            _setStatus(StatusMessage);
            MessageBox.Show(
                StatusMessage,
                "剪贴板同步",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return Task.CompletedTask;
        }

        try
        {
            _store.Save(settings);
            AutoStartService.SetEnabled(settings.AutoStart);

            var started = false;
            if (settings.Enabled)
                started = _service.Start(settings);
            else
                _service.Stop();

            IsRunning = _service.IsRunning;
            StatusMessage = settings.Enabled
                ? (started ? "设置已保存，同步已启动。" : "设置已保存，但同步启动失败。")
                : "设置已保存，同步已停止。";
            _setStatus(StatusMessage);

            _isDirty = false;
            OnPropertyChanged(nameof(IsDirty));
        }
        catch (Exception ex)
        {
            StatusMessage = "保存失败：" + ex.Message;
            _setStatus(StatusMessage);
            MessageBox.Show(ex.Message, "保存失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        return Task.CompletedTask;
    }

    private async Task TestConnectionAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        StatusMessage = "正在测试连接…";

        try
        {
            await Task.Run(async () =>
            {
                using var client = new SyncClipboardClient(
                    ServerUrl,
                    Username,
                    Password,
                    AllowPrivateAddresses);
                await client.TestConnectionAsync(CancellationToken.None);
            });

            StatusMessage = "连接成功。";
            _setStatus(StatusMessage);
        }
        catch (Exception ex)
        {
            StatusMessage = "连接失败：" + ex.Message;
            _setStatus(StatusMessage);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SyncNowAsync()
    {
        if (!IsRunning)
        {
            StatusMessage = "同步未启动。";
            _setStatus(StatusMessage);
            return;
        }

        IsBusy = true;
        try
        {
            await _service.PullNowAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private SyncSettings BuildSettings() => new()
    {
        Enabled = Enabled,
        ServerUrl = ServerUrl.Trim(),
        Username = Username.Trim(),
        Password = Password,
        IntervalSeconds = Math.Clamp(IntervalSeconds, 1, 60),
        MinimizeToTray = MinimizeToTray,
        AutoStart = AutoStart,
        AllowPrivateAddresses = AllowPrivateAddresses
    };

    private void OnSyncStatusChanged(object? sender, SyncStatusEventArgs e)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.HasShutdownStarted)
            return;

        dispatcher.BeginInvoke(() =>
        {
            StatusMessage = e.Message;
            LastSyncText = e.LastSyncTime?.ToString("HH:mm:ss") ?? LastSyncText;
            IsRunning = _service.IsRunning;

            if (e.IsError)
                _setStatus("剪贴板同步：" + e.Message);
        });
    }

    private void MarkDirty()
    {
        if (_loading)
            return;

        _isDirty = true;
        OnPropertyChanged(nameof(IsDirty));
        StatusMessage = "有未保存的修改。";
    }
}
