using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WeaselStudio.Models;

namespace WeaselStudio.Services;

public sealed class SyncClipboardClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly string _jsonUrl;

    public SyncClipboardClient(
        string serverUrl,
        string username,
        string password,
        bool allowPrivateAddresses,
        HttpMessageHandler? handler = null)
    {
        var uri = SyncUrlValidator.Normalize(serverUrl, allowPrivateAddresses);
        _jsonUrl = SyncUrlValidator.GetJsonUrl(uri);

        var httpHandler = handler ?? new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false
        };

        _http = new HttpClient(httpHandler)
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        if (!string.IsNullOrWhiteSpace(username) || !string.IsNullOrWhiteSpace(password))
        {
            var token = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{username}:{password}"));
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", token);
        }

        _http.DefaultRequestHeaders.UserAgent.ParseAdd("WeaselStudio/0.4");
    }

    public async Task<(ClipboardPayload? Data, string? ETag)> PullAsync(
        string? etag,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, _jsonUrl);
        if (!string.IsNullOrWhiteSpace(etag))
            request.Headers.TryAddWithoutValidation("If-None-Match", etag);

        using var response = await _http.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotModified)
            return (null, response.Headers.ETag?.Tag ?? etag);

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
            return (null, response.Headers.ETag?.Tag);

        var data = JsonSerializer.Deserialize<ClipboardPayload>(json, JsonOptions);
        return (data, response.Headers.ETag?.Tag);
    }

    public async Task PushTextAsync(
        string text,
        string hash,
        CancellationToken cancellationToken)
    {
        var payload = new ClipboardPayload
        {
            Type = "Text",
            Text = text,
            Hash = hash,
            HasData = false,
            DataName = "",
            Size = Encoding.UTF8.GetByteCount(text)
        };

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _http.PutAsync(_jsonUrl, content, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task TestConnectionAsync(CancellationToken cancellationToken)
    {
        await PullAsync(null, cancellationToken);
    }

    public static string HashText(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    public void Dispose() => _http.Dispose();
}
