using System.IO;

namespace WeaselStudio.Services;

public static class BackupService
{
    public const string BackupDirectoryName = ".weasel-studio-backups";

    public static string? Backup(string filePath)
    {
        if (!File.Exists(filePath))
            return null;

        var directory = Path.GetDirectoryName(filePath);
        if (string.IsNullOrWhiteSpace(directory))
            return null;

        var backupDirectory = Path.Combine(directory, BackupDirectoryName);
        Directory.CreateDirectory(backupDirectory);

        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
        var baseName = Path.GetFileName(filePath) + "." + stamp;
        var destination = Path.Combine(backupDirectory, baseName + ".bak");

        var counter = 1;
        while (File.Exists(destination))
            destination = Path.Combine(backupDirectory, $"{baseName}-{counter++}.bak");

        File.Copy(filePath, destination, false);
        return destination;
    }

    public static string GetBackupDirectory(string userDataDir) =>
        Path.Combine(userDataDir, BackupDirectoryName);
}
