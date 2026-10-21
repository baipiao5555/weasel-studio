using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using WeaselStudio.Models;

namespace WeaselStudio.Services;

public sealed record DictionaryInstallResult(
    int FileCount,
    IReadOnlyList<string> Schemas,
    string PackageDirectory);

public sealed class DictionaryInstallerService
{
    private const long MaxDownloadBytes = 64L * 1024 * 1024;
    private readonly ConfigService _config;

    public DictionaryInstallerService(ConfigService config)
    {
        _config = config;
    }

    public async Task<DictionaryInstallResult> InstallAsync(
        DictionaryPackage package,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        if (package.Url.Length == 0)
            throw new InvalidOperationException("词典包下载地址为空。");

        var cacheDir = Path.Combine(Path.GetTempPath(), "WeaselStudio");
        Directory.CreateDirectory(cacheDir);
        var archivePath = Path.Combine(cacheDir, $"dict-{Guid.NewGuid():N}.zip");

        try
        {
            await DownloadAsync(new Uri(package.Url), archivePath, progress, cancellationToken);
            return await InstallFromArchiveAsync(package, archivePath, cancellationToken);
        }
        finally
        {
            try
            {
                if (File.Exists(archivePath))
                    File.Delete(archivePath);
            }
            catch
            {
                // Temp cleanup failure must not hide the install result.
            }
        }
    }

    public async Task<DictionaryInstallResult> InstallFromArchiveAsync(
        DictionaryPackage package,
        string archivePath,
        CancellationToken cancellationToken)
    {
        if (package.Sha256.Length != 64 ||
            package.SchemaIds.Count == 0)
        {
            throw new InvalidOperationException("词典包元数据不完整。");
        }
        if (!File.Exists(archivePath))
            throw new FileNotFoundException("找不到词典压缩包。", archivePath);

        var userDir = Path.GetFullPath(_config.UserDataDir);
        Directory.CreateDirectory(userDir);

        var actualHash = await ComputeSha256Async(archivePath, cancellationToken);
        if (!string.Equals(actualHash, package.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "词典包 SHA-256 校验失败，已停止安装，文件可能被篡改或上游已变化。");
        }

        return ExtractAndInstall(package, archivePath, userDir);
    }

    private static async Task DownloadAsync(
        Uri uri,
        string destination,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        using var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.GZip |
                                     DecompressionMethods.Deflate |
                                     DecompressionMethods.Brotli
        };
        using var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMinutes(5)
        };

        var current = uri;
        for (var redirect = 0; redirect < 6; ++redirect)
        {
            await RemotePackageGuard.ValidateAsync(current, cancellationToken);

            using var request = new HttpRequestMessage(HttpMethod.Get, current);
            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (response.StatusCode is HttpStatusCode.MovedPermanently or
                HttpStatusCode.Found or
                HttpStatusCode.SeeOther or
                HttpStatusCode.TemporaryRedirect or
                HttpStatusCode.PermanentRedirect)
            {
                var location = response.Headers.Location
                               ?? throw new InvalidOperationException("词典下载重定向缺少目标地址。");
                current = new Uri(current, location);
                continue;
            }

            response.EnsureSuccessStatusCode();

            var total = response.Content.Headers.ContentLength;
            if (total is > MaxDownloadBytes)
                throw new InvalidOperationException("词典包超过 64MB 下载限制。");

            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var target = new FileStream(
                destination,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                useAsync: true);

            var buffer = new byte[81920];
            long received = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                received += read;
                if (received > MaxDownloadBytes)
                    throw new InvalidOperationException("词典包超过 64MB 下载限制。");

                await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                if (total is > 0)
                    progress?.Report(Math.Clamp((double)received / total.Value, 0, 1));
            }

            progress?.Report(1);
            return;
        }

        throw new InvalidOperationException("词典下载重定向次数过多。");
    }

    private static async Task<string> ComputeSha256Async(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            useAsync: true);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private DictionaryInstallResult ExtractAndInstall(
        DictionaryPackage package,
        string archivePath,
        string userDir)
    {
        var packageDir = Path.Combine(userDir, "dict-packs", package.Id);
        Directory.CreateDirectory(packageDir);

        var rootPrefix = string.Empty;
        using var archive = ZipFile.OpenRead(archivePath);
        var rootCandidates = archive.Entries
            .Where(x => x.FullName.Contains('/'))
            .Select(x => x.FullName[..x.FullName.IndexOf('/')])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (rootCandidates.Count == 1)
            rootPrefix = rootCandidates[0] + "/";

        var rootWithSeparator = userDir.TrimEnd(
                                    Path.DirectorySeparatorChar,
                                    Path.AltDirectorySeparatorChar) +
                                Path.DirectorySeparatorChar;
        var installed = 0;

        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name))
                continue;

            var fullName = entry.FullName.Replace('\\', '/');
            if (!fullName.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                continue;

            var relative = fullName[rootPrefix.Length..].TrimStart('/');
            if (relative.Length == 0)
                continue;

            var fileName = Path.GetFileName(relative);
            if (IsMetadataFile(fileName))
            {
                CopyMetadataEntry(entry, packageDir, fileName);
                continue;
            }

            if (ShouldSkip(relative))
                continue;

            var destination = Path.GetFullPath(Path.Combine(userDir, relative));
            if (!destination.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("词典包包含非法路径，已停止安装。");

            var directory = Path.GetDirectoryName(destination);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            if (File.Exists(destination))
                BackupService.Backup(destination);

            entry.ExtractToFile(destination, overwrite: true);
            ++installed;
        }

        File.WriteAllText(
            Path.Combine(packageDir, "SOURCE.txt"),
            $"名称: {package.Name}\r\n" +
            $"来源: {package.SourceUrl}\r\n" +
            $"授权: {package.License}\r\n" +
            $"下载: {package.Url}\r\n" +
            $"SHA-256: {package.Sha256}\r\n");

        var schemas = new List<string>();
        var enabled = _config.LoadEnabledSchemaIds().ToList();
        foreach (var schema in package.SchemaIds)
        {
            if (string.IsNullOrWhiteSpace(schema))
                continue;
            schemas.Add(schema);
            if (!enabled.Contains(schema, StringComparer.OrdinalIgnoreCase))
                enabled.Add(schema);
        }

        _config.SaveSchemaList(enabled);
        return new DictionaryInstallResult(installed, schemas, packageDir);
    }

    private static bool IsMetadataFile(string fileName)
    {
        return fileName.StartsWith("LICENSE", StringComparison.OrdinalIgnoreCase) ||
               fileName.StartsWith("COPYING", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("AUTHORS", StringComparison.OrdinalIgnoreCase);
    }

    private static void CopyMetadataEntry(
        ZipArchiveEntry entry,
        string packageDir,
        string fileName)
    {
        var destination = Path.Combine(packageDir, fileName);
        try
        {
            entry.ExtractToFile(destination, overwrite: true);
        }
        catch
        {
            // License metadata is best-effort; it must not block installation.
        }
    }

    private static bool ShouldSkip(string relative)
    {
        var normalized = relative.Replace('\\', '/');
        var fileName = Path.GetFileName(normalized);
        var firstSegment = normalized.Split('/', 2)[0];

        if (firstSegment.Equals(".github", StringComparison.OrdinalIgnoreCase) ||
            firstSegment.Equals(".ci", StringComparison.OrdinalIgnoreCase) ||
            firstSegment.Equals(".git", StringComparison.OrdinalIgnoreCase) ||
            firstSegment.Equals("build", StringComparison.OrdinalIgnoreCase) ||
            firstSegment.Equals("demo", StringComparison.OrdinalIgnoreCase) ||
            firstSegment.Equals("preview", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (fileName.Equals("default.yaml", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("weasel.yaml", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("installation.yaml", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("user.yaml", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("squirrel.yaml", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("custom_phrase.txt", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("recipe.yaml", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("AGENTS.md", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".custom.yaml", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return fileName.StartsWith("README", StringComparison.OrdinalIgnoreCase);
    }
}
