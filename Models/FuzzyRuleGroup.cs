using WeaselStudio.Infrastructure;

namespace WeaselStudio.Models;

public sealed class FuzzyRuleGroup : ObservableObject
{
    private bool _isEnabled;

    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public IReadOnlyList<string> Rules { get; init; } = Array.Empty<string>();

    public bool IsBuiltIn { get; set; }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    public bool CanToggle => !IsBuiltIn;

    public string StateText => IsBuiltIn
        ? "方案自带"
        : IsEnabled ? "已启用" : "未启用";
}
