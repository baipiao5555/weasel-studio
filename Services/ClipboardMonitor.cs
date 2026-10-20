using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace WeaselStudio.Services;

public sealed class ClipboardMonitor : IDisposable
{
    private const int WM_CLIPBOARDUPDATE = 0x031D;
    private static readonly IntPtr HWND_MESSAGE = new(-3);

    private HwndSource? _source;
    private bool _disposed;

    public event EventHandler<string>? TextChanged;

    public ClipboardMonitor()
    {
        var parameters = new HwndSourceParameters("WeaselStudioClipboardMonitor")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0,
            ExtendedWindowStyle = 0,
            ParentWindow = HWND_MESSAGE
        };

        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);

        if (!AddClipboardFormatListener(_source.Handle))
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    private IntPtr WndProc(
        IntPtr hwnd,
        int message,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled)
    {
        if (message != WM_CLIPBOARDUPDATE)
            return IntPtr.Zero;

        var text = ClipboardHelper.TryGetText();
        if (!string.IsNullOrEmpty(text))
            TextChanged?.Invoke(this, text);

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_source is not null)
        {
            RemoveClipboardFormatListener(_source.Handle);
            _source.RemoveHook(WndProc);
            _source.Dispose();
            _source = null;
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
}
