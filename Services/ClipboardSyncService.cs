using System.Windows.Threading;
using WeaselStudio.Models;

namespace WeaselStudio.Services;

public sealed class SyncStatusEventArgs : EventArgs
{
    public SyncStatusEventArgs(string message, bool isError, DateTime? lastSyncTime)
    {
        Message = message;
        IsError = isError;
        LastSyncTime = lastSyncTime;
    }

    public string Message { get; }
    public bool IsError { get; }
    public DateTime? LastSyncTime { get; }
}

public sealed class ClipboardSyncService : IDisposable
{
    private ClipboardMonitor? _monitor;
    private readonly SemaphoreSlim _syncLock = new(1, 1);

    private SyncSettings _settings = new();
    private SyncClipboardClient? _client;
    private CancellationTokenSource? _loopCts;
    private CancellationTokenSource? _debounceCts;
    private string? _lastEtag;
    private string? _lastSyncedHash;
    private string? _suppressHash;
    private bool _disposed;

    public event EventHandler<SyncStatusEventArgs>? StatusChanged;

    public bool IsRunning => _client is not null;
    public DateTime? LastSyncTime { get; private set; }

    public bool Start(SyncSettings settings)
    {
        Stop(false);

        _settings = settings.Clone();
        _lastEtag = null;
        _lastSyncedHash = null;
        _suppressHash = null;

        if (!_settings.Enabled || string.IsNullOrWhiteSpace(_settings.ServerUrl))
        {
            SetStatus("同步未启用");
            return false;
        }

        try
        {
            _client = new SyncClipboardClient(
                _settings.ServerUrl,
                _settings.Username,
                _settings.Password,
                _settings.AllowPrivateAddresses);

            EnsureMonitor();
        }
        catch (Exception ex)
        {
            _client?.Dispose();
            _client = null;
            SetStatus("同步启动失败：" + ex.Message, true);
            return false;
        }

        _loopCts = new CancellationTokenSource();
        _ = Task.Run(() => LoopAsync(_loopCts.Token));
        SetStatus("同步已启动");
        return true;
    }

    public void Stop(bool notify = true)
    {
        _loopCts?.Cancel();
        _loopCts?.Dispose();
        _loopCts = null;

        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        _debounceCts = null;

        _client?.Dispose();
        _client = null;

        if (notify)
            SetStatus("同步已停止");
    }

    public async Task PullNowAsync()
    {
        if (_client is null)
        {
            SetStatus("同步未启用");
            return;
        }

        try
        {
            await PullOnceAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            SetStatus("同步失败：" + ex.Message, true);
        }
    }

    private async Task LoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await PullOnceAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                if (IsRunning)
                    SetStatus("同步失败：" + ex.Message, true);
            }

            var interval = Math.Clamp(_settings.IntervalSeconds, 1, 60);
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(interval), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task PullOnceAsync(CancellationToken cancellationToken)
    {
        var client = _client;
        if (client is null)
            return;

        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            client = _client;
            if (client is null)
                return;

            var (data, etag) = await client.PullAsync(_lastEtag, cancellationToken);
            if (!string.IsNullOrWhiteSpace(etag))
                _lastEtag = etag;

            if (data is null)
            {
                SetStatus("已连接，没有新内容");
                return;
            }

            if (!string.Equals(data.Type, "Text", StringComparison.OrdinalIgnoreCase))
            {
                SetStatus($"收到 {data.Type} 内容，当前版本只同步文本");
                return;
            }

            if (string.IsNullOrEmpty(data.Text))
                return;

            var hash = string.IsNullOrWhiteSpace(data.Hash)
                ? SyncClipboardClient.HashText(data.Text)
                : data.Hash;

            if (string.Equals(hash, _lastSyncedHash, StringComparison.OrdinalIgnoreCase))
            {
                SetStatus("已是最新");
                return;
            }

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is null)
                return;

            _suppressHash = hash;
            var applied = await dispatcher.InvokeAsync(
                () => ClipboardHelper.TrySetText(data.Text));

            if (!applied)
            {
                _suppressHash = null;
                SetStatus("写入系统剪贴板失败", true);
                return;
            }

            _lastSyncedHash = hash;
            LastSyncTime = DateTime.Now;
            SetStatus("已从服务器更新剪贴板");
        }
        finally
        {
            _syncLock.Release();
        }
    }

    private void EnsureMonitor()
    {
        if (_monitor is not null)
            return;

        _monitor = new ClipboardMonitor();
        _monitor.TextChanged += OnClipboardTextChanged;
    }

    private void OnClipboardTextChanged(object? sender, string text)
    {
        if (_client is null || string.IsNullOrEmpty(text))
            return;

        var hash = SyncClipboardClient.HashText(text);

        if (string.Equals(hash, _suppressHash, StringComparison.OrdinalIgnoreCase))
        {
            _suppressHash = null;
            return;
        }

        if (string.Equals(hash, _lastSyncedHash, StringComparison.OrdinalIgnoreCase))
            return;

        _debounceCts?.Cancel();
        _debounceCts?.Dispose();

        var cts = new CancellationTokenSource();
        _debounceCts = cts;
        _ = PushAfterDelayAsync(text, cts.Token);
    }

    private async Task PushAfterDelayAsync(string text, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(500, cancellationToken);
            await PushAsync(text, cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (IsRunning)
                SetStatus("上传失败：" + ex.Message, true);
        }
    }

    private async Task PushAsync(string text, CancellationToken cancellationToken)
    {
        var client = _client;
        if (client is null)
            return;

        var hash = SyncClipboardClient.HashText(text);
        if (string.Equals(hash, _suppressHash, StringComparison.OrdinalIgnoreCase))
        {
            _suppressHash = null;
            return;
        }

        if (string.Equals(hash, _lastSyncedHash, StringComparison.OrdinalIgnoreCase))
            return;

        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            client = _client;
            if (client is null)
                return;

            await client.PushTextAsync(text, hash, cancellationToken);
            _lastSyncedHash = hash;
            LastSyncTime = DateTime.Now;
            SetStatus("已上传到服务器");
        }
        finally
        {
            _syncLock.Release();
        }
    }

    private void SetStatus(string message, bool isError = false)
    {
        StatusChanged?.Invoke(
            this,
            new SyncStatusEventArgs(message, isError, LastSyncTime));
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Stop(false);

        if (_monitor is not null)
        {
            _monitor.TextChanged -= OnClipboardTextChanged;
            _monitor.Dispose();
            _monitor = null;
        }

        _syncLock.Dispose();
    }
}
