using WeaselStudio.Infrastructure;

namespace WeaselStudio.Models;

public sealed class SchemaSwitch : ObservableObject
{
    private int _reset;

    public string Name { get; set; } = "";
    public List<string> States { get; set; } = new();

    public int Reset
    {
        get => _reset;
        set => SetProperty(ref _reset, value);
    }

    public string DisplayName => FriendlyName(Name);

    public bool HasStates => States.Count > 0;

    public IReadOnlyList<string> DisplayStates => Name switch
    {
        "emoji_suggestion" => new[] { "关闭（😣️）", "开启（😁️）" },
        "ascii_punct" => new[] { "中文标点（。，）", "英文标点（．，）" },
        _ => States
    };

    public string HintText => States.Count switch
    {
        0 => "该开关没有 states，可在高级 YAML 中编辑。",
        1 => $"当前只有一种状态：{States[0]}",
        _ => "选择要作为默认值的状态。"
    };

    public static string FriendlyName(string name) => name switch
    {
        "ascii_mode" => "中英文模式",
        "emoji_suggestion" => "Emoji 候选",
        "full_shape" => "全角 / 半角",
        "tone_display" => "声调显示",
        "transcription" => "简繁转换",
        "ascii_punct" => "标点风格",
        _ => name
    };
}
