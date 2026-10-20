using System.Text.Json.Serialization;

namespace WeaselStudio.Models;

public sealed class ClipboardPayload
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "Text";

    [JsonPropertyName("text")]
    public string Text { get; set; } = "";

    [JsonPropertyName("hash")]
    public string Hash { get; set; } = "";

    [JsonPropertyName("hasData")]
    public bool HasData { get; set; }

    [JsonPropertyName("dataName")]
    public string DataName { get; set; } = "";

    [JsonPropertyName("size")]
    public long Size { get; set; }
}
