using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using WeaselStudio.Infrastructure;
using WeaselStudio.Models;
using WeaselStudio.Services;

namespace WeaselStudio.ViewModels;

public sealed class InputHabitsViewModel : ObservableObject, IReloadable
{
    private readonly ConfigService _config;
    private readonly InputHabitsService _service;
    private readonly Func<Task<DeployResult>> _deploy;
    private readonly Action<string> _setStatus;

    private SchemaInfo? _selectedSchema;
    private int _pageSize = 6;
    private bool _isDirty;
    private bool _suppress;
    private string _statusMessage = "选择方案后调整开关，保存会写入 <方案>.custom.yaml。";

    public InputHabitsViewModel(
        ConfigService config,
        Func<Task<DeployResult>> deploy,
        Action<string> setStatus)
    {
        _config = config;
        _service = new InputHabitsService(config);
        _deploy = deploy;
        _setStatus = setStatus;

        Schemas = new ObservableCollection<SchemaInfo>();
        Switches = new ObservableCollection<SchemaSwitch>();

        ReloadCommand = new RelayCommand(ReloadWithConfirm);
        SaveCommand = new AsyncRelayCommand(() => SaveAsync(false));
        SaveAndDeployCommand = new AsyncRelayCommand(() => SaveAsync(true));
        OpenSchemaFileCommand = new RelayCommand(OpenSchemaFile);
    }

    public ObservableCollection<SchemaInfo> Schemas { get; }
    public ObservableCollection<SchemaSwitch> Switches { get; }

    public SchemaInfo? SelectedSchema
    {
        get => _selectedSchema;
        set
        {
            if (ReferenceEquals(_selectedSchema, value))
                return;

            if (IsDirty && _selectedSchema is not null)
            {
                if (MessageBox.Show(
                        "当前方案有未保存的修改，切换方案会丢弃它们。继续吗？",
                        "切换方案",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question) != MessageBoxResult.Yes)
                {
                    OnPropertyChanged(nameof(SelectedSchema));
                    return;
                }
            }

            if (SetProperty(ref _selectedSchema, value))
                LoadForSelectedSchema();
        }
    }

    public int PageSize
    {
        get => _pageSize;
        set
        {
            if (!SetProperty(ref _pageSize, value))
                return;

            MarkDirty();
        }
    }

    public bool IsDirty
    {
        get => _isDirty;
        private set => SetProperty(ref _isDirty, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public ICommand ReloadCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand SaveAndDeployCommand { get; }
    public ICommand OpenSchemaFileCommand { get; }

    public void Reload()
    {
        var selectedId = SelectedSchema?.Id;

        _suppress = true;
        Schemas.Clear();
        foreach (var schema in _config.LoadSchemas())
            Schemas.Add(schema);

        var selected = Schemas.FirstOrDefault(
                           x => string.Equals(x.Id, selectedId, StringComparison.OrdinalIgnoreCase))
                       ?? Schemas.FirstOrDefault(x => x.IsEnabled)
                       ?? Schemas.FirstOrDefault();

        _selectedSchema = selected;
        OnPropertyChanged(nameof(SelectedSchema));
        LoadForSelectedSchema();
        _suppress = false;
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

    private void LoadForSelectedSchema()
    {
        foreach (var item in Switches)
            item.PropertyChanged -= OnSwitchChanged;

        Switches.Clear();

        _suppress = true;

        if (_selectedSchema is not null)
        {
            foreach (var item in _service.LoadSwitches(_selectedSchema))
            {
                if (item.States.Count > 0 &&
                    (item.Reset < 0 || item.Reset >= item.States.Count))
                {
                    item.Reset = 0;
                }

                item.PropertyChanged += OnSwitchChanged;
                Switches.Add(item);
            }

            PageSize = _service.LoadPageSize();
            StatusMessage = $"已加载 {_selectedSchema.DisplayName} 的 {Switches.Count} 个开关。";
        }
        else
        {
            PageSize = 6;
            StatusMessage = "没有可用的输入方案。";
        }

        _suppress = false;
        IsDirty = false;
    }

    private async Task SaveAsync(bool deploy)
    {
        if (SelectedSchema is null)
            return;

        try
        {
            _service.SaveSwitches(SelectedSchema, Switches.ToList());
            _service.SavePageSize(PageSize);

            IsDirty = false;
            StatusMessage = $"已保存 {SelectedSchema.DisplayName} 的开关和候选数。";
            _setStatus(StatusMessage);

            if (deploy)
            {
                if (_config.Environment.IsCustomUserDir)
                {
                    StatusMessage = "已保存（测试模式，不执行部署）。";
                    return;
                }

                StatusMessage = "已保存，正在部署…";
                _setStatus(StatusMessage);
                var result = await _deploy();
                StatusMessage = result.Success
                    ? "已保存并部署完成。"
                    : "已保存，但部署失败：" + result.Summary;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "保存失败：" + ex.Message;
            _setStatus(StatusMessage);
            throw;
        }
    }

    private void OpenSchemaFile()
    {
        if (SelectedSchema is null)
            return;

        ShellService.OpenFileLocation(_service.GetSchemaCustomPath(SelectedSchema));
    }

    private void OnSwitchChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_suppress)
            MarkDirty();
    }

    private void MarkDirty()
    {
        if (_suppress)
            return;

        IsDirty = true;
        StatusMessage = "有未保存的修改。";
    }
}
