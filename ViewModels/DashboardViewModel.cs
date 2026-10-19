using System.IO;
using System.Windows.Input;
using WeaselStudio.Infrastructure;
using WeaselStudio.Services;

namespace WeaselStudio.ViewModels;

public sealed class DashboardViewModel : ObservableObject, IReloadable
{
    private readonly ConfigService _config;
    private readonly Func<Task<DeployResult>> _deploy;
    private readonly Action<string> _setStatus;

    private string _currentSchemaName = "—";
    private string _currentSchemaId = "—";
    private int _schemaCount;
    private int _enabledSchemaCount;
    private int _customFileCount;
    private string _backupText = "尚无备份";

    public DashboardViewModel(
        ConfigService config,
        Func<Task<DeployResult>> deploy,
        Action<string> setStatus)
    {
        _config = config;
        _deploy = deploy;
        _setStatus = setStatus;

        RefreshCommand = new RelayCommand(Reload);
        DeployCommand = new AsyncRelayCommand(_deploy);
        OpenUserDirCommand = new RelayCommand(OpenUserDir);
        OpenWeaselDirCommand = new RelayCommand(OpenWeaselDir);
        OpenBackupsCommand = new RelayCommand(OpenBackups);
    }

    public RimeEnvironment Environment => _config.Environment;
    public string UserDataDir => _config.UserDataDir;
    public string WeaselRoot => _config.Environment.WeaselRoot;
    public string DeployerPath => _config.Environment.DeployerPath;
    public string Version => string.IsNullOrWhiteSpace(_config.Environment.Version)
        ? "未知版本"
        : _config.Environment.Version;
    public string BackupDirectory => BackupService.GetBackupDirectory(_config.UserDataDir);
    public bool IsDirty => false;

    public string CurrentSchemaName
    {
        get => _currentSchemaName;
        private set => SetProperty(ref _currentSchemaName, value);
    }

    public string CurrentSchemaId
    {
        get => _currentSchemaId;
        private set => SetProperty(ref _currentSchemaId, value);
    }

    public int SchemaCount
    {
        get => _schemaCount;
        private set => SetProperty(ref _schemaCount, value);
    }

    public int EnabledSchemaCount
    {
        get => _enabledSchemaCount;
        private set => SetProperty(ref _enabledSchemaCount, value);
    }

    public int CustomFileCount
    {
        get => _customFileCount;
        private set => SetProperty(ref _customFileCount, value);
    }

    public string BackupText
    {
        get => _backupText;
        private set => SetProperty(ref _backupText, value);
    }

    public ICommand RefreshCommand { get; }
    public ICommand DeployCommand { get; }
    public ICommand OpenUserDirCommand { get; }
    public ICommand OpenWeaselDirCommand { get; }
    public ICommand OpenBackupsCommand { get; }

    public void Reload()
    {
        var schemas = _config.LoadSchemas();
        SchemaCount = schemas.Count;
        EnabledSchemaCount = schemas.Count(x => x.IsEnabled);

        var userState = YamlConfig.LoadMap(Path.Combine(_config.UserDataDir, "user.yaml"));
        var currentId = YamlConfig.AsString(
            YamlConfig.Get(userState, "var", "previously_selected_schema"));

        var current = schemas.FirstOrDefault(
            x => string.Equals(x.Id, currentId, StringComparison.OrdinalIgnoreCase));

        current ??= schemas.FirstOrDefault(x => x.IsEnabled) ?? schemas.FirstOrDefault();

        CurrentSchemaName = current?.DisplayName ?? "—";
        CurrentSchemaId = current?.Id ?? "—";

        CustomFileCount = _config.GetEditableFiles().Length;
        BackupText = FindLatestBackup();
    }

    private string FindLatestBackup()
    {
        var directory = BackupDirectory;
        if (!Directory.Exists(directory))
            return "尚无备份";

        var latest = new DirectoryInfo(directory)
            .EnumerateFiles("*.bak")
            .OrderByDescending(x => x.LastWriteTime)
            .FirstOrDefault();

        return latest is null
            ? "尚无备份"
            : $"{latest.Name} · {latest.LastWriteTime:yyyy-MM-dd HH:mm}";
    }

    private void OpenUserDir() => ShellService.OpenDirectory(_config.UserDataDir);

    private void OpenWeaselDir() => ShellService.OpenDirectory(_config.Environment.WeaselRoot);

    private void OpenBackups()
    {
        Directory.CreateDirectory(BackupDirectory);
        ShellService.OpenDirectory(BackupDirectory);
    }
}
