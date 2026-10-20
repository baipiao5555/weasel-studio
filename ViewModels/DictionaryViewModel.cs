using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using WeaselStudio.Infrastructure;
using WeaselStudio.Services;

namespace WeaselStudio.ViewModels;

public sealed class DictionaryViewModel : ObservableObject, IReloadable
{
    private readonly ConfigService _config;
    private readonly DictionaryService _service;
    private readonly Action<string> _setStatus;

    private string? _selectedDictionary;
    private bool _isBusy;
    private string _statusMessage = "备份/导出是只读操作；导入/恢复会修改用户词典，建议先备份。";

    public DictionaryViewModel(ConfigService config, Action<string> setStatus)
    {
        _config = config;
        _service = new DictionaryService(config);
        _setStatus = setStatus;

        Dictionaries = new ObservableCollection<string>();

        RefreshCommand = new RelayCommand(Refresh);
        BackupCommand = new AsyncRelayCommand(BackupAsync);
        ExportCommand = new AsyncRelayCommand(ExportAsync);
        ImportCommand = new AsyncRelayCommand(ImportAsync);
        RestoreCommand = new AsyncRelayCommand(RestoreAsync);
        OpenSyncFolderCommand = new RelayCommand(
            () => ShellService.OpenDirectory(_service.GetSyncDirectory()));

        Refresh();
    }

    public ObservableCollection<string> Dictionaries { get; }

    public string? SelectedDictionary
    {
        get => _selectedDictionary;
        set => SetProperty(ref _selectedDictionary, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public bool CanRun => !_config.Environment.IsCustomUserDir;
    public bool IsDirty => false;

    public string SyncDirectory => _service.GetSyncDirectory();

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public ICommand RefreshCommand { get; }
    public ICommand BackupCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand ImportCommand { get; }
    public ICommand RestoreCommand { get; }
    public ICommand OpenSyncFolderCommand { get; }

    public void Reload()
    {
        Refresh();
    }

    private void Refresh()
    {
        var selected = SelectedDictionary;

        Dictionaries.Clear();
        foreach (var dictionary in _service.GetUserDictionaries())
            Dictionaries.Add(dictionary);

        SelectedDictionary = Dictionaries.FirstOrDefault(
                                 x => string.Equals(x, selected, StringComparison.OrdinalIgnoreCase))
                             ?? Dictionaries.FirstOrDefault();

        StatusMessage = CanRun
            ? $"已找到 {Dictionaries.Count} 个用户词典。"
            : "测试模式下不执行词典操作。";
    }

    private async Task BackupAsync()
    {
        if (!EnsureCanRun() || SelectedDictionary is null)
            return;

        await RunAsync(
            $"正在备份 {SelectedDictionary}…",
            () => _service.BackupAsync(SelectedDictionary),
            openSyncAfterSuccess: true);
    }

    private async Task ExportAsync()
    {
        if (!EnsureCanRun() || SelectedDictionary is null)
            return;

        var dialog = new SaveFileDialog
        {
            Title = "导出用户词典",
            Filter = "词典快照 (*.txt)|*.txt|所有文件 (*.*)|*.*",
            FileName = $"{SelectedDictionary}.userdb.txt"
        };

        if (dialog.ShowDialog() != true)
            return;

        await RunAsync(
            $"正在导出 {SelectedDictionary}…",
            () => _service.ExportAsync(SelectedDictionary, dialog.FileName));
    }

    private async Task ImportAsync()
    {
        if (!EnsureCanRun() || SelectedDictionary is null)
            return;

        var dialog = new OpenFileDialog
        {
            Title = "导入用户词典",
            Filter = "词典快照 (*.txt)|*.txt|所有文件 (*.*)|*.*"
        };

        if (dialog.ShowDialog() != true)
            return;

        if (MessageBox.Show(
                $"导入会修改用户词典 {SelectedDictionary}。建议先点“备份”。继续吗？",
                "导入用户词典",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        await RunAsync(
            $"正在导入 {SelectedDictionary}…",
            () => _service.ImportAsync(SelectedDictionary, dialog.FileName));
    }

    private async Task RestoreAsync()
    {
        if (!EnsureCanRun())
            return;

        var dialog = new OpenFileDialog
        {
            Title = "恢复用户词典快照",
            Filter = "词典快照 (*.txt)|*.txt|所有文件 (*.*)|*.*"
        };

        if (dialog.ShowDialog() != true)
            return;

        if (MessageBox.Show(
                "恢复会覆盖对应的用户词典。建议先点“备份”。继续吗？",
                "恢复用户词典",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        await RunAsync(
            "正在恢复用户词典…",
            () => _service.RestoreAsync(dialog.FileName));
    }

    private async Task RunAsync(
        string busyMessage,
        Func<Task<DictionaryCommandResult>> action,
        bool openSyncAfterSuccess = false)
    {
        if (IsBusy)
            return;

        IsBusy = true;
        StatusMessage = busyMessage;
        _setStatus(busyMessage);

        try
        {
            var result = await action();
            StatusMessage = result.Summary;
            _setStatus(StatusMessage);

            if (!result.Success)
            {
                MessageBox.Show(
                    result.Summary,
                    "词典操作失败",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            else if (openSyncAfterSuccess)
            {
                ShellService.OpenDirectory(SyncDirectory);
            }

            Refresh();
            StatusMessage = result.Summary;
        }
        catch (Exception ex)
        {
            StatusMessage = "词典操作失败：" + ex.Message;
            _setStatus(StatusMessage);
            MessageBox.Show(ex.Message, "词典操作失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool EnsureCanRun()
    {
        if (CanRun)
            return true;

        const string message = "当前是测试模式（自定义用户目录），词典操作会作用于系统小狼毫，已禁用。";
        StatusMessage = message;
        _setStatus(message);
        MessageBox.Show(message, "测试模式", MessageBoxButton.OK, MessageBoxImage.Information);
        return false;
    }
}
