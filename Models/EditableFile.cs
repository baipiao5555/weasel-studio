namespace WeaselStudio.Models;

public sealed record EditableFile(string Name, string Path)
{
    public override string ToString() => Name;
}
