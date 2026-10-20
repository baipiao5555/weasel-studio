using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using WeaselStudio.Infrastructure;
using WeaselStudio.Models;
using WeaselStudio.Services;

namespace WeaselStudio.ViewModels;

public sealed class PhrasesViewModel : ObservableObject, IReloadable
{
    private readonly ConfigService _config;
    private readonly PhrasesService _service;
    private readonly Func<Task<DeployResult>> _deploy;
    private readonly Action<string> _setStatus;

    private EditableFile? _selectedFile;
    private PhraseEntry? _selectedEntry;
    private List<string> _comments = new();
    private bool _isDirty;
    private bool _suppress;
    private string _statusMessage = "格式：短语<Tab>编码<Tab>权重（权重可省略）。";

    public PhrasesViewModel(
        ConfigService config,
        Func<Task<DeployResult>> deploy,
        Action<string> setStatus)
    {
        _config = config;
        _service = new PhrasesService(config);
        _deploy = deploy;
        _setStatus = setStatus;

        Files = new ObservableCollection<EditableFile>();
        Entries = new ObservableCollection<PhraseEntry>();

        ReloadFilesCommand = new RelayCommand(ReloadWithConfirm);
        AddCommand = new RelayCommand(Add);
        DeleteCommand = new RelayCommand(Delete);
        SaveCommand = new AsyncRelayCommand(() => SaveAsync(false));
        SaveAndDeployCommand = new AsyncRelayCommand(() => SaveAsync(true));
        OpenFolderCommand = new RelayCommand(
            () => ShellService.OpenFileLocation(SelectedFile?.Path));
    }

    public ObservableCollection<EditableFile> Files { get; }
    public ObservableCollection<PhraseEntry> Entries { get; }

    public EditableFile? SelectedFile
    {
        get => _selectedFile;
        set
        {
            if (ReferenceEquals(_selectedFile, value))
                return;

            if (IsDirty && _selectedFile is not null)
            {
                if (MessageBox.Show(
                        "当前短语文件有未保存的修改，切换文件会丢弃它们。继续吗？",
                        "切换文件",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question) != MessageBoxResult.Yes)
                {
                    OnPropertyChanged(nameof(SelectedFile));
                    return;
                }
            }

            if (SetProperty(ref _selectedFile, value))
                LoadFile();
        }
    }

    public PhraseEntry? SelectedEntry
    {
        get => _selectedEntry;
        set => SetProperty(ref _selectedEntry, value);
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

    public ICommand ReloadFilesCommand { get; }
    public ICommand AddCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand SaveAndDeployCommand { get; }
    public ICommand OpenFolderCommand { get; }

    public void Reload()
    {
        var selectedPath = SelectedFile?.Path;

        _suppress = true;
        Files.Clear();

        foreach (var path in _service.GetPhraseFiles())
            Files.Add(new EditableFile(Path.GetFileName(path), path));

        var selected = Files.FirstOrDefault(
            x => string.Equals(x.Path, selectedPath, StringComparison.OrdinalIgnoreCase));

        _selectedFile = selected ?? Files.FirstOrDefault();
        OnPropertyChanged(nameof(SelectedFile));

        _suppress = false;
        LoadFile();
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

    private void LoadFile()
    {
        foreach (var entry in Entries)
            entry.PropertyChanged -= OnEntryChanged;

        Entries.Clear();
        SelectedEntry = null;

        _suppress = true;

        if (_selectedFile is not null)
        {
            var (comments, entries) = _service.Load(_selectedFile.Path);
            _comments = comments;

            foreach (var entry in entries)
            {
                entry.PropertyChanged += OnEntryChanged;
                Entries.Add(entry);
            }

            StatusMessage = $"已加载 {_selectedFile.Name}，共 {Entries.Count} 条短语。";
        }
        else
        {
            _comments = new List<string>();
            StatusMessage = "没有可编辑的短语文件。";
        }

        _suppress = false;
        IsDirty = false;
    }

    private void Add()
    {
        var entry = new PhraseEntry();
        entry.PropertyChanged += OnEntryChanged;
        Entries.Add(entry);
        SelectedEntry = entry;
        IsDirty = true;
        StatusMessage = "已添加一行，填写短语和编码后保存。";
    }

    private void Delete()
    {
        if (SelectedEntry is null)
            return;

        SelectedEntry.PropertyChanged -= OnEntryChanged;
        Entries.Remove(SelectedEntry);
        SelectedEntry = Entries.FirstOrDefault();
        IsDirty = true;
        StatusMessage = "已删除一行，保存后生效。";
    }

    private async Task SaveAsync(bool deploy)
    {
        if (SelectedFile is null)
            return;

        try
        {
            _service.Save(SelectedFile.Path, _comments, Entries);

            IsDirty = false;
            StatusMessage = $"已保存 {SelectedFile.Name}，共 {Entries.Count} 条短语。";
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
                    ? "短语已保存并部署完成。"
                    : "短语已保存，但部署失败：" + result.Summary;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "保存失败：" + ex.Message;
            _setStatus(StatusMessage);
            throw;
        }
    }

    private void OnEntryChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suppress)
            return;

        IsDirty = true;
        StatusMessage = "有未保存的修改。";
    }
}
