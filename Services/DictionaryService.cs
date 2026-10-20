using System.Diagnostics;
using System.IO;
using WeaselStudio.Models;

namespace WeaselStudio.Services;

public sealed record DictionaryCommandResult(
    bool Success,
    bool TimedOut,
    int ExitCode,
    string Output,
    string Error)
{
    public string Summary
    {
        get
        {
            if (TimedOut)
                return "操作超时（120 秒），可能另一个部署进程正在运行。";

            if (Success)
                return "操作完成。";

            var detail = string.IsNullOrWhiteSpace(Error) ? Output : Error;
            return string.IsNullOrWhiteSpace(detail)
                ? $"操作失败，WeaselDeployer 退出码 {ExitCode}。"
                : $"操作失败（退出码 {ExitCode}）：{detail.Trim()}";
        }
    }
}

public sealed class DictionaryService
{
    private readonly ConfigService _config;

    public DictionaryService(ConfigService config)
    {
        _config = config;
    }

    public IReadOnlyList<string> GetUserDictionaries()
    {
        var result = new List<string>();
        var userDir = _config.UserDataDir;

        if (!Directory.Exists(userDir))
            return result;

        foreach (var directory in Directory.EnumerateDirectories(userDir, "*.userdb"))
        {
            var name = Path.GetFileNameWithoutExtension(directory);
            if (!string.IsNullOrWhiteSpace(name))
                result.Add(name);
        }

        return result
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public string GetSyncDirectory()
    {
        var installation = YamlConfig.LoadMap(
            Path.Combine(_config.UserDataDir, "installation.yaml"));
        var id = YamlConfig.AsString(YamlConfig.Get(installation, "installation_id"));

        return string.IsNullOrWhiteSpace(id)
            ? Path.Combine(_config.UserDataDir, "sync")
            : Path.Combine(_config.UserDataDir, "sync", id);
    }

    public Task<DictionaryCommandResult> BackupAsync(
        string dictionary,
        CancellationToken cancellationToken = default) =>
        RunAsync(new[] { "/dict", "backup", dictionary }, cancellationToken);

    public Task<DictionaryCommandResult> ExportAsync(
        string dictionary,
        string file,
        CancellationToken cancellationToken = default) =>
        RunAsync(new[] { "/dict", "export", dictionary, file }, cancellationToken);

    public Task<DictionaryCommandResult> ImportAsync(
        string dictionary,
        string file,
        CancellationToken cancellationToken = default) =>
        RunAsync(new[] { "/dict", "import", dictionary, file }, cancellationToken);

    public Task<DictionaryCommandResult> RestoreAsync(
        string file,
        CancellationToken cancellationToken = default) =>
        RunAsync(new[] { "/dict", "restore", file }, cancellationToken);

    private async Task<DictionaryCommandResult> RunAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(_config.Environment.DeployerPath))
            throw new FileNotFoundException(
                "找不到 WeaselDeployer.exe，请检查小狼毫安装目录。",
                _config.Environment.DeployerPath);

        var startInfo = new ProcessStartInfo
        {
            FileName = _config.Environment.DeployerPath,
            WorkingDirectory = _config.Environment.WeaselRoot,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(120));

        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Process may have exited between the timeout and the kill.
            }

            return new DictionaryCommandResult(false, true, -1, "", "");
        }

        var output = await outputTask;
        var error = await errorTask;
        return new DictionaryCommandResult(
            process.ExitCode == 0,
            false,
            process.ExitCode,
            output,
            error);
    }
}
