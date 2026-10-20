using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using WeaselStudio.Infrastructure;
using WeaselStudio.Models;
using WeaselStudio.Services;

namespace WeaselStudio.ViewModels;

public sealed class AppearanceViewModel : ObservableObject, IReloadable
{
    private readonly ConfigService _config;
    private readonly Func<Task<DeployResult>> _deploy;
    private readonly Action<string> _setStatus;

    private StyleSettings _original = new();
    private bool _suppress;

    private string _selectedColorSchemeId = "";
    private string _selectedDarkColorSchemeId = "";
    private string _fontFace = "Microsoft YaHei";
    private double _fontPoint = 12;
    private string _labelFontFace = "Microsoft YaHei";
    private double _labelFontPoint = 12;
    private string _commentFontFace = "Microsoft YaHei";
    private double _commentFontPoint = 10;
    private string _selectedDirection = "vertical";
    private bool _inlinePreedit;
    private string _selectedPreeditType = "composition";
    private int _cornerRadius = 8;
    private int _borderWidth;
    private int _marginX = 8;
    private int _marginY = 8;
    private int _spacing = 10;
    private int _candidateSpacing = 22;
    private int _hilitePadding = 8;
    private int _shadowRadius;
    private int _shadowOffsetX = 4;
    private int _shadowOffsetY = 4;
    private int _maxHeight = 600;
    private int _minWidth = 145;
    private int _abbreviateLength = 30;

    private bool _isDirty;
    private string _statusMessage = "调整后点击保存，修改会写入 weasel.custom.yaml。";

    private Brush _previewBackground = Brushes.White;
    private Brush _previewText = Brushes.Black;
    private Brush _previewBorder = Brushes.Transparent;
    private Brush _previewLabel = Brushes.Gray;
    private Brush _previewComment = Brushes.Gray;
    private Brush _previewAccent = Brushes.DodgerBlue;
    private Brush _previewAccentText = Brushes.White;
    private Brush _previewCandidateBack = Brushes.Transparent;
    private CornerRadius _previewCornerRadius;
    private Thickness _previewBorderThickness;
    private Thickness _previewCandidateMargin;
    private string _previewFontFamily = "Microsoft YaHei";
    private double _previewFontSize = 16;
    private bool _isHorizontalPreview;

    public AppearanceViewModel(
        ConfigService config,
        Func<Task<DeployResult>> deploy,
        Action<string> setStatus)
    {
        _config = config;
        _deploy = deploy;
        _setStatus = setStatus;

        ColorSchemes = new ObservableCollection<ColorSchemeInfo>();
        DarkColorSchemes = new ObservableCollection<ColorSchemeInfo>();
        FontFamilies = new ObservableCollection<string>();
        PreviewCandidates = new ObservableCollection<PreviewCandidate>();

        Directions = new List<OptionItem>
        {
            new("vertical", "竖排（默认）"),
            new("horizontal", "横排"),
            new("vertical_text", "竖排文本")
        };

        PreeditTypes = new List<OptionItem>
        {
            new("composition", "编码（composition）"),
            new("preview", "选中候选（preview）"),
            new("preview_all", "全部候选（preview_all）")
        };

        ThemePresets = new List<ThemePreset>
        {
            new("Windows 11 浅色", "圆角、细边框、舒适间距",
                "mint_light_blue", "mint_dark_blue", false, false,
                "Microsoft YaHei", 12, 10, 1, 12, 8, 10, 18, 8, 0),
            new("Windows 11 深色", "深色配色，适合夜间",
                "mint_dark_blue", "mint_dark_blue", false, false,
                "Microsoft YaHei", 12, 10, 1, 12, 8, 10, 18, 8, 0),
            new("微信输入法风", "横排、大字号、紧凑",
                "mint_light_green", "mint_dark_green", true, false,
                "Microsoft YaHei", 14, 8, 0, 10, 6, 8, 16, 8, 0),
            new("极简代码风", "等宽字体、无边框、横排",
                "nord", "", true, false,
                "Cascadia Mono", 12, 4, 0, 8, 4, 6, 12, 6, 0)
        };

        LoadFontFamilies();

        ReloadCommand = new RelayCommand(ReloadWithConfirm);
        SaveCommand = new AsyncRelayCommand(() => SaveAsync(false));
        SaveAndDeployCommand = new AsyncRelayCommand(() => SaveAsync(true));
        ApplyThemePresetCommand = new RelayCommand(parameter =>
            ApplyThemePreset(parameter as ThemePreset));
    }

    public ObservableCollection<ColorSchemeInfo> ColorSchemes { get; }
    public ObservableCollection<ColorSchemeInfo> DarkColorSchemes { get; }
    public ObservableCollection<string> FontFamilies { get; }
    public ObservableCollection<PreviewCandidate> PreviewCandidates { get; }
    public IReadOnlyList<OptionItem> Directions { get; }
    public IReadOnlyList<OptionItem> PreeditTypes { get; }
    public IReadOnlyList<ThemePreset> ThemePresets { get; }

    public ICommand ReloadCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand SaveAndDeployCommand { get; }
    public ICommand ApplyThemePresetCommand { get; }

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

    public string SelectedColorSchemeId
    {
        get => _selectedColorSchemeId;
        set
        {
            if (!SetProperty(ref _selectedColorSchemeId, value))
                return;

            MarkDirty();
            if (!_suppress)
                RefreshPreview();
        }
    }

    public string SelectedDarkColorSchemeId
    {
        get => _selectedDarkColorSchemeId;
        set
        {
            if (SetProperty(ref _selectedDarkColorSchemeId, value))
                MarkDirty();
        }
    }

    public string FontFace
    {
        get => _fontFace;
        set
        {
            if (!SetProperty(ref _fontFace, value))
                return;

            MarkDirty();
            if (!_suppress)
                RefreshPreview();
        }
    }

    public double FontPoint
    {
        get => _fontPoint;
        set
        {
            if (!SetProperty(ref _fontPoint, value))
                return;

            MarkDirty();
            if (!_suppress)
                RefreshPreview();
        }
    }

    public string LabelFontFace
    {
        get => _labelFontFace;
        set
        {
            if (SetProperty(ref _labelFontFace, value))
                MarkDirty();
        }
    }

    public double LabelFontPoint
    {
        get => _labelFontPoint;
        set
        {
            if (SetProperty(ref _labelFontPoint, value))
                MarkDirty();
        }
    }

    public string CommentFontFace
    {
        get => _commentFontFace;
        set
        {
            if (SetProperty(ref _commentFontFace, value))
                MarkDirty();
        }
    }

    public double CommentFontPoint
    {
        get => _commentFontPoint;
        set
        {
            if (SetProperty(ref _commentFontPoint, value))
                MarkDirty();
        }
    }

    public string SelectedDirection
    {
        get => _selectedDirection;
        set
        {
            if (!SetProperty(ref _selectedDirection, value))
                return;

            MarkDirty();
            if (!_suppress)
                RefreshPreview();
        }
    }

    public bool InlinePreedit
    {
        get => _inlinePreedit;
        set
        {
            if (SetProperty(ref _inlinePreedit, value))
                MarkDirty();
        }
    }

    public string SelectedPreeditType
    {
        get => _selectedPreeditType;
        set
        {
            if (SetProperty(ref _selectedPreeditType, value))
                MarkDirty();
        }
    }

    public int CornerRadius
    {
        get => _cornerRadius;
        set
        {
            if (!SetProperty(ref _cornerRadius, value))
                return;

            MarkDirty();
            if (!_suppress)
                RefreshPreview();
        }
    }

    public int BorderWidth
    {
        get => _borderWidth;
        set
        {
            if (!SetProperty(ref _borderWidth, value))
                return;

            MarkDirty();
            if (!_suppress)
                RefreshPreview();
        }
    }

    public int MarginX
    {
        get => _marginX;
        set
        {
            if (SetProperty(ref _marginX, value))
                MarkDirty();
        }
    }

    public int MarginY
    {
        get => _marginY;
        set
        {
            if (SetProperty(ref _marginY, value))
                MarkDirty();
        }
    }

    public int Spacing
    {
        get => _spacing;
        set
        {
            if (SetProperty(ref _spacing, value))
                MarkDirty();
        }
    }

    public int CandidateSpacing
    {
        get => _candidateSpacing;
        set
        {
            if (!SetProperty(ref _candidateSpacing, value))
                return;

            MarkDirty();
            if (!_suppress)
                RefreshPreview();
        }
    }

    public int HilitePadding
    {
        get => _hilitePadding;
        set
        {
            if (SetProperty(ref _hilitePadding, value))
                MarkDirty();
        }
    }

    public int ShadowRadius
    {
        get => _shadowRadius;
        set
        {
            if (SetProperty(ref _shadowRadius, value))
                MarkDirty();
        }
    }

    public int ShadowOffsetX
    {
        get => _shadowOffsetX;
        set
        {
            if (SetProperty(ref _shadowOffsetX, value))
                MarkDirty();
        }
    }

    public int ShadowOffsetY
    {
        get => _shadowOffsetY;
        set
        {
            if (SetProperty(ref _shadowOffsetY, value))
                MarkDirty();
        }
    }

    public int MaxHeight
    {
        get => _maxHeight;
        set
        {
            if (SetProperty(ref _maxHeight, value))
                MarkDirty();
        }
    }

    public int MinWidth
    {
        get => _minWidth;
        set
        {
            if (SetProperty(ref _minWidth, value))
                MarkDirty();
        }
    }

    public int AbbreviateLength
    {
        get => _abbreviateLength;
        set
        {
            if (SetProperty(ref _abbreviateLength, value))
                MarkDirty();
        }
    }

    public Brush PreviewBackground => _previewBackground;
    public Brush PreviewText => _previewText;
    public Brush PreviewBorder => _previewBorder;
    public Brush PreviewLabel => _previewLabel;
    public Brush PreviewComment => _previewComment;
    public Brush PreviewAccent => _previewAccent;
    public Brush PreviewAccentText => _previewAccentText;
    public Brush PreviewCandidateBack => _previewCandidateBack;
    public CornerRadius PreviewCornerRadius => _previewCornerRadius;
    public Thickness PreviewBorderThickness => _previewBorderThickness;
    public Thickness PreviewCandidateMargin => _previewCandidateMargin;
    public string PreviewFontFamily => _previewFontFamily;
    public double PreviewFontSize => _previewFontSize;
    public bool IsHorizontalPreview => _isHorizontalPreview;
    public bool IsVerticalPreview => !_isHorizontalPreview;

    public void Reload()
    {
        _suppress = true;

        var style = _config.LoadStyle();
        var schemes = _config.LoadColorSchemes();

        ColorSchemes.Clear();
        foreach (var scheme in schemes)
            ColorSchemes.Add(scheme);

        DarkColorSchemes.Clear();
        DarkColorSchemes.Add(new ColorSchemeInfo { Id = "", Name = "（跟随浅色方案）" });
        foreach (var scheme in schemes)
            DarkColorSchemes.Add(scheme);

        if (ColorSchemes.Count == 0)
        {
            var fallback = new ColorSchemeInfo { Id = "default", Name = "默认" };
            fallback.RecomputePreview();
            ColorSchemes.Add(fallback);
            DarkColorSchemes.Add(fallback);
        }

        SelectedColorSchemeId = ColorSchemes.Any(x => x.Id == style.ColorScheme)
            ? style.ColorScheme
            : ColorSchemes[0].Id;

        SelectedDarkColorSchemeId = string.IsNullOrWhiteSpace(style.ColorSchemeDark)
            ? ""
            : DarkColorSchemes.Any(x => x.Id == style.ColorSchemeDark)
                ? style.ColorSchemeDark
                : "";

        FontFace = style.FontFace;
        FontPoint = style.FontPoint;
        LabelFontFace = style.LabelFontFace;
        LabelFontPoint = style.LabelFontPoint;
        CommentFontFace = style.CommentFontFace;
        CommentFontPoint = style.CommentFontPoint;
        SelectedDirection = style.VerticalText
            ? "vertical_text"
            : style.Horizontal ? "horizontal" : "vertical";
        InlinePreedit = style.InlinePreedit;
        SelectedPreeditType = PreeditTypes.Any(x => x.Value == style.PreeditType)
            ? style.PreeditType
            : "composition";
        CornerRadius = style.CornerRadius;
        BorderWidth = style.BorderWidth;
        MarginX = style.MarginX;
        MarginY = style.MarginY;
        Spacing = style.Spacing;
        CandidateSpacing = style.CandidateSpacing;
        HilitePadding = style.HilitePadding;
        ShadowRadius = style.ShadowRadius;
        ShadowOffsetX = style.ShadowOffsetX;
        ShadowOffsetY = style.ShadowOffsetY;
        MaxHeight = style.MaxHeight;
        MinWidth = style.MinWidth;
        AbbreviateLength = style.CandidateAbbreviateLength;

        _original = style.Clone();
        _suppress = false;

        IsDirty = false;
        StatusMessage = $"已加载 {ColorSchemes.Count} 个配色方案。";
        RefreshPreview();
    }

    private void ApplyThemePreset(ThemePreset? preset)
    {
        if (preset is null)
            return;

        if (ColorSchemes.Any(x => x.Id == preset.ColorScheme))
            SelectedColorSchemeId = preset.ColorScheme;

        SelectedDarkColorSchemeId = string.IsNullOrWhiteSpace(preset.DarkScheme) ||
                                    DarkColorSchemes.Any(x => x.Id == preset.DarkScheme)
            ? preset.DarkScheme
            : "";

        SelectedDirection = preset.Horizontal
            ? "horizontal"
            : preset.VerticalText ? "vertical_text" : "vertical";

        FontFace = preset.FontFace;
        FontPoint = preset.FontPoint;
        CornerRadius = preset.CornerRadius;
        BorderWidth = preset.BorderWidth;
        MarginX = preset.MarginX;
        MarginY = preset.MarginY;
        Spacing = preset.Spacing;
        CandidateSpacing = preset.CandidateSpacing;
        HilitePadding = preset.HilitePadding;
        ShadowRadius = preset.ShadowRadius;

        MarkDirty();
        RefreshPreview();
        StatusMessage = $"已应用主题预设「{preset.Name}」，保存并部署后生效。";
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
        var current = BuildSettings();

        if (string.IsNullOrWhiteSpace(current.ColorScheme))
            current.ColorScheme = _original.ColorScheme;

        var patch = new PatchFile(_config.WeaselCustomPath);

        ApplyString(patch, "style/color_scheme", current.ColorScheme, _original.ColorScheme);
        ApplyString(patch, "style/color_scheme_dark", current.ColorSchemeDark, _original.ColorSchemeDark);
        ApplyBool(patch, "style/horizontal", current.Horizontal, _original.Horizontal);
        ApplyBool(patch, "style/vertical_text", current.VerticalText, _original.VerticalText);
        ApplyString(patch, "style/font_face", current.FontFace, _original.FontFace);
        ApplyDouble(patch, "style/font_point", current.FontPoint, _original.FontPoint);
        ApplyString(patch, "style/label_font_face", current.LabelFontFace, _original.LabelFontFace);
        ApplyDouble(patch, "style/label_font_point", current.LabelFontPoint, _original.LabelFontPoint);
        ApplyString(patch, "style/comment_font_face", current.CommentFontFace, _original.CommentFontFace);
        ApplyDouble(patch, "style/comment_font_point", current.CommentFontPoint, _original.CommentFontPoint);
        ApplyBool(patch, "style/inline_preedit", current.InlinePreedit, _original.InlinePreedit);
        ApplyString(patch, "style/preedit_type", current.PreeditType, _original.PreeditType);
        ApplyInt(patch, "style/layout/corner_radius", current.CornerRadius, _original.CornerRadius);
        ApplyInt(patch, "style/layout/border_width", current.BorderWidth, _original.BorderWidth);
        ApplyInt(patch, "style/layout/margin_x", current.MarginX, _original.MarginX);
        ApplyInt(patch, "style/layout/margin_y", current.MarginY, _original.MarginY);
        ApplyInt(patch, "style/layout/spacing", current.Spacing, _original.Spacing);
        ApplyInt(patch, "style/layout/candidate_spacing", current.CandidateSpacing, _original.CandidateSpacing);
        ApplyInt(patch, "style/layout/hilite_padding", current.HilitePadding, _original.HilitePadding);
        ApplyInt(patch, "style/layout/shadow_radius", current.ShadowRadius, _original.ShadowRadius);
        ApplyInt(patch, "style/layout/shadow_offset_x", current.ShadowOffsetX, _original.ShadowOffsetX);
        ApplyInt(patch, "style/layout/shadow_offset_y", current.ShadowOffsetY, _original.ShadowOffsetY);
        ApplyInt(patch, "style/layout/max_height", current.MaxHeight, _original.MaxHeight);
        ApplyInt(patch, "style/layout/min_width", current.MinWidth, _original.MinWidth);
        ApplyInt(patch, "style/candidate_abbreviate_length", current.CandidateAbbreviateLength, _original.CandidateAbbreviateLength);

        try
        {
            patch.Save();
            _original = current.Clone();
            IsDirty = false;
            StatusMessage = "外观设置已保存。";
            _setStatus(StatusMessage);

            if (deploy)
            {
                StatusMessage = "外观设置已保存，正在部署…";
                _setStatus(StatusMessage);
                var result = await _deploy();
                StatusMessage = result.Success
                    ? "外观设置已保存并部署完成。"
                    : "外观设置已保存，但部署失败：" + result.Summary;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "保存失败：" + ex.Message;
            _setStatus(StatusMessage);
            throw;
        }
    }

    private StyleSettings BuildSettings() => new()
    {
        ColorScheme = SelectedColorSchemeId,
        ColorSchemeDark = SelectedDarkColorSchemeId,
        Horizontal = SelectedDirection == "horizontal",
        VerticalText = SelectedDirection == "vertical_text",
        FontFace = FontFace,
        FontPoint = FontPoint,
        LabelFontFace = LabelFontFace,
        LabelFontPoint = LabelFontPoint,
        CommentFontFace = CommentFontFace,
        CommentFontPoint = CommentFontPoint,
        InlinePreedit = InlinePreedit,
        PreeditType = SelectedPreeditType,
        CornerRadius = CornerRadius,
        BorderWidth = BorderWidth,
        MarginX = MarginX,
        MarginY = MarginY,
        Spacing = Spacing,
        CandidateSpacing = CandidateSpacing,
        HilitePadding = HilitePadding,
        ShadowRadius = ShadowRadius,
        ShadowOffsetX = ShadowOffsetX,
        ShadowOffsetY = ShadowOffsetY,
        MaxHeight = MaxHeight,
        MinWidth = MinWidth,
        CandidateAbbreviateLength = AbbreviateLength
    };

    private void RefreshPreview()
    {
        var scheme = ColorSchemes.FirstOrDefault(x => x.Id == SelectedColorSchemeId)
                     ?? ColorSchemes.FirstOrDefault();

        if (scheme is not null)
        {
            _previewBackground = Brush(scheme.PreviewBack);
            _previewText = Brush(scheme.PreviewText);
            _previewBorder = Brush(scheme.PreviewBorder);
            _previewLabel = Brush(scheme.PreviewLabel);
            _previewComment = Brush(scheme.PreviewComment);
            _previewAccent = Brush(scheme.PreviewAccent);
            _previewAccentText = Brush(scheme.PreviewAccentText);
            _previewCandidateBack = Brush(scheme.PreviewCandidateBack);
        }

        _previewCornerRadius = new CornerRadius(Math.Max(0, CornerRadius));
        _previewBorderThickness = new Thickness(Math.Max(0, BorderWidth));
        _previewCandidateMargin = new Thickness(
            0, 0, Math.Max(0, CandidateSpacing), Math.Max(0, CandidateSpacing / 2.0));
        _previewFontFamily = string.IsNullOrWhiteSpace(FontFace) ? "Microsoft YaHei" : FontFace;
        _previewFontSize = Math.Max(8, FontPoint * 4.0 / 3.0);
        _isHorizontalPreview = SelectedDirection == "horizontal";

        PreviewCandidates.Clear();
        PreviewCandidates.Add(new PreviewCandidate
        {
            Label = "1",
            Text = "小狼毫",
            Comment = "Weasel",
            IsHighlighted = true,
            Background = _previewAccent,
            Foreground = _previewAccentText,
            LabelForeground = _previewAccentText,
            CommentForeground = _previewAccentText
        });
        PreviewCandidates.Add(new PreviewCandidate
        {
            Label = "2",
            Text = "设置中心",
            Comment = "Studio",
            Background = _previewCandidateBack,
            Foreground = _previewText,
            LabelForeground = _previewLabel,
            CommentForeground = _previewComment
        });
        PreviewCandidates.Add(new PreviewCandidate
        {
            Label = "3",
            Text = "候选预览",
            Comment = "Preview",
            Background = _previewCandidateBack,
            Foreground = _previewText,
            LabelForeground = _previewLabel,
            CommentForeground = _previewComment
        });

        OnPropertyChanged(nameof(PreviewBackground));
        OnPropertyChanged(nameof(PreviewText));
        OnPropertyChanged(nameof(PreviewBorder));
        OnPropertyChanged(nameof(PreviewLabel));
        OnPropertyChanged(nameof(PreviewComment));
        OnPropertyChanged(nameof(PreviewAccent));
        OnPropertyChanged(nameof(PreviewAccentText));
        OnPropertyChanged(nameof(PreviewCandidateBack));
        OnPropertyChanged(nameof(PreviewCornerRadius));
        OnPropertyChanged(nameof(PreviewBorderThickness));
        OnPropertyChanged(nameof(PreviewCandidateMargin));
        OnPropertyChanged(nameof(PreviewFontFamily));
        OnPropertyChanged(nameof(PreviewFontSize));
        OnPropertyChanged(nameof(IsHorizontalPreview));
        OnPropertyChanged(nameof(IsVerticalPreview));
    }

    private void LoadFontFamilies()
    {
        var names = Fonts.SystemFontFamilies
            .Select(x => x.Source)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase);

        foreach (var name in names)
            FontFamilies.Add(name);
    }

    private void MarkDirty()
    {
        if (_suppress)
            return;

        IsDirty = true;
        StatusMessage = "有未保存的修改。";
    }

    private static SolidColorBrush Brush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static void ApplyString(PatchFile patch, string path, string value, string oldValue)
    {
        if (string.Equals(value, oldValue, StringComparison.Ordinal))
            return;

        if (string.IsNullOrWhiteSpace(value))
            patch.RemoveValue(path);
        else
            patch.SetValue(path, PatchFile.Quote(value));
    }

    private static void ApplyBool(PatchFile patch, string path, bool value, bool oldValue)
    {
        if (value != oldValue)
            patch.SetValue(path, PatchFile.FormatBool(value));
    }

    private static void ApplyInt(PatchFile patch, string path, int value, int oldValue)
    {
        if (value != oldValue)
            patch.SetValue(path, PatchFile.FormatInt(value));
    }

    private static void ApplyDouble(PatchFile patch, string path, double value, double oldValue)
    {
        if (Math.Abs(value - oldValue) > 0.0001)
            patch.SetValue(path, PatchFile.FormatDouble(value));
    }
}
