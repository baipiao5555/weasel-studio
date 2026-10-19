using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using WeaselStudio.Infrastructure;
using WeaselStudio.Services;

namespace WeaselStudio.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly RimeEnvironment _environment;
    private readonly ConfigService _config;
    private object _currentPage;
    private string _selectedNavKey = "dashboard";
    private string _pageTitle = "概览";
    private string _pageSubtitle = "检查小狼毫安装状态、当前方案和配置概况。";
    private string _statusMessage = "就绪";
    private bool _isBusy;

    public MainViewModel()
    {
        _environment = RimeEnvironment.Detect();
        _config = new ConfigService(_environment);

        Dashboard = new DashboardViewModel(_config, DeployAndReportAsync, SetStatus);
        Schemas = new SchemasViewModel(_config, DeployAndReportAsync, SetStatus);
        Appearance = new AppearanceViewModel(_config, DeployAndReportAsync, SetStatus);
        Advanced = new AdvancedViewModel(_config, DeployAndReportAsync, SetStatus);

        _currentPage = Dashboard;

        NavigateCommand = new RelayCommand(parameter => Navigate(parameter as string));
        DeployCommand = new AsyncRelayCommand(DeployAndReportAsync);
        OpenUserDirCommand = new RelayCommand(OpenUserDir);
        OpenWeaselDirCommand = new RelayCommand(OpenWeaselDir);

        Dashboard.Reload();
    }

    public RimeEnvironment Environment => _environment;
    public bool IsEnvironmentFound => _environment.IsFound;
    public string VersionText => string.IsNullOrWhiteSpace(_environment.Version)
        ? "未知版本"
        : _environment.Version;

    public Brush StatusBrush => _environment.IsFound
        ? new SolidColorBrush(Color.FromRgb(0x12, 0xB7, 0x6A))
        : new SolidColorBrush(Color.FromRgb(0xF0, 0x44, 0x38));

    public DashboardViewModel Dashboard { get; }
    public SchemasViewModel Schemas { get; }
    public AppearanceViewModel Appearance { get; }
    public AdvancedViewModel Advanced { get; }

    public object CurrentPage
    {
        get => _currentPage;
        private set => SetProperty(ref _currentPage, value);
    }

    public string SelectedNavKey
    {
        get => _selectedNavKey;
        set
        {
            if (SetProperty(ref _selectedNavKey, value))
                Navigate(value);
        }
    }

    public string PageTitle
    {
        get => _pageTitle;
        private set => SetProperty(ref _pageTitle, value);
    }

    public string PageSubtitle
    {
        get => _pageSubtitle;
        private set => SetProperty(ref _pageSubtitle, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public ICommand NavigateCommand { get; }
    public ICommand DeployCommand { get; }
    public ICommand OpenUserDirCommand { get; }
    public ICommand OpenWeaselDirCommand { get; }

    private void Navigate(string? key)
    {
        var normalized = key switch
        {
            "schemas" => "schemas",
            "appearance" => "appearance",
            "advanced" => "advanced",
            _ => "dashboard"
        };

        IReloadable? page = normalized switch
        {
            "schemas" => Schemas,
            "appearance" => Appearance,
            "advanced" => Advanced,
            _ => Dashboard
        };

        if (page is not null && !page.IsDirty)
            page.Reload();

        CurrentPage = page ?? Dashboard;
        PageTitle = normalized switch
        {
            "schemas" => "输入方案",
            "appearance" => "外观与候选窗",
            "advanced" => "高级 YAML",
            _ => "概览"
        };
        PageSubtitle = normalized switch
        {
            "schemas" => "勾选要启用的方案；列表顺序就是方案切换顺序，第一位为默认。",
            "appearance" => "修改会写入 weasel.custom.yaml 的 patch 节点，原有注释会保留。",
            "advanced" => "直接编辑 *.custom.yaml；保存前会校验语法并自动备份。",
            _ => "检查小狼毫安装状态、当前方案和配置概况。"
        };

        if (_selectedNavKey != normalized)
        {
            _selectedNavKey = normalized;
            OnPropertyChanged(nameof(SelectedNavKey));
        }
    }

    private async Task<DeployResult> DeployAndReportAsync()
    {
        if (IsBusy)
            return new DeployResult(-1, "", "已有部署任务正在运行。", false);

        IsBusy = true;
        SetStatus("正在部署配置，请稍候…");

        try
        {
            var result = await DeployService.DeployAsync(_environment);
            SetStatus(result.Summary);
            if (!result.Success)
            {
                MessageBox.Show(
                    result.Summary,
                    "部署失败",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            Dashboard.Reload();
            return result;
        }
        catch (Exception ex)
        {
            SetStatus("部署失败：" + ex.Message);
            MessageBox.Show(ex.Message, "部署失败", MessageBoxButton.OK, MessageBoxImage.Error);
            return new DeployResult(-1, "", ex.Message, false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void SetStatus(string message)
    {
        StatusMessage = $"{DateTime.Now:HH:mm:ss}  {message}";
    }

    private void OpenUserDir() => ShellService.OpenDirectory(_environment.UserDataDir);

    private void OpenWeaselDir() => ShellService.OpenDirectory(_environment.WeaselRoot);
}
