using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using WeaselStudio.Infrastructure;
using WeaselStudio.Models;
using WeaselStudio.Services;

namespace WeaselStudio.ViewModels;

public sealed class FuzzyPinyinViewModel : ObservableObject, IReloadable
{
    private readonly ConfigService _config;
    private readonly FuzzyPinyinService _service;
    private readonly Func<Task<DeployResult>> _deploy;
    private readonly Action<string> _setStatus;

    private SchemaInfo? _selectedSchema;
    private IReadOnlyList<string> _baseRules = Array.Empty<string>();
    private IReadOnlyList<string> _customRules = Array.Empty<string>();
    private bool _isDirty;
    private bool _suppress;
    private bool _canEdit;
    private string _statusMessage = "选择方案后勾选需要的模糊音，保存会重建 algebra 列表。";
    private int _baseRuleCount;
    private int _customRuleCount;
    private int _totalRuleCount;

    public FuzzyPinyinViewModel(
        ConfigService config,
        Func<Task<DeployResult>> deploy,
        Action<string> setStatus)
    {
        _config = config;
        _service = new FuzzyPinyinService(config);
        _deploy = deploy;
        _setStatus = setStatus;

        Schemas = new ObservableCollection<SchemaInfo>();
        Groups = new ObservableCollection<FuzzyRuleGroup>();

        ReloadCommand = new RelayCommand(ReloadWithConfirm);
        SelectAllCommand = new RelayCommand(SelectAll);
        ClearAllCommand = new RelayCommand(ClearAll);
        SaveCommand = new AsyncRelayCommand(() => SaveAsync(false));
        SaveAndDeployCommand = new AsyncRelayCommand(() => SaveAsync(true));
        OpenCustomFileCommand = new RelayCommand(OpenCustomFile);
    }

    public ObservableCollection<SchemaInfo> Schemas { get; }
    public ObservableCollection<FuzzyRuleGroup> Groups { get; }

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
                        "当前方案有未保存的模糊音修改，切换会丢弃它们。继续吗？",
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

    public bool CanEdit
    {
        get => _canEdit;
        private set => SetProperty(ref _canEdit, value);
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

    public int BaseRuleCount
    {
        get => _baseRuleCount;
        private set => SetProperty(ref _baseRuleCount, value);
    }

    public int CustomRuleCount
    {
        get => _customRuleCount;
        private set => SetProperty(ref _customRuleCount, value);
    }

    public int TotalRuleCount
    {
        get => _totalRuleCount;
        private set => SetProperty(ref _totalRuleCount, value);
    }

    public ICommand ReloadCommand { get; }
    public ICommand SelectAllCommand { get; }
    public ICommand ClearAllCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand SaveAndDeployCommand { get; }
    public ICommand OpenCustomFileCommand { get; }

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
        foreach (var group in Groups)
            group.PropertyChanged -= OnGroupChanged;

        Groups.Clear();

        _suppress = true;
        _baseRules = Array.Empty<string>();
        _customRules = Array.Empty<string>();

        if (_selectedSchema is null)
        {
            CanEdit = false;
            BaseRuleCount = 0;
            CustomRuleCount = 0;
            TotalRuleCount = 0;
            StatusMessage = "没有可用的输入方案。";
            _suppress = false;
            IsDirty = false;
            return;
        }

        _baseRules = _service.LoadBaseAlgebra(_selectedSchema);
        _customRules = _service.LoadCustomAlgebra(_selectedSchema);

        foreach (var group in FuzzyPinyinService.CreateGroups())
        {
            group.IsBuiltIn = group.Rules.All(rule => _baseRules.Contains(rule));
            group.IsEnabled = group.IsBuiltIn ||
                              group.Rules.All(rule => _customRules.Contains(rule));
            group.PropertyChanged += OnGroupChanged;
            Groups.Add(group);
        }

        BaseRuleCount = _baseRules.Count;
        CustomRuleCount = _customRules.Count;
        RecalculateTotal();
        CanEdit = _baseRules.Count > 0;

        StatusMessage = CanEdit
            ? $"已加载 {_selectedSchema.DisplayName}：基础 {_baseRules.Count} 条规则。"
            : "该方案没有找到 speller/algebra，可能不是拼音方案，模糊音不适用。";

        _suppress = false;
        IsDirty = false;
    }

    private void SelectAll()
    {
        foreach (var group in Groups.Where(x => x.CanToggle))
            group.IsEnabled = true;

        MarkDirty();
    }

    private void ClearAll()
    {
        foreach (var group in Groups.Where(x => x.CanToggle))
            group.IsEnabled = false;

        MarkDirty();
    }

    private async Task SaveAsync(bool deploy)
    {
        if (SelectedSchema is null)
            return;

        if (!CanEdit)
        {
            StatusMessage = "该方案没有 algebra，已取消保存。";
            _setStatus(StatusMessage);
            return;
        }

        try
        {
            var merged = _service.MergeRules(_baseRules, _customRules, Groups);
            _service.SaveAlgebra(SelectedSchema, merged);

            _customRules = merged;
            BaseRuleCount = _baseRules.Count;
            CustomRuleCount = _customRules.Count;
            RecalculateTotal();
            IsDirty = false;

            StatusMessage =
                $"已保存 {SelectedSchema.DisplayName} 的 algebra：共 {merged.Count} 条规则。";
            _setStatus(StatusMessage);

            if (deploy)
            {
                if (_config.Environment.IsCustomUserDir)
                {
                    StatusMessage = "已保存（测试模式，不执行部署）。";
                    return;
                }

                StatusMessage = "模糊音已保存，正在部署…";
                _setStatus(StatusMessage);
                var result = await _deploy();
                StatusMessage = result.Success
                    ? "模糊音已保存并部署完成。"
                    : "模糊音已保存，但部署失败：" + result.Summary;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "保存失败：" + ex.Message;
            _setStatus(StatusMessage);
            throw;
        }
    }

    private void OpenCustomFile()
    {
        if (SelectedSchema is null)
            return;

        ShellService.OpenFileLocation(_service.GetCustomPath(SelectedSchema));
    }

    private void OnGroupChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suppress)
            return;

        MarkDirty();
        RecalculateTotal();
    }

    private void MarkDirty()
    {
        if (_suppress)
            return;

        IsDirty = true;
        StatusMessage = "有未保存的修改。";
    }

    private void RecalculateTotal()
    {
        TotalRuleCount = _service.MergeRules(_baseRules, _customRules, Groups).Count;
    }
}
