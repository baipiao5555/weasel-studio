using System.Globalization;
using System.Windows.Media;

namespace WeaselStudio.Services;

public static class RimeColorParser
{
    /// <summary>
    /// Parses Rime/Weasel color literals. Supported inputs: 0xRGB, 0xARGB,
    /// 0xRRGGBB, 0xRRGGBBAA, #RGB, #RGBA, with color_format abgr/argb/rgba.
    /// The default format for Weasel schemes is abgr (0xAABBGGRR).
    /// </summary>
    public static Color Parse(string? literal, string? colorFormat = "abgr")
    {
        if (string.IsNullOrWhiteSpace(literal))
            return Colors.Transparent;

        var text = literal.Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            text = text[2..];
        else if (text.StartsWith("#", StringComparison.Ordinal))
            text = text[1..];

        if (text.Length == 3 || text.Length == 4)
            text = string.Concat(text.Select(c => new string(c, 2)));

        if (text.Length is not (6 or 8))
            return Colors.Transparent;

        if (!uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
            return Colors.Transparent;

        if (text.Length == 6)
        {
            return Color.FromRgb(
                (byte)((value >> 16) & 0xFF),
                (byte)((value >> 8) & 0xFF),
                (byte)(value & 0xFF));
        }

        byte a, r, g, b;
        switch ((colorFormat ?? "abgr").Trim().ToLowerInvariant())
        {
            case "argb":
                a = (byte)((value >> 24) & 0xFF);
                r = (byte)((value >> 16) & 0xFF);
                g = (byte)((value >> 8) & 0xFF);
                b = (byte)(value & 0xFF);
                break;
            case "rgba":
                r = (byte)((value >> 24) & 0xFF);
                g = (byte)((value >> 16) & 0xFF);
                b = (byte)((value >> 8) & 0xFF);
                a = (byte)(value & 0xFF);
                break;
            default: // abgr
                a = (byte)((value >> 24) & 0xFF);
                b = (byte)((value >> 16) & 0xFF);
                g = (byte)((value >> 8) & 0xFF);
                r = (byte)(value & 0xFF);
                break;
        }

        return Color.FromArgb(a, r, g, b);
    }
}
