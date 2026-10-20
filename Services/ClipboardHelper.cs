using System.Runtime.InteropServices;
using System.Threading;

namespace WeaselStudio.Services;

public static class ClipboardHelper
{
    public static string? TryGetText()
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                return Clipboard.ContainsText()
                    ? Clipboard.GetText(System.Windows.TextDataFormat.UnicodeText)
                    : null;
            }
            catch (COMException)
            {
                Thread.Sleep(30);
            }
            catch (ExternalException)
            {
                Thread.Sleep(30);
            }
        }

        return null;
    }

    public static bool TrySetText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                Clipboard.SetText(text);
                return true;
            }
            catch (COMException)
            {
                Thread.Sleep(30);
            }
            catch (ExternalException)
            {
                Thread.Sleep(30);
            }
        }

        return false;
    }
}
