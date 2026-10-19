namespace WeaselStudio.Models;

public sealed class StyleSettings
{
    public string ColorScheme { get; set; } = "default";
    public string ColorSchemeDark { get; set; } = "";
    public bool Horizontal { get; set; }
    public bool VerticalText { get; set; }

    public string FontFace { get; set; } = "Microsoft YaHei";
    public double FontPoint { get; set; } = 12;
    public string LabelFontFace { get; set; } = "Microsoft YaHei";
    public double LabelFontPoint { get; set; } = 12;
    public string CommentFontFace { get; set; } = "Microsoft YaHei";
    public double CommentFontPoint { get; set; } = 10;

    public bool InlinePreedit { get; set; }
    public string PreeditType { get; set; } = "composition";

    public int CornerRadius { get; set; } = 8;
    public int BorderWidth { get; set; }
    public int MarginX { get; set; } = 8;
    public int MarginY { get; set; } = 8;
    public int Spacing { get; set; } = 10;
    public int CandidateSpacing { get; set; } = 22;
    public int HilitePadding { get; set; } = 8;
    public int ShadowRadius { get; set; }
    public int ShadowOffsetX { get; set; } = 4;
    public int ShadowOffsetY { get; set; } = 4;
    public int MaxHeight { get; set; } = 600;
    public int MinWidth { get; set; } = 145;
    public int CandidateAbbreviateLength { get; set; } = 30;

    public StyleSettings Clone() => (StyleSettings)MemberwiseClone();

    public bool ValueEquals(StyleSettings other)
    {
        return ColorScheme == other.ColorScheme
            && ColorSchemeDark == other.ColorSchemeDark
            && Horizontal == other.Horizontal
            && VerticalText == other.VerticalText
            && FontFace == other.FontFace
            && FontPoint.Equals(other.FontPoint)
            && LabelFontFace == other.LabelFontFace
            && LabelFontPoint.Equals(other.LabelFontPoint)
            && CommentFontFace == other.CommentFontFace
            && CommentFontPoint.Equals(other.CommentFontPoint)
            && InlinePreedit == other.InlinePreedit
            && PreeditType == other.PreeditType
            && CornerRadius == other.CornerRadius
            && BorderWidth == other.BorderWidth
            && MarginX == other.MarginX
            && MarginY == other.MarginY
            && Spacing == other.Spacing
            && CandidateSpacing == other.CandidateSpacing
            && HilitePadding == other.HilitePadding
            && ShadowRadius == other.ShadowRadius
            && ShadowOffsetX == other.ShadowOffsetX
            && ShadowOffsetY == other.ShadowOffsetY
            && MaxHeight == other.MaxHeight
            && MinWidth == other.MinWidth
            && CandidateAbbreviateLength == other.CandidateAbbreviateLength;
    }
}
