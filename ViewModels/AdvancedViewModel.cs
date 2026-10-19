using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using WeaselStudio.Infrastructure;
using WeaselStudio.Models;
using WeaselStudio.Services;

namespace WeaselStudio.ViewModels;

public sealed class AdvancedViewModel : ObservableObject, IReloadable
{
    private readonly ConfigService _config;
    private readonly Func<Task<DeployResult>> _deploy;
    private readonly Action<string> _setStatus;

    private EditableFile? _selectedFile;
    private string _content = "";
    private bool _isDirty;
    private bool _loading;
    private string _statusMessage = "直接编辑 YAML；保存前会自动备份并校验语法。";
    private string _validationError = "";

    public AdvancedViewModel(
        ConfigService config,
        Func<Task<DeployResult>> deploy,
        Action<string> setStatus)
    {
        _config = config;
        _deploy = deploy;
        _setStatus = setStatus;

        Files = new ObservableCollection<EditableFile>();

        ReloadFilesCommand = new RelayCommand(ReloadFiles);
        ReloadContentCommand = new RelayCommand(ReloadContentWithConfirm);
        SaveCommand = new AsyncRelayCommand(() => SaveAsync(false));
        SaveAndDeployCommand = new AsyncRelayCommand(() => SaveAsync(true));
        OpenFolderCommand = new RelayCommand(
            () => ShellService.OpenFileLocation(SelectedFile?.Path));
    }

    public ObservableCollection<EditableFile> Files { get; }

    public EditableFile? SelectedFile
    {
        get => _selectedFile;
        set
        {
            if (!SetProperty(ref _selectedFile, value))
                return;

            LoadContent();
        }
    }

    public string Content
    {
        get => _content;
        set
        {
            if (!SetProperty(ref _content, value) || _loading)
                return;

            IsDirty = true;
            StatusMessage = "有未保存的修改。";
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

    public string ValidationError
    {
        get => _validationError;
        private set => SetProperty(ref _validationError, value);
    }

    public ICommand ReloadFilesCommand { get; }
    public ICommand ReloadContentCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand SaveAndDeployCommand { get; }
    public ICommand OpenFolderCommand { get; }

    public void Reload()
    {
        ReloadFiles();
    }

    private void ReloadFiles()
    {
        var selectedPath = SelectedFile?.Path;

        Files.Clear();
        foreach (var path in _config.GetEditableFiles())
        {
            var name = Path.GetRelativePath(_config.UserDataDir, path);
            Files.Add(new EditableFile(name, path));
        }

        var selected = Files.FirstOrDefault(
            x => string.Equals(x.Path, selectedPath, StringComparison.OrdinalIgnoreCase));

        SelectedFile = selected ?? Files.FirstOrDefault();

        if (Files.Count == 0)
        {
            _loading = true;
            Content = "";
            _loading = false;
            StatusMessage = "没有找到可编辑的 *.custom.yaml。";
        }
    }

    private void LoadContent()
    {
        if (SelectedFile is null)
        {
            _loading = true;
            Content = "";
            _loading = false;
            return;
        }

        _loading = true;
        Content = _config.ReadText(SelectedFile.Path);
        _loading = false;

        IsDirty = false;
        ValidationError = "";
        StatusMessage = $"已加载 {SelectedFile.Name}。";
    }

    private void ReloadContentWithConfirm()
    {
        if (IsDirty &&
            MessageBox.Show(
                "当前文件有未保存的修改，重新加载会丢弃它们。继续吗？",
                "重新加载",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        LoadContent();
    }

    private async Task SaveAsync(bool deploy)
    {
        if (SelectedFile is null)
            return;

        if (!YamlConfig.TryParseMap(Content, out _, out var error))
        {
            ValidationError = error ?? "YAML 语法无效。";
            StatusMessage = "YAML 语法校验失败，未写入文件。";
            _setStatus(StatusMessage);
            MessageBox.Show(
                ValidationError,
                "YAML 语法错误",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        try
        {
            _config.WriteText(SelectedFile.Path, Content);
            ValidationError = "";
            IsDirty = false;
            StatusMessage = $"已保存 {SelectedFile.Name}。";
            _setStatus(StatusMessage);

            if (deploy)
            {
                StatusMessage = $"已保存 {SelectedFile.Name}，正在部署…";
                _setStatus(StatusMessage);
                var result = await _deploy();
                StatusMessage = result.Success
                    ? "文件已保存并部署完成。"
                    : "文件已保存，但部署失败：" + result.Summary;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "保存失败：" + ex.Message;
            _setStatus(StatusMessage);
            throw;
        }
    }
}
