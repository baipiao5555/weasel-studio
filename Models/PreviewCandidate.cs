using System.Windows.Media;

namespace WeaselStudio.Models;

public sealed class PreviewCandidate
{
    public string Label { get; init; } = "";
    public string Text { get; init; } = "";
    public string Comment { get; init; } = "";
    public bool IsHighlighted { get; init; }

    public Brush Background { get; init; } = Brushes.Transparent;
    public Brush Foreground { get; init; } = Brushes.Black;
    public Brush LabelForeground { get; init; } = Brushes.Gray;
    public Brush CommentForeground { get; init; } = Brushes.Gray;
}
