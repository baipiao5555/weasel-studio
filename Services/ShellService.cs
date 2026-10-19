using System.IO;
using System.Runtime.InteropServices;

namespace WeaselStudio.Services;

/// <summary>
/// Opens a local directory through the Windows shell. The path is resolved
/// and validated first; no command line is ever constructed from user input.
/// </summary>
public static class ShellService
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr ShellExecute(
        IntPtr hwnd,
        string? lpOperation,
        string lpFile,
        string? lpParameters,
        string? lpDirectory,
        int nShowCmd);

    public static bool OpenDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch
        {
            return false;
        }

        if (!Directory.Exists(fullPath))
            return false;

        var result = ShellExecute(IntPtr.Zero, "open", fullPath, null, null, 1);
        return result.ToInt64() > 32;
    }

    public static bool OpenFileLocation(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return false;

        var fullPath = Path.GetFullPath(filePath);
        var directory = File.Exists(fullPath)
            ? Path.GetDirectoryName(fullPath)
            : fullPath;

        return OpenDirectory(directory);
    }
}
