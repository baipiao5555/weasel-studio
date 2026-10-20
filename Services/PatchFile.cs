using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace WeaselStudio.Services;

/// <summary>
/// Line-oriented editor for Rime *.custom.yaml patch files.
/// It deliberately does not re-serialize the whole file, so comments,
/// quoting style and the user's own ordering survive a save.
/// </summary>
public sealed class PatchFile
{
    private static readonly Regex PlainKeyRegex =
        new(@"^[A-Za-z0-9_.-]+$", RegexOptions.Compiled);

    private readonly string _path;
    private List<string> _lines = new();
    private string _newLine = "\r\n";
    private bool _hasBom;

    public PatchFile(string path)
    {
        _path = path;
        Reload();
    }

    public string Path => _path;

    public string Text
    {
        get
        {
            if (_lines.Count == 0)
                return "";

            var text = string.Join(_newLine, _lines);
            return text.EndsWith(_newLine, StringComparison.Ordinal)
                ? text
                : text + _newLine;
        }
    }

    public void Reload()
    {
        if (!File.Exists(_path))
        {
            _lines = new List<string>();
            _newLine = "\r\n";
            _hasBom = false;
            return;
        }

        var bytes = File.ReadAllBytes(_path);
        _hasBom = bytes.Length >= 3 &&
                  bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;

        var offset = _hasBom ? 3 : 0;
        var text = new UTF8Encoding(false).GetString(bytes, offset, bytes.Length - offset);
        _newLine = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        _lines = text.Replace("\r\n", "\n", StringComparison.Ordinal)
                     .Split('\n')
                     .ToList();
    }

    public void Save()
    {
        var text = Text;
        if (!string.IsNullOrWhiteSpace(text))
        {
            var parsed = YamlConfig.ParseMap(text);
            if (parsed is null)
                throw new InvalidOperationException("生成的 YAML 不是有效的映射，已取消写入。");
        }

        BackupService.Backup(_path);

        var directory = System.IO.Path.GetDirectoryName(_path);
        if (string.IsNullOrWhiteSpace(directory))
            throw new InvalidOperationException("无法确定配置文件目录。");

        Directory.CreateDirectory(directory);
        var temporary = System.IO.Path.Combine(
            directory,
            "." + System.IO.Path.GetFileName(_path) + "." + Guid.NewGuid().ToString("N") + ".tmp");

        try
        {
            File.WriteAllText(temporary, text, new UTF8Encoding(_hasBom));
            File.Move(temporary, _path, true);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    /// <summary>
    /// Sets a scalar value. The path may be flattened ("style/color_scheme")
    /// or nested in the existing patch (style: { color_scheme: ... }).
    /// </summary>
    public void SetValue(string path, string yamlValue)
    {
        if (TrySetFlat(path, yamlValue))
            return;

        if (path.Contains('/') && TrySetNested(path.Split('/'), yamlValue))
            return;

        AppendFlat(path, yamlValue);
    }

    public void SetBlock(string key, IEnumerable<string> childLines)
    {
        var children = childLines
            .Select(x => x.TrimEnd())
            .Where(x => x.Trim().Length > 0)
            .ToList();
        var (header, start, end) = FindPatchSection();
        if (header < 0)
        {
            AppendPatchHeader();
            (header, start, end) = FindPatchSection();
        }

        NormalizePatchHeader(header);
        var baseIndent = GetBaseIndent(start, end);
        var index = FindKey(start, end, baseIndent, key);

        string keyLine;
        if (index >= 0)
        {
            TryParseKeyLine(_lines[index], out var keyIndent, out _, out var rest);
            var (_, comment) = SplitValueComment(rest);
            keyLine = new string(' ', keyIndent) + FormatKey(key) + ":" +
                      (comment is null ? "" : " " + comment);

            var blockEnd = FindBlockEnd(index, end, keyIndent);
            _lines.RemoveRange(index, blockEnd - index);
            _lines.Insert(index, keyLine);
        }
        else
        {
            var insertAt = FindInsertIndex(start, end);
            keyLine = new string(' ', baseIndent) + FormatKey(key) + ":";
            _lines.Insert(insertAt, keyLine);
            index = insertAt;
        }

        var childIndent = new string(' ', GetIndent(keyLine) + 2);
        var cursor = index + 1;
        foreach (var child in children)
            _lines.Insert(cursor++, childIndent + child);
    }

    public void RemoveValue(string path)
    {
        var (_, start, end) = FindPatchSection();
        if (start < 0)
            return;

        var baseIndent = GetBaseIndent(start, end);
        var flatIndex = FindKey(start, end, baseIndent, path);
        if (flatIndex >= 0)
        {
            RemoveKeyAt(flatIndex, baseIndent, end);
            return;
        }

        if (!path.Contains('/'))
            return;

        var segments = path.Split('/');
        var blockStart = start;
        var blockEnd = end;
        var indent = baseIndent;

        for (var i = 0; i < segments.Length; i++)
        {
            var index = FindKey(blockStart, blockEnd, indent, segments[i]);
            if (index < 0)
                return;

            if (i == segments.Length - 1)
            {
                RemoveKeyAt(index, indent, blockEnd);
                return;
            }

            var childIndent = GetChildIndent(index, blockEnd, indent);
            if (childIndent < 0)
                return;

            blockStart = index + 1;
            blockEnd = FindBlockEnd(index, blockEnd, indent);
            indent = childIndent;
        }
    }

    private bool TrySetFlat(string key, string value)
    {
        var (header, start, end) = FindPatchSection();
        if (header < 0)
            return false;

        NormalizePatchHeader(header);
        var baseIndent = GetBaseIndent(start, end);
        var index = FindKey(start, end, baseIndent, key);
        if (index < 0)
            return false;

        UpdateExistingLine(index, baseIndent, key, value, end);
        return true;
    }

    private bool TrySetNested(string[] segments, string value)
    {
        var (header, start, end) = FindPatchSection();
        if (header < 0)
            return false;

        NormalizePatchHeader(header);
        var blockStart = start;
        var blockEnd = end;
        var indent = GetBaseIndent(start, end);

        for (var i = 0; i < segments.Length; i++)
        {
            var index = FindKey(blockStart, blockEnd, indent, segments[i]);
            if (index < 0)
                return false;

            if (i == segments.Length - 1)
            {
                UpdateExistingLine(index, indent, segments[i], value, blockEnd);
                return true;
            }

            var childIndent = GetChildIndent(index, blockEnd, indent);
            if (childIndent < 0)
                return false;

            blockStart = index + 1;
            blockEnd = FindBlockEnd(index, blockEnd, indent);
            indent = childIndent;
        }

        return false;
    }

    private void UpdateExistingLine(int index, int indent, string key, string value, int end)
    {
        TryParseKeyLine(_lines[index], out _, out _, out var rest);
        var (_, comment) = SplitValueComment(rest);

        var newLine = new string(' ', indent) + FormatKey(key) + ": " + value;
        if (comment is not null)
            newLine += " " + comment;

        var blockEnd = FindBlockEnd(index, end, indent);
        _lines.RemoveRange(index + 1, blockEnd - (index + 1));
        _lines[index] = newLine;
    }

    private void RemoveKeyAt(int index, int indent, int end)
    {
        var blockEnd = FindBlockEnd(index, end, indent);
        _lines.RemoveRange(index, blockEnd - index);
    }

    private void AppendFlat(string key, string value)
    {
        var (header, start, end) = FindPatchSection();
        if (header < 0)
        {
            AppendPatchHeader();
            (header, start, end) = FindPatchSection();
        }

        NormalizePatchHeader(header);
        var baseIndent = GetBaseIndent(start, end);
        var insertAt = FindInsertIndex(start, end);
        _lines.Insert(insertAt, new string(' ', baseIndent) + FormatKey(key) + ": " + value);
    }

    private void AppendPatchHeader()
    {
        if (_lines.Count > 0 && !string.IsNullOrWhiteSpace(_lines[^1]))
            _lines.Add("");

        _lines.Add("patch:");
    }

    private (int Header, int Start, int End) FindPatchSection()
    {
        for (var i = 0; i < _lines.Count; i++)
        {
            var line = _lines[i];
            if (GetIndent(line) != 0)
                continue;

            var trimmed = line.Trim();
            if (trimmed.StartsWith("#", StringComparison.Ordinal))
                continue;

            var colon = trimmed.IndexOf(':');
            if (colon <= 0)
                continue;

            var key = Unquote(trimmed[..colon].Trim());
            if (!string.Equals(key, "patch", StringComparison.Ordinal))
                continue;

            var start = i + 1;
            var end = _lines.Count;
            for (var j = start; j < _lines.Count; j++)
            {
                var candidate = _lines[j];
                if (string.IsNullOrWhiteSpace(candidate) ||
                    candidate.TrimStart().StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                if (GetIndent(candidate) == 0)
                {
                    end = j;
                    break;
                }
            }

            return (i, start, end);
        }

        return (-1, -1, -1);
    }

    private void NormalizePatchHeader(int header)
    {
        if (header < 0 || header >= _lines.Count)
            return;

        var line = _lines[header];
        var colon = line.IndexOf(':');
        if (colon < 0)
            return;

        var rest = line[(colon + 1)..].Trim();
        if (rest.Length == 0 || rest.StartsWith("#", StringComparison.Ordinal))
            return;

        if (rest is "{}" or "{ }")
        {
            _lines[header] = line[..(colon + 1)];
            return;
        }

        throw new InvalidOperationException("暂不支持内联的非空 patch 映射，请先在高级编辑器中展开为块格式。");
    }

    private int GetBaseIndent(int start, int end)
    {
        var min = int.MaxValue;
        for (var i = start; i < end; i++)
        {
            var line = _lines[i];
            if (string.IsNullOrWhiteSpace(line) ||
                line.TrimStart().StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            min = Math.Min(min, GetIndent(line));
        }

        return min == int.MaxValue ? 2 : Math.Max(0, min);
    }

    private int FindKey(int start, int end, int indent, string key)
    {
        for (var i = start; i < end; i++)
        {
            if (!TryParseKeyLine(_lines[i], out var lineIndent, out var lineKey, out _))
                continue;

            if (lineIndent == indent && string.Equals(lineKey, key, StringComparison.Ordinal))
                return i;
        }

        return -1;
    }

    private int GetChildIndent(int index, int end, int parentIndent)
    {
        for (var i = index + 1; i < end; i++)
        {
            var line = _lines[i];
            if (string.IsNullOrWhiteSpace(line) ||
                line.TrimStart().StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            var indent = GetIndent(line);
            return indent > parentIndent ? indent : -1;
        }

        return -1;
    }

    private int FindBlockEnd(int index, int end, int baseIndent)
    {
        for (var i = index + 1; i < end; i++)
        {
            var line = _lines[i];
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var indent = GetIndent(line);
            if (line.TrimStart().StartsWith("#", StringComparison.Ordinal))
            {
                // A comment at (or above) the key's indentation belongs to
                // the surrounding block, not to this value.
                if (indent <= baseIndent)
                    return i;
                continue;
            }

            if (indent <= baseIndent)
                return i;
        }

        return end;
    }

    private int FindInsertIndex(int start, int end)
    {
        for (var i = end - 1; i >= start; i--)
        {
            var line = _lines[i];
            if (!string.IsNullOrWhiteSpace(line) &&
                !line.TrimStart().StartsWith("#", StringComparison.Ordinal))
            {
                return i + 1;
            }
        }

        return start;
    }

    private static bool TryParseKeyLine(
        string line, out int indent, out string key, out string rest)
    {
        indent = GetIndent(line);
        key = "";
        rest = "";

        var content = line[indent..];
        if (content.StartsWith("#", StringComparison.Ordinal))
            return false;

        var colon = FindColonOutsideQuotes(content);
        if (colon <= 0)
            return false;

        var keyPart = content[..colon].Trim();
        if (keyPart.Length == 0)
            return false;

        key = Unquote(keyPart);
        rest = content[(colon + 1)..].Trim();
        return true;
    }

    private static int FindColonOutsideQuotes(string text)
    {
        var single = false;
        var doubleQuote = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '\'' && !doubleQuote)
            {
                single = !single;
            }
            else if (c == '"' && !single && (i == 0 || text[i - 1] != '\\'))
            {
                doubleQuote = !doubleQuote;
            }
            else if (c == ':' && !single && !doubleQuote)
            {
                return i;
            }
        }

        return -1;
    }

    private static (string Value, string? Comment) SplitValueComment(string rest)
    {
        var single = false;
        var doubleQuote = false;

        for (var i = 0; i < rest.Length; i++)
        {
            var c = rest[i];
            if (c == '\'' && !doubleQuote)
            {
                single = !single;
            }
            else if (c == '"' && !single && (i == 0 || rest[i - 1] != '\\'))
            {
                doubleQuote = !doubleQuote;
            }
            else if (c == '#' && !single && !doubleQuote &&
                     (i == 0 || char.IsWhiteSpace(rest[i - 1])))
            {
                return (rest[..i].TrimEnd(), rest[i..].Trim());
            }
        }

        return (rest.Trim(), null);
    }

    private static int GetIndent(string line)
    {
        var indent = 0;
        while (indent < line.Length && line[indent] == ' ')
            indent++;
        return indent;
    }

    private static string Unquote(string value)
    {
        if (value.Length >= 2 &&
            ((value[0] == '"' && value[^1] == '"') ||
             (value[0] == '\'' && value[^1] == '\'')))
        {
            return value[1..^1];
        }

        return value;
    }

    private static string FormatKey(string key) =>
        PlainKeyRegex.IsMatch(key) ? key : Quote(key);

    public static string Quote(string value)
    {
        var escaped = value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
        return "\"" + escaped + "\"";
    }

    public static string FormatScalar(string value) =>
        PlainKeyRegex.IsMatch(value) ? value : Quote(value);

    public static string FormatBool(bool value) => value ? "true" : "false";

    public static string FormatInt(int value) =>
        value.ToString(CultureInfo.InvariantCulture);

    public static string FormatDouble(double value) =>
        value.ToString("0.#####", CultureInfo.InvariantCulture);
}
