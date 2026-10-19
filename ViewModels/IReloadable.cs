namespace WeaselStudio.ViewModels;

public interface IReloadable
{
    bool IsDirty { get; }
    void Reload();
}
