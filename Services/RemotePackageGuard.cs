using System.Net;
using System.Net.Sockets;

namespace WeaselStudio.Services;

/// <summary>
/// Validates dictionary download endpoints before any request is sent.
/// Only HTTPS, an explicit host allowlist, and public DNS addresses are allowed.
/// </summary>
public static class RemotePackageGuard
{
    private static readonly HashSet<string> AllowedHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "github.com",
        "codeload.github.com",
        "objects.githubusercontent.com",
        "release-assets.githubusercontent.com",
        "raw.githubusercontent.com"
    };

    public static async Task ValidateAsync(Uri uri, CancellationToken cancellationToken)
    {
        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("词典下载只允许 HTTPS。");
        if (string.IsNullOrWhiteSpace(uri.Host))
            throw new InvalidOperationException("词典下载地址缺少主机名。");
        if (!uri.IsDefaultPort && uri.Port != 443)
            throw new InvalidOperationException("词典下载只允许 HTTPS 默认端口。");
        if (!AllowedHosts.Contains(uri.Host))
            throw new InvalidOperationException("词典下载地址不在允许的主机列表中。");
        if (!string.IsNullOrEmpty(uri.UserInfo))
            throw new InvalidOperationException("词典下载地址不能包含用户名或密码。");

        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(uri.Host, cancellationToken);
        }
        catch (Exception ex) when (ex is SocketException or ArgumentException)
        {
            throw new InvalidOperationException("无法解析词典下载地址的主机名。", ex);
        }

        if (addresses.Length == 0 ||
            addresses.Any(SyncUrlValidator.IsPrivateAddress))
        {
            throw new InvalidOperationException(
                "词典下载地址解析到了本机、环回、私有或保留地址，已拒绝。");
        }
    }
}
