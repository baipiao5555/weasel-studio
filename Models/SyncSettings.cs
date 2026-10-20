namespace WeaselStudio.Models;

public sealed class SyncSettings
{
    public bool Enabled { get; set; }
    public string ServerUrl { get; set; } = "";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public int IntervalSeconds { get; set; } = 3;
    public bool MinimizeToTray { get; set; } = true;
    public bool AutoStart { get; set; }
    public bool AllowPrivateAddresses { get; set; }

    public SyncSettings Clone() => new()
    {
        Enabled = Enabled,
        ServerUrl = ServerUrl,
        Username = Username,
        Password = Password,
        IntervalSeconds = IntervalSeconds,
        MinimizeToTray = MinimizeToTray,
        AutoStart = AutoStart,
        AllowPrivateAddresses = AllowPrivateAddresses
    };
}
