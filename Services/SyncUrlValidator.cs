using System.Net;
using System.Net.Sockets;

namespace WeaselStudio.Services;

public static class SyncUrlValidator
{
    public static Uri Normalize(string serverUrl, bool allowPrivateAddresses)
    {
        if (string.IsNullOrWhiteSpace(serverUrl))
            throw new InvalidOperationException("请填写 SyncClipboard 服务器地址。");

        var text = serverUrl.Trim();
        if (!text.Contains("://", StringComparison.Ordinal))
            text = "http://" + text;

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri))
            throw new InvalidOperationException("服务器地址格式不正确。");

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("服务器地址只允许 http 或 https。");
        }

        if (string.IsNullOrWhiteSpace(uri.Host))
            throw new InvalidOperationException("服务器地址缺少主机名。");

        if (!allowPrivateAddresses && IsLocalOrPrivateHost(uri.Host))
        {
            throw new InvalidOperationException(
                "默认拒绝本机、环回和私有地址。SyncClipboard 如果在局域网/本机，请勾选“允许私有地址”。");
        }

        return uri;
    }

    public static string GetJsonUrl(Uri serverUri)
    {
        if (serverUri.AbsolutePath.EndsWith(
                "/SyncClipboard.json", StringComparison.OrdinalIgnoreCase))
        {
            return serverUri.ToString();
        }

        var builder = new UriBuilder(serverUri);
        var path = serverUri.AbsolutePath;
        if (!path.EndsWith("/", StringComparison.Ordinal))
            path += "/";

        builder.Path = path + "SyncClipboard.json";
        return builder.Uri.ToString();
    }

    public static bool IsLocalOrPrivateHost(string host)
    {
        var normalized = host.Trim('[', ']');

        if (normalized.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            normalized.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (IPAddress.TryParse(normalized, out var literal))
            return IsPrivateAddress(literal);

        try
        {
            var addresses = Dns.GetHostAddresses(normalized);
            return addresses.Length > 0 && addresses.Any(IsPrivateAddress);
        }
        catch
        {
            // If DNS fails, let HttpClient produce the connection error.
            return false;
        }
    }

    public static bool IsPrivateAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        if (IPAddress.IsLoopback(address))
            return true;

        var bytes = address.GetAddressBytes();

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            return bytes[0] == 0 ||
                   bytes[0] == 10 ||
                   bytes[0] == 127 ||
                   (bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127) ||
                   (bytes[0] == 169 && bytes[1] == 254) ||
                   (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
                   (bytes[0] == 192 && bytes[1] == 168) ||
                   (bytes[0] == 198 && (bytes[1] == 18 || bytes[1] == 19)) ||
                   bytes[0] >= 224;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return address.Equals(IPAddress.IPv6Loopback) ||
                   address.IsIPv6LinkLocal ||
                   address.IsIPv6SiteLocal ||
                   (bytes[0] & 0xFE) == 0xFC;
        }

        return false;
    }
}
