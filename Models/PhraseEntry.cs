using WeaselStudio.Infrastructure;

namespace WeaselStudio.Models;

public sealed class PhraseEntry : ObservableObject
{
    private string _text = "";
    private string _code = "";
    private string _weight = "";

    public string Text
    {
        get => _text;
        set => SetProperty(ref _text, value);
    }

    public string Code
    {
        get => _code;
        set => SetProperty(ref _code, value);
    }

    public string Weight
    {
        get => _weight;
        set => SetProperty(ref _weight, value);
    }
}
