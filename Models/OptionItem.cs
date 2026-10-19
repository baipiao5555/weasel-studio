namespace WeaselStudio.Models;

public sealed record OptionItem(string Value, string Label)
{
    public override string ToString() => Label;
}
