using System.Windows.Media;
using WeaselStudio.Services;

namespace WeaselStudio.Models;

public sealed class ColorSchemeInfo
{
    public string Id { get; init; } = "";
    public string Name { get; set; } = "";
    public string Author { get; set; } = "";
    public string ColorFormat { get; set; } = "abgr";

    public string BackColor { get; set; } = "";
    public string TextColor { get; set; } = "";
    public string BorderColor { get; set; } = "";
    public string LabelColor { get; set; } = "";
    public string CommentTextColor { get; set; } = "";
    public string CandidateBackColor { get; set; } = "";
    public string HilitedCandidateBackColor { get; set; } = "";
    public string HilitedCandidateTextColor { get; set; } = "";
    public string HilitedCandidateLabelColor { get; set; } = "";
    public string HilitedCandidateCommentColor { get; set; } = "";
    public string ShadowColor { get; set; } = "";

    public Color PreviewBack { get; private set; } = Colors.White;
    public Color PreviewText { get; private set; } = Colors.Black;
    public Color PreviewBorder { get; private set; } = Color.FromRgb(0xE4, 0xE7, 0xEC);
    public Color PreviewLabel { get; private set; } = Colors.Black;
    public Color PreviewComment { get; private set; } = Color.FromRgb(0x66, 0x70, 0x85);
    public Color PreviewCandidateBack { get; private set; } = Colors.Transparent;
    public Color PreviewAccent { get; private set; } = Color.FromRgb(0x2F, 0x6B, 0xFF);
    public Color PreviewAccentText { get; private set; } = Colors.White;

    public string DisplayName =>
        string.IsNullOrWhiteSpace(Name) ? Id : $"{Name}  ({Id})";

    public void SetProperty(string property, string value)
    {
        switch (property)
        {
            case "name": Name = value; break;
            case "author": Author = value; break;
            case "color_format": ColorFormat = value; break;
            case "back_color": BackColor = value; break;
            case "text_color": TextColor = value; break;
            case "border_color": BorderColor = value; break;
            case "label_color": LabelColor = value; break;
            case "comment_text_color": CommentTextColor = value; break;
            case "candidate_back_color": CandidateBackColor = value; break;
            case "hilited_candidate_back_color": HilitedCandidateBackColor = value; break;
            case "hilited_candidate_text_color": HilitedCandidateTextColor = value; break;
            case "hilited_candidate_label_color": HilitedCandidateLabelColor = value; break;
            case "hilited_candidate_comment_color": HilitedCandidateCommentColor = value; break;
            case "shadow_color": ShadowColor = value; break;
        }
    }

    public void Merge(IReadOnlyDictionary<string, string> values)
    {
        foreach (var (key, value) in values)
            SetProperty(key, value);
    }

    public void RecomputePreview()
    {
        var back = RimeColorParser.Parse(BackColor, ColorFormat);
        if (back.A == 0)
            back = Colors.White;

        var text = RimeColorParser.Parse(TextColor, ColorFormat);
        if (text.A == 0)
            text = Colors.Black;

        var border = RimeColorParser.Parse(BorderColor, ColorFormat);
        if (border.A == 0)
            border = Color.FromRgb(0xE4, 0xE7, 0xEC);

        var label = RimeColorParser.Parse(LabelColor, ColorFormat);
        if (label.A == 0)
            label = text;

        var comment = RimeColorParser.Parse(CommentTextColor, ColorFormat);
        if (comment.A == 0)
            comment = label;

        var candidateBack = RimeColorParser.Parse(CandidateBackColor, ColorFormat);
        if (candidateBack.A == 0)
            candidateBack = back;

        var accent = RimeColorParser.Parse(HilitedCandidateBackColor, ColorFormat);
        if (accent.A == 0)
            accent = Color.FromRgb(0x2F, 0x6B, 0xFF);

        var accentText = RimeColorParser.Parse(HilitedCandidateTextColor, ColorFormat);
        if (accentText.A == 0)
            accentText = Colors.White;

        PreviewBack = back;
        PreviewText = text;
        PreviewBorder = border;
        PreviewLabel = label;
        PreviewComment = comment;
        PreviewCandidateBack = candidateBack;
        PreviewAccent = accent;
        PreviewAccentText = accentText;
    }
}
