namespace WeaselStudio.Models;

public sealed class ThemeFile
{
    public int Version { get; set; } = 1;
    public string Name { get; set; } = "自定义主题";
    public string ColorScheme { get; set; } = "";
    public string ColorSchemeDark { get; set; } = "";
    public bool Horizontal { get; set; }
    public bool VerticalText { get; set; }
    public string FontFace { get; set; } = "";
    public double FontPoint { get; set; }
    public string LabelFontFace { get; set; } = "";
    public double LabelFontPoint { get; set; }
    public string CommentFontFace { get; set; } = "";
    public double CommentFontPoint { get; set; }
    public bool InlinePreedit { get; set; }
    public string PreeditType { get; set; } = "composition";
    public int CornerRadius { get; set; }
    public int BorderWidth { get; set; }
    public int MarginX { get; set; }
    public int MarginY { get; set; }
    public int Spacing { get; set; }
    public int CandidateSpacing { get; set; }
    public int HilitePadding { get; set; }
    public int ShadowRadius { get; set; }
    public int ShadowOffsetX { get; set; }
    public int ShadowOffsetY { get; set; }
    public int MaxHeight { get; set; }
    public int MinWidth { get; set; }
    public int CandidateAbbreviateLength { get; set; }
}
