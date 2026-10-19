using WeaselStudio.Infrastructure;

namespace WeaselStudio.Models;

public sealed class SchemaInfo : ObservableObject
{
    private bool _isEnabled;

    public required string Id { get; init; }
    public string Name { get; init; } = "";
    public string Version { get; init; } = "";
    public string Author { get; init; } = "";
    public string Description { get; init; } = "";
    public string FilePath { get; init; } = "";
    public bool IsMissing { get; init; }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Id : Name;

    public string MetaText
    {
        get
        {
            var parts = new[] { Id, Version, Author }
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToArray();
            return string.Join(" · ", parts);
        }
    }

    public string DescriptionText =>
        string.IsNullOrWhiteSpace(Description) ? "（该方案没有提供描述）" : Description;
}
