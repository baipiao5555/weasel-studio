using System.Globalization;
using System.Windows;
using System.Windows.Input;
using WeaselStudio.Infrastructure;
using WeaselStudio.Services;

namespace WeaselStudio.ViewModels;

public sealed class ClipboardPanelViewModel : ObservableObject, IReloadable
{
    private const string DefaultHotkey = "Control+Shift+V";
    private readonly ConfigService _config;
    private readonly Func<Task<DeployResult>> _deploy;
    private readonly Action<string> _setStatus;

    private bool _suppress;
    private bool _isDirty;
    private bool _enabled = true;
    private int _historyLimit = 100;
    private int _pageSize = 8;
    private int _maxTextLength = 262144;
    private string _hotkey = DefaultHotkey;
    private string _statusMessage = "设置会写入 weasel.custom.yaml 的 clipboard/* 节点。";

    public ClipboardPanelViewModel(
        ConfigService config,
        Func<Task<DeployResult>> deploy,
        Action<string> setStatus)
    {
        _config = config;
        _deploy = deploy;
        _setStatus = setStatus;

        ReloadCommand = new RelayCommand(ReloadWithConfirm);
        SaveCommand = new AsyncRelayCommand(() => SaveAsync(false));
        SaveAndDeployCommand = new AsyncRelayCommand(() => SaveAsync(true));
        OpenConfigCommand = new RelayCommand(
            () => ShellService.OpenFileLocation(_config.WeaselCustomPath));
    }

    public bool Enabled
    {
        get => _enabled;
        set { if (SetProperty(ref _enabled, value)) MarkDirty(); }
    }

    public int HistoryLimit
    {
        get => _historyLimit;
        set { if (SetProperty(ref _historyLimit, value)) MarkDirty(); }
    }

    public int PageSize
    {
        get => _pageSize;
        set { if (SetProperty(ref _pageSize, value)) MarkDirty(); }
    }

    public int MaxTextLength
    {
        get => _maxTextLength;
        set { if (SetProperty(ref _maxTextLength, value)) MarkDirty(); }
    }

    public string Hotkey
    {
        get => _hotkey;
        set { if (SetProperty(ref _hotkey, value)) MarkDirty(); }
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
    public ICommand OpenConfigCommand { get; }

    public void Reload()
    {
        _suppress = true;
        var readFailed = false;
        try
        {
            var patch = _config.LoadPatch(_config.WeaselCustomPath);
            Enabled = YamlConfig.AsBool(
                YamlConfig.GetPatch(patch, "clipboard/enabled")) ?? true;
            HistoryLimit = Math.Clamp(
                YamlConfig.AsInt(
                    YamlConfig.GetPatch(patch, "clipboard/history_limit")) ?? 100,
                1, 1000);
            PageSize = Math.Clamp(
                YamlConfig.AsInt(
                    YamlConfig.GetPatch(patch, "clipboard/page_size")) ?? 8,
                1, 8);
            MaxTextLength = Math.Clamp(
                YamlConfig.AsInt(
                    YamlConfig.GetPatch(patch, "clipboard/max_text_length")) ?? 262144,
                1024, 4194304);
            Hotkey = YamlConfig.AsString(
                YamlConfig.GetPatch(patch, "clipboard/hotkey"))?.Trim() is { Length: > 0 } value
                ? value
                : DefaultHotkey;
        }
        catch (Exception ex)
        {
            readFailed = true;
            StatusMessage = "读取配置失败：" + ex.Message;
        }
        finally
        {
            _suppress = false;
            IsDirty = false;
        }

        if (!readFailed)
            StatusMessage = Enabled ? "已加载设置。" : "内置剪贴板面板未启用。";
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

    private async Task SaveAsync(bool deploy)
    {
        var error = Validate();
        if (error is not null)
        {
            StatusMessage = error;
            _setStatus(error);
            MessageBox.Show(error, "内置剪贴板", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var patch = new PatchFile(_config.WeaselCustomPath);
            patch.SetValue("clipboard/enabled", PatchFile.FormatBool(Enabled));
            patch.SetValue(
                "clipboard/history_limit",
                HistoryLimit.ToString(CultureInfo.InvariantCulture));
            patch.SetValue(
                "clipboard/page_size",
                PageSize.ToString(CultureInfo.InvariantCulture));
            patch.SetValue(
                "clipboard/max_text_length",
                MaxTextLength.ToString(CultureInfo.InvariantCulture));
            patch.SetValue("clipboard/hotkey", PatchFile.FormatScalar(Hotkey.Trim()));
            patch.Save();

            IsDirty = false;
            StatusMessage = "内置剪贴板设置已保存。";
            _setStatus(StatusMessage);

            if (!deploy)
                return;

            if (_config.Environment.IsCustomUserDir)
            {
                StatusMessage = "已保存（测试模式，不执行部署）。";
                _setStatus(StatusMessage);
                return;
            }

            StatusMessage = "已保存，正在部署…";
            _setStatus(StatusMessage);
            var result = await _deploy();
            StatusMessage = result.Success
                ? "已保存并部署完成。"
                : "已保存，但部署失败：" + result.Summary;
            _setStatus(StatusMessage);
        }
        catch (Exception ex)
        {
            StatusMessage = "保存失败：" + ex.Message;
            _setStatus(StatusMessage);
            throw;
        }
    }

    private string? Validate()
    {
        if (HistoryLimit is < 1 or > 1000)
            return "历史上限必须在 1–1000 之间。";
        if (PageSize is < 1 or > 8)
            return "每页条数必须在 1–8 之间。";
        if (MaxTextLength is < 1024 or > 4194304)
            return "文本上限必须在 1024–4194304 之间。";
        return ValidateHotkey(Hotkey);
    }

    private static string? ValidateHotkey(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "快捷键不能为空。";

        var tokens = text
            .Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length < 2)
            return "快捷键至少需要一个修饰键和一个主键，例如 Control+Shift+V。";

        var hasModifier = false;
        var hasKey = false;
        foreach (var token in tokens)
        {
            var lower = token.ToLowerInvariant();
            if (lower is "ctrl" or "control" or "shift" or "alt")
            {
                hasModifier = true;
                continue;
            }

            if (hasKey)
                return "快捷键只能包含一个主键。";

            var validKey = token.Length == 1 && char.IsLetterOrDigit(token[0]) ||
                           lower is "space" or "tab" or "enter";
            if (!validKey)
                return "主键只支持字母、数字、Space、Tab 或 Enter。";

            hasKey = true;
        }

        if (!hasModifier)
            return "快捷键至少需要一个 Ctrl、Shift 或 Alt。";
        if (!hasKey)
            return "快捷键缺少主键，例如 V。";
        return null;
    }

    private void MarkDirty()
    {
        if (_suppress)
            return;

        IsDirty = true;
        StatusMessage = "有未保存的修改。";
    }
}
