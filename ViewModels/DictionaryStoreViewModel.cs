using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using WeaselStudio.Infrastructure;
using WeaselStudio.Models;
using WeaselStudio.Services;

namespace WeaselStudio.ViewModels;

public sealed class DictionaryPackageItem : ObservableObject
{
    private bool _isInstalled;
    private bool _isBusy;
    private double _progress;
    private string _statusText = "未安装";

    public DictionaryPackageItem(DictionaryPackage package)
    {
        Package = package;
    }

    public DictionaryPackage Package { get; }
    public string Name => Package.Name;
    public string Description => Package.Description;
    public string License => Package.License;
    public string SourceUrl => Package.SourceUrl;
    public string SizeText => $"{Package.SizeBytes / 1024.0 / 1024.0:0.0} MB";
    public string SchemasText => string.Join("、", Package.SchemaIds);

    public bool IsInstalled
    {
        get => _isInstalled;
        set => SetProperty(ref _isInstalled, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public double Progress
    {
        get => _progress;
        set => SetProperty(ref _progress, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }
}

public sealed class DictionaryStoreViewModel : ObservableObject, IReloadable
{
    private readonly ConfigService _config;
    private readonly DictionaryInstallerService _installer;
    private readonly Func<Task<DeployResult>> _deploy;
    private readonly Action<string> _setStatus;

    private bool _isBusy;
    private string _statusMessage = "选择词典包后一键下载、校验、安装并部署。";

    public DictionaryStoreViewModel(
        ConfigService config,
        Func<Task<DeployResult>> deploy,
        Action<string> setStatus)
    {
        _config = config;
        _installer = new DictionaryInstallerService(config);
        _deploy = deploy;
        _setStatus = setStatus;

        Packages = new ObservableCollection<DictionaryPackageItem>(
            DictionaryCatalog.Packages.Select(x => new DictionaryPackageItem(x)));

        ReloadCommand = new RelayCommand(Reload);
        InstallCommand = new AsyncRelayCommand(parameter => InstallAsync(parameter as DictionaryPackageItem));
        OpenUserDirCommand = new RelayCommand(
            () => ShellService.OpenDirectory(_config.UserDataDir));

        Reload();
    }

    public ObservableCollection<DictionaryPackageItem> Packages { get; }
    public bool IsDirty => false;

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public ICommand ReloadCommand { get; }
    public ICommand InstallCommand { get; }
    public ICommand OpenUserDirCommand { get; }

    public void Reload()
    {
        foreach (var item in Packages)
        {
            item.IsInstalled = IsInstalled(item.Package);
            item.Progress = 0;
            item.StatusText = item.IsInstalled ? "已安装" : "未安装";
        }

        StatusMessage = $"共 {Packages.Count} 个词典包；安装后会写入用户目录并加入方案列表。";
    }

    private bool IsInstalled(DictionaryPackage package)
    {
        if (!Directory.Exists(_config.UserDataDir))
            return false;

        return package.SchemaIds.All(schema =>
            File.Exists(Path.Combine(_config.UserDataDir, schema + ".schema.yaml")));
    }

    private async Task InstallAsync(DictionaryPackageItem? item)
    {
        if (item is null || item.IsBusy || IsBusy)
            return;

        if (MessageBox.Show(
                $"将下载 {item.Name}（{item.SizeText}），安装到 %APPDATA%\\Rime，并加入方案列表。\n\n" +
                $"来源：{item.SourceUrl}\n授权：{item.License}\n\n继续吗？",
                "安装词典",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        IsBusy = true;
        item.IsBusy = true;
        item.Progress = 0;
        item.StatusText = "正在下载…";
        StatusMessage = $"正在下载 {item.Name}…";
        _setStatus(StatusMessage);

        try
        {
            var progress = new Progress<double>(value =>
            {
                item.Progress = value * 100;
                item.StatusText = $"下载 {item.Progress:0}%";
            });

            var result = await _installer.InstallAsync(
                item.Package,
                progress,
                CancellationToken.None);

            item.Progress = 100;
            item.StatusText = "正在部署…";
            StatusMessage = $"已写入 {result.FileCount} 个文件，正在部署…";
            _setStatus(StatusMessage);

            if (_config.Environment.IsCustomUserDir)
            {
                item.IsInstalled = IsInstalled(item.Package);
                item.StatusText = "已安装（测试模式未部署）";
                StatusMessage = "已安装（测试模式，不执行部署）；已加入方案列表。";
                _setStatus(StatusMessage);
                return;
            }

            var deploy = await _deploy();
            item.IsInstalled = IsInstalled(item.Package);
            item.StatusText = deploy.Success ? "已安装" : "已安装，部署失败";
            StatusMessage = deploy.Success
                ? $"{item.Name} 已安装并部署完成；已加入方案列表，可在“输入方案”页切换。"
                : $"{item.Name} 已安装，但部署失败：{deploy.Summary}";
            _setStatus(StatusMessage);

            if (!deploy.Success)
            {
                MessageBox.Show(
                    StatusMessage,
                    "词典安装",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            item.StatusText = "安装失败";
            StatusMessage = $"安装 {item.Name} 失败：{ex.Message}";
            _setStatus(StatusMessage);
            MessageBox.Show(
                StatusMessage,
                "词典安装失败",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            item.IsBusy = false;
            IsBusy = false;
        }
    }
}
