namespace WeaselStudio.Models;

public sealed record ThemePreset(
    string Name,
    string Description,
    string ColorScheme,
    string DarkScheme,
    bool Horizontal,
    bool VerticalText,
    string FontFace,
    double FontPoint,
    int CornerRadius,
    int BorderWidth,
    int MarginX,
    int MarginY,
    int Spacing,
    int CandidateSpacing,
    int HilitePadding,
    int ShadowRadius);
