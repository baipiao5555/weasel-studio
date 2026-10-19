using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace WeaselStudio.Services;

public sealed class RimeEnvironment
{
    public string UserDataDir { get; init; } = "";
    public string WeaselRoot { get; init; } = "";
    public string DeployerPath { get; init; } = "";
    public string ServerPath { get; init; } = "";
    public string Version { get; init; } = "";
    public string InstallDir { get; init; } = "";

    public bool IsFound =>
        !string.IsNullOrWhiteSpace(UserDataDir) &&
        Directory.Exists(UserDataDir) &&
        !string.IsNullOrWhiteSpace(DeployerPath) &&
        File.Exists(DeployerPath);

    public string StatusText => IsFound
        ? $"已连接 · Weasel {Version}"
        : "未找到小狼毫，请先安装或手动选择目录";

    public static RimeEnvironment Detect()
    {
        var userDir = Registry.GetValue(
            @"HKEY_CURRENT_USER\Software\Rime\Weasel",
            "RimeUserDir", null) as string;

        if (string.IsNullOrWhiteSpace(userDir))
        {
            userDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Rime");
        }

        var installDir = Registry.GetValue(
            @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Rime\Weasel",
            "InstallDir", null) as string;

        var weaselRoot = Registry.GetValue(
            @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Rime\Weasel",
            "WeaselRoot", null) as string;

        if (string.IsNullOrWhiteSpace(weaselRoot))
        {
            weaselRoot = Registry.GetValue(
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Rime\Weasel",
                "WeaselRoot", null) as string;
        }

        if (string.IsNullOrWhiteSpace(weaselRoot))
            weaselRoot = FindWeaselRoot();

        if (string.IsNullOrWhiteSpace(weaselRoot))
            weaselRoot = "";

        var deployer = string.IsNullOrWhiteSpace(weaselRoot)
            ? ""
            : Path.Combine(weaselRoot, "WeaselDeployer.exe");

        var server = string.IsNullOrWhiteSpace(weaselRoot)
            ? ""
            : Path.Combine(weaselRoot, "WeaselServer.exe");

        var version = "";
        if (!string.IsNullOrWhiteSpace(server) && File.Exists(server))
        {
            try
            {
                version = FileVersionInfo.GetVersionInfo(server).FileVersion ?? "";
            }
            catch
            {
                // Keep the version empty; the path is still usable.
            }
        }

        if (string.IsNullOrWhiteSpace(version) && !string.IsNullOrWhiteSpace(weaselRoot))
        {
            var name = Path.GetFileName(weaselRoot.TrimEnd('\\', '/'));
            if (name.StartsWith("weasel-", StringComparison.OrdinalIgnoreCase))
                version = name["weasel-".Length..];
        }

        return new RimeEnvironment
        {
            UserDataDir = userDir,
            WeaselRoot = weaselRoot,
            DeployerPath = deployer,
            ServerPath = server,
            Version = version,
            InstallDir = installDir ?? ""
        };
    }

    private static string FindWeaselRoot()
    {
        var candidates = new List<string>();
        var bases = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Rime"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Rime")
        };

        foreach (var baseDir in bases.Where(Directory.Exists))
        {
            try
            {
                candidates.AddRange(Directory.EnumerateDirectories(baseDir, "weasel-*"));
            }
            catch
            {
                // Ignore inaccessible install locations.
            }
        }

        return candidates
            .OrderByDescending(ParseVersion)
            .FirstOrDefault() ?? "";
    }

    private static Version ParseVersion(string directory)
    {
        var name = Path.GetFileName(directory.TrimEnd('\\', '/'));
        if (name.StartsWith("weasel-", StringComparison.OrdinalIgnoreCase) &&
            System.Version.TryParse(name["weasel-".Length..], out var version))
        {
            return version;
        }

        return new System.Version(0, 0);
    }
}
