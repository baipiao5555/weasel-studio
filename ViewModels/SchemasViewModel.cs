using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using WeaselStudio.Infrastructure;
using WeaselStudio.Models;
using WeaselStudio.Services;

namespace WeaselStudio.ViewModels;

public sealed class SchemasViewModel : ObservableObject, IReloadable
{
    private readonly ConfigService _config;
    private readonly Func<Task<DeployResult>> _deploy;
    private readonly Action<string> _setStatus;

    private SchemaInfo? _selectedSchema;
    private bool _isDirty;
    private bool _suppressDirty;
    private string _statusMessage = "勾选要启用的方案，用上移/下移调整顺序。";
    private int _enabledCount;
    private int _totalCount;

    public SchemasViewModel(
        ConfigService config,
        Func<Task<DeployResult>> deploy,
        Action<string> setStatus)
    {
        _config = config;
        _deploy = deploy;
        _setStatus = setStatus;

        Schemas = new ObservableCollection<SchemaInfo>();

        RefreshCommand = new RelayCommand(Refresh);
        MoveUpCommand = new RelayCommand(MoveUp);
        MoveDownCommand = new RelayCommand(MoveDown);
        SetDefaultCommand = new RelayCommand(SetDefault);
        SaveCommand = new AsyncRelayCommand(() => SaveAsync(false));
        SaveAndDeployCommand = new AsyncRelayCommand(() => SaveAsync(true));
        OpenFolderCommand = new RelayCommand(
            () => ShellService.OpenDirectory(_config.UserDataDir));
    }

    public ObservableCollection<SchemaInfo> Schemas { get; }

    public SchemaInfo? SelectedSchema
    {
        get => _selectedSchema;
        set => SetProperty(ref _selectedSchema, value);
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

    public int EnabledCount
    {
        get => _enabledCount;
        private set => SetProperty(ref _enabledCount, value);
    }

    public int TotalCount
    {
        get => _totalCount;
        private set => SetProperty(ref _totalCount, value);
    }

    public ICommand RefreshCommand { get; }
    public ICommand MoveUpCommand { get; }
    public ICommand MoveDownCommand { get; }
    public ICommand SetDefaultCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand SaveAndDeployCommand { get; }
    public ICommand OpenFolderCommand { get; }

    public void Reload()
    {
        var selectedId = SelectedSchema?.Id;

        foreach (var schema in Schemas)
            schema.PropertyChanged -= OnSchemaPropertyChanged;

        _suppressDirty = true;
        Schemas.Clear();

        foreach (var schema in _config.LoadSchemas())
        {
            schema.PropertyChanged += OnSchemaPropertyChanged;
            Schemas.Add(schema);
        }

        _suppressDirty = false;

        SelectedSchema = Schemas.FirstOrDefault(
            x => string.Equals(x.Id, selectedId, StringComparison.OrdinalIgnoreCase))
            ?? Schemas.FirstOrDefault();

        IsDirty = false;
        UpdateCounts();
        StatusMessage = $"已加载 {Schemas.Count} 个输入方案。";
    }

    private void Refresh()
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

    private void MoveUp()
    {
        if (SelectedSchema is null)
            return;

        var index = Schemas.IndexOf(SelectedSchema);
        if (index <= 0)
            return;

        Schemas.Move(index, index - 1);
        IsDirty = true;
    }

    private void MoveDown()
    {
        if (SelectedSchema is null)
            return;

        var index = Schemas.IndexOf(SelectedSchema);
        if (index < 0 || index >= Schemas.Count - 1)
            return;

        Schemas.Move(index, index + 1);
        IsDirty = true;
    }

    private void SetDefault()
    {
        if (SelectedSchema is null)
            return;

        var index = Schemas.IndexOf(SelectedSchema);
        if (index > 0)
            Schemas.Move(index, 0);

        SelectedSchema.IsEnabled = true;
        IsDirty = true;
        StatusMessage = $"{SelectedSchema.DisplayName} 已设为列表首位，保存并部署后生效。";
    }

    private async Task SaveAsync(bool deploy)
    {
        var enabled = Schemas
            .Where(x => x.IsEnabled)
            .Select(x => x.Id)
            .ToList();

        if (enabled.Count == 0)
        {
            StatusMessage = "至少保留一个输入方案。";
            _setStatus(StatusMessage);
            MessageBox.Show(
                "至少保留一个输入方案，否则小狼毫无法切换方案。",
                "无法保存",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        try
        {
            _config.SaveSchemaList(enabled);
            IsDirty = false;
            StatusMessage = $"已保存 {enabled.Count} 个启用的方案。";
            _setStatus(StatusMessage);

            if (deploy)
            {
                StatusMessage = "方案已保存，正在部署…";
                _setStatus(StatusMessage);
                var result = await _deploy();
                StatusMessage = result.Success
                    ? "方案已保存并部署完成。"
                    : "方案已保存，但部署失败：" + result.Summary;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "保存失败：" + ex.Message;
            _setStatus(StatusMessage);
            throw;
        }
    }

    private void OnSchemaPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SchemaInfo.IsEnabled))
            return;

        UpdateCounts();

        if (!_suppressDirty)
        {
            IsDirty = true;
            StatusMessage = "方案启用状态已修改，保存后生效。";
        }
    }

    private void UpdateCounts()
    {
        TotalCount = Schemas.Count;
        EnabledCount = Schemas.Count(x => x.IsEnabled);
    }
}
