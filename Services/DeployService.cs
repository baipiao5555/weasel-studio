using System.Diagnostics;
using System.IO;

namespace WeaselStudio.Services;

public sealed record DeployResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    bool TimedOut)
{
    public bool Success => !TimedOut && ExitCode == 0;

    public string Summary
    {
        get
        {
            if (TimedOut)
                return "部署超时（120 秒），请检查是否有其他部署进程正在运行。";

            if (ExitCode == 0)
                return "部署完成，配置已生效。";

            var detail = string.IsNullOrWhiteSpace(StandardError)
                ? StandardOutput
                : StandardError;

            return string.IsNullOrWhiteSpace(detail)
                ? $"部署失败，WeaselDeployer 退出码 {ExitCode}。"
                : $"部署失败（退出码 {ExitCode}）：{detail.Trim()}";
        }
    }
}

public static class DeployService
{
    public static async Task<DeployResult> DeployAsync(
        RimeEnvironment environment,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(environment.DeployerPath))
            throw new FileNotFoundException(
                "找不到 WeaselDeployer.exe，请检查小狼毫安装目录。",
                environment.DeployerPath);

        var startInfo = new ProcessStartInfo
        {
            FileName = environment.DeployerPath,
            Arguments = "/deploy",
            WorkingDirectory = environment.WeaselRoot,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

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

            return new DeployResult(-1, "", "", true);
        }

        var output = await outputTask;
        var error = await errorTask;
        return new DeployResult(process.ExitCode, output, error, false);
    }
}
