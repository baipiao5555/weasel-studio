using System.Globalization;
using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace WeaselStudio.Services;

public static class YamlConfig
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(NullNamingConvention.Instance)
        .Build();

    public static Dictionary<object, object>? LoadMap(string path)
    {
        if (!File.Exists(path))
            return null;

        return TryParseMap(File.ReadAllText(path), out var map, out _) ? map : null;
    }

    public static Dictionary<object, object>? ParseMap(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var value = Deserializer.Deserialize<object>(text);
        return value as Dictionary<object, object>;
    }

    public static bool TryParseMap(
        string text,
        out Dictionary<object, object>? map,
        out string? error)
    {
        map = null;
        error = null;

        if (string.IsNullOrWhiteSpace(text))
            return true;

        try
        {
            map = ParseMap(text);
            if (map is null)
            {
                error = "YAML 根节点不是映射。";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static object? Get(Dictionary<object, object>? map, params string[] path)
    {
        object? current = map;
        foreach (var segment in path)
        {
            if (current is not Dictionary<object, object> dictionary)
                return null;

            if (!dictionary.TryGetValue(segment, out current))
                return null;
        }

        return current;
    }

    /// <summary>
    /// Reads a Weasel/Rime patch value. Both the flattened form
    /// ("style/color_scheme") and the nested form (style: color_scheme:)
    /// are supported.
    /// </summary>
    public static object? GetPatch(Dictionary<object, object>? patch, string slashPath)
    {
        if (patch is null)
            return null;

        if (patch.TryGetValue(slashPath, out var flat))
            return flat;

        return Get(patch, slashPath.Split('/'));
    }

    public static string? AsString(object? value)
    {
        return value switch
        {
            null => null,
            string s => s,
            bool b => b ? "true" : "false",
            _ => Convert.ToString(value, CultureInfo.InvariantCulture)
        };
    }

    public static bool? AsBool(object? value)
    {
        if (value is null)
            return null;

        if (value is bool b)
            return b;

        var text = AsString(value)?.Trim();
        if (string.IsNullOrWhiteSpace(text))
            return null;

        return text.ToLowerInvariant() switch
        {
            "true" or "yes" or "on" or "1" => true,
            "false" or "no" or "off" or "0" => false,
            _ => null
        };
    }

    public static int? AsInt(object? value)
    {
        var text = AsString(value);
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;
    }

    public static double? AsDouble(object? value)
    {
        var text = AsString(value);
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;
    }

    public static IEnumerable<object> AsList(object? value)
    {
        return value is IEnumerable<object> list && value is not string
            ? list
            : Enumerable.Empty<object>();
    }

    public static IEnumerable<Dictionary<object, object>> AsMapList(object? value)
    {
        return AsList(value).OfType<Dictionary<object, object>>();
    }
}
