using System.IO;

namespace WeaselStudio.Services;

public sealed record SchemaMetadata(
    string Id,
    string Name,
    string Version,
    string Author,
    string Description);

/// <summary>
/// Reads only the schema header. Some community schema files contain YAML
/// constructs that YamlDotNet 18 rejects (for example multi-line flow
/// sequences), so this reader is intentionally line-based and tolerant.
/// </summary>
public static class SchemaMetadataReader
{
    public static SchemaMetadata Read(string file)
    {
        var fallback = GetIdFromFileName(file);
        string? id = null;
        string? name = null;
        string? version = null;
        string? author = null;
        string? description = null;

        string[] lines;
        try
        {
            lines = File.ReadAllLines(file);
        }
        catch
        {
            return new SchemaMetadata(fallback, fallback, "", "", "");
        }

        var start = -1;
        for (var i = 0; i < lines.Length; i++)
        {
            if (IsTopLevelKey(lines[i], "schema"))
            {
                start = i + 1;
                break;
            }
        }

        if (start < 0)
            return new SchemaMetadata(fallback, fallback, "", "", "");

        for (var i = start; i < lines.Length; i++)
        {
            var line = lines[i];
            if (IsTopLevelKey(line))
                break;

            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#", StringComparison.Ordinal))
                continue;

            if (CountIndent(line) != 2)
                continue;

            var (key, rawValue) = SplitKeyValue(line.Trim());
            if (key is null)
                continue;

            var value = Unquote(StripComment(rawValue).Trim());

            switch (key)
            {
                case "schema_id":
                    id = value;
                    break;
                case "name":
                    name = ReadValue(lines, i, value, rawValue, 2);
                    break;
                case "version":
                    version = ReadValue(lines, i, value, rawValue, 2);
                    break;
                case "author":
                    author = string.IsNullOrWhiteSpace(value)
                        ? ReadList(lines, i, 2)
                        : ReadValue(lines, i, value, rawValue, 2);
                    break;
                case "description":
                    description = ReadValue(lines, i, value, rawValue, 2);
                    break;
            }
        }

        return new SchemaMetadata(
            string.IsNullOrWhiteSpace(id) ? fallback : id,
            string.IsNullOrWhiteSpace(name) ? fallback : name,
            version ?? "",
            author ?? "",
            description ?? "");
    }

    private static string ReadValue(
        string[] lines,
        int index,
        string value,
        string rawValue,
        int indent)
    {
        var trimmed = rawValue.Trim();
        if (trimmed is "|" or ">" or "|-" or ">-" or "|+" or ">+")
            return ReadBlock(lines, index + 1, indent);

        return value;
    }

    private static string ReadList(string[] lines, int index, int parentIndent)
    {
        var items = new List<string>();

        for (var i = index + 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var indent = CountIndent(line);
            if (indent <= parentIndent)
                break;

            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("#", StringComparison.Ordinal))
                continue;

            if (!trimmed.StartsWith("- ", StringComparison.Ordinal) &&
                trimmed != "-")
            {
                break;
            }

            var item = trimmed.Length > 2 ? trimmed[2..] : "";
            items.Add(Unquote(StripComment(item).Trim()));
        }

        return string.Join(", ", items);
    }

    private static string ReadBlock(string[] lines, int start, int parentIndent)
    {
        var collected = new List<string>();
        var minIndent = int.MaxValue;

        for (var i = start; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
            {
                collected.Add("");
                continue;
            }

            var indent = CountIndent(line);
            if (indent <= parentIndent)
                break;

            minIndent = Math.Min(minIndent, indent);
            collected.Add(line);
        }

        if (collected.Count == 0)
            return "";

        if (minIndent == int.MaxValue)
            minIndent = parentIndent + 2;

        var result = collected
            .Select(line => line.Length >= minIndent ? line[minIndent..] : "")
            .ToList();

        while (result.Count > 0 && string.IsNullOrWhiteSpace(result[^1]))
            result.RemoveAt(result.Count - 1);

        return string.Join("\n", result);
    }

    private static (string? Key, string Value) SplitKeyValue(string text)
    {
        var single = false;
        var doubleQuote = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '\'' && !doubleQuote)
                single = !single;
            else if (c == '"' && !single && (i == 0 || text[i - 1] != '\\'))
                doubleQuote = !doubleQuote;
            else if (c == ':' && !single && !doubleQuote)
            {
                var key = Unquote(text[..i].Trim());
                return key.Length == 0 ? (null, "") : (key, text[(i + 1)..]);
            }
        }

        return (null, "");
    }

    private static string StripComment(string text)
    {
        var single = false;
        var doubleQuote = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '\'' && !doubleQuote)
                single = !single;
            else if (c == '"' && !single && (i == 0 || text[i - 1] != '\\'))
                doubleQuote = !doubleQuote;
            else if (c == '#' && !single && !doubleQuote &&
                     (i == 0 || char.IsWhiteSpace(text[i - 1])))
            {
                return text[..i];
            }
        }

        return text;
    }

    private static string Unquote(string value)
    {
        if (value.Length >= 2 &&
            ((value[0] == '"' && value[^1] == '"') ||
             (value[0] == '\'' && value[^1] == '\'')))
        {
            return value[1..^1]
                .Replace("\\\"", "\"", StringComparison.Ordinal)
                .Replace("\\\\", "\\", StringComparison.Ordinal);
        }

        return value;
    }

    private static bool IsTopLevelKey(string line)
    {
        if (string.IsNullOrWhiteSpace(line) || CountIndent(line) != 0)
            return false;

        var trimmed = line.TrimStart();
        if (trimmed.StartsWith("#", StringComparison.Ordinal))
            return false;

        var colon = trimmed.IndexOf(':');
        return colon > 0;
    }

    private static bool IsTopLevelKey(string line, string key)
    {
        if (!IsTopLevelKey(line))
            return false;

        var trimmed = line.Trim();
        var colon = trimmed.IndexOf(':');
        return colon > 0 &&
               string.Equals(Unquote(trimmed[..colon].Trim()), key, StringComparison.Ordinal);
    }

    private static int CountIndent(string line)
    {
        var indent = 0;
        while (indent < line.Length && line[indent] == ' ')
            indent++;
        return indent;
    }

    private static string GetIdFromFileName(string file)
    {
        var name = Path.GetFileName(file);
        const string suffix = ".schema.yaml";
        return name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
            ? name[..^suffix.Length]
            : Path.GetFileNameWithoutExtension(name);
    }
}
