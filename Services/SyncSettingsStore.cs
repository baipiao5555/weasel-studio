using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WeaselStudio.Models;

namespace WeaselStudio.Services;

public sealed class SyncSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _directory;

    public SyncSettingsStore(string? directory = null)
    {
        _directory = directory ??
                     Environment.GetEnvironmentVariable("WEASEL_STUDIO_DATA_DIR") ??
                     Path.Combine(
                         Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                         "WeaselStudio");
    }

    public string SettingsPath => Path.Combine(_directory, "sync-settings.json");

    public SyncSettings Load()
    {
        if (!File.Exists(SettingsPath))
            return new SyncSettings();

        try
        {
            var dto = JsonSerializer.Deserialize<SyncSettingsDto>(
                File.ReadAllText(SettingsPath), JsonOptions);

            if (dto is null)
                return new SyncSettings();

            return new SyncSettings
            {
                Enabled = dto.Enabled,
                ServerUrl = dto.ServerUrl ?? "",
                Username = dto.Username ?? "",
                Password = Unprotect(dto.ProtectedPassword),
                IntervalSeconds = Math.Clamp(dto.IntervalSeconds, 1, 60),
                MinimizeToTray = dto.MinimizeToTray,
                AutoStart = dto.AutoStart,
                AllowPrivateAddresses = dto.AllowPrivateAddresses
            };
        }
        catch
        {
            return new SyncSettings();
        }
    }

    public void Save(SyncSettings settings)
    {
        Directory.CreateDirectory(_directory);

        var dto = new SyncSettingsDto
        {
            Enabled = settings.Enabled,
            ServerUrl = settings.ServerUrl,
            Username = settings.Username,
            ProtectedPassword = Protect(settings.Password),
            IntervalSeconds = Math.Clamp(settings.IntervalSeconds, 1, 60),
            MinimizeToTray = settings.MinimizeToTray,
            AutoStart = settings.AutoStart,
            AllowPrivateAddresses = settings.AllowPrivateAddresses
        };

        File.WriteAllText(
            SettingsPath,
            JsonSerializer.Serialize(dto, JsonOptions),
            new UTF8Encoding(false));
    }

    private static string Protect(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        var bytes = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(value),
            null,
            DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(bytes);
    }

    private static string Unprotect(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        try
        {
            var bytes = ProtectedData.Unprotect(
                Convert.FromBase64String(value),
                null,
                DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return "";
        }
    }

    private sealed class SyncSettingsDto
    {
        public bool Enabled { get; set; }
        public string? ServerUrl { get; set; }
        public string? Username { get; set; }
        public string? ProtectedPassword { get; set; }
        public int IntervalSeconds { get; set; } = 3;
        public bool MinimizeToTray { get; set; } = true;
        public bool AutoStart { get; set; }
        public bool AllowPrivateAddresses { get; set; }
    }
}
