using System.Globalization;
using System.IO;
using System.Text;
using WeaselStudio.Models;

namespace WeaselStudio.Services;

public sealed class InputHabitsService
{
    private readonly ConfigService _config;

    public InputHabitsService(ConfigService config)
    {
        _config = config;
    }

    public string GetSchemaCustomPath(SchemaInfo schema) =>
        Path.Combine(_config.UserDataDir, schema.Id + ".custom.yaml");

    public IReadOnlyList<SchemaSwitch> LoadSwitches(SchemaInfo schema)
    {
        var result = new List<SchemaSwitch>();

        if (string.IsNullOrWhiteSpace(schema.FilePath) || !File.Exists(schema.FilePath))
            return result;

        string[] lines;
        try
        {
            lines = File.ReadAllLines(schema.FilePath);
        }
        catch
        {
            return result;
        }

        var start = -1;
        for (var i = 0; i < lines.Length; i++)
        {
            if (IsTopLevelKey(lines[i], "switches"))
            {
                start = i + 1;
                break;
            }
        }

        if (start < 0)
            return result;

        var end = lines.Length;
        for (var i = start; i < lines.Length; i++)
        {
            if (IsTopLevelKey(lines[i]))
            {
                end = i;
                break;
            }
        }

        SchemaSwitch? current = null;

        for (var i = start; i < end; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line) ||
                line.TrimStart().StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            var indent = CountIndent(line);
            var trimmed = line.Trim();

            if (indent == 2 && trimmed.StartsWith("- ", StringComparison.Ordinal))
            {
                if (current is not null)
                    result.Add(current);

                current = new SchemaSwitch();
                var rest = trimmed[2..].Trim();

                if (rest.StartsWith("name:", StringComparison.Ordinal))
                {
                    current.Name = Unquote(StripComment(rest["name:".Length..]).Trim());
                }
                else if (rest.StartsWith("{", StringComparison.Ordinal))
                {
                    // Inline form: - {name: ascii_mode, reset: 0, states: [...]}
                    var map = ParseInlineMap(rest);
                    if (map.TryGetValue("name", out var name))
                        current.Name = name;
                    if (map.TryGetValue("reset", out var reset))
                        current.Reset = ParseInt(reset);
                    if (map.TryGetValue("states", out var states))
                        current.States = ParseStates(states, lines, i, end, indent);
                }

                continue;
            }

            if (current is null || indent <= 2)
                continue;

            var (key, value) = SplitKeyValue(trimmed);
            if (key is null)
                continue;

            switch (key)
            {
                case "name" when string.IsNullOrWhiteSpace(current.Name):
                    current.Name = Unquote(StripComment(value).Trim());
                    break;
                case "reset":
                    current.Reset = ParseInt(value);
                    break;
                case "states":
                    current.States = ParseStates(value, lines, i, end, indent);
                    break;
            }
        }

        if (current is not null)
            result.Add(current);

        ApplyCustomOverrides(schema, result);

        return result
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .ToList();
    }

    private void ApplyCustomOverrides(SchemaInfo schema, List<SchemaSwitch> switches)
    {
        var customPath = GetSchemaCustomPath(schema);
        if (!File.Exists(customPath))
            return;

        var root = YamlConfig.LoadMap(customPath);
        var patch = YamlConfig.Get(root, "patch") as Dictionary<object, object>;
        var items = YamlConfig.AsList(YamlConfig.Get(patch, "switches"))
            .OfType<Dictionary<object, object>>();

        foreach (var item in items)
        {
            var name = YamlConfig.AsString(YamlConfig.Get(item, "name"));
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var target = switches.FirstOrDefault(
                x => string.Equals(x.Name, name, StringComparison.Ordinal));
            if (target is null)
                continue;

            var reset = YamlConfig.AsInt(YamlConfig.Get(item, "reset"));
            if (reset is not null)
                target.Reset = reset.Value;

            var states = YamlConfig.AsList(YamlConfig.Get(item, "states"))
                .Select(YamlConfig.AsString)
                .Where(x => x is not null)
                .Select(x => x!)
                .ToList();

            if (states.Count > 0)
                target.States = states;
        }
    }

    public void SaveSwitches(SchemaInfo schema, IReadOnlyList<SchemaSwitch> switches)
    {
        var lines = new List<string>();

        foreach (var item in switches)
        {
            lines.Add($"- name: {PatchFile.FormatScalar(item.Name)}");
            lines.Add($"  reset: {Math.Max(0, item.Reset)}");

            if (item.States.Count > 0)
            {
                var states = string.Join(", ", item.States.Select(PatchFile.Quote));
                lines.Add($"  states: [{states}]");
            }
        }

        var patch = new PatchFile(GetSchemaCustomPath(schema));
        patch.SetBlock("switches", lines);
        patch.Save();
    }

    public int LoadPageSize()
    {
        var patchRoot = YamlConfig.LoadMap(_config.DefaultCustomPath);
        var patch = YamlConfig.Get(patchRoot, "patch") as Dictionary<object, object>;
        var value = YamlConfig.GetPatch(patch, "menu/page_size");

        if (value is null)
        {
            var defaultRoot = YamlConfig.LoadMap(_config.DefaultBasePath);
            value = YamlConfig.Get(defaultRoot, "menu", "page_size");
        }

        return YamlConfig.AsInt(value) ?? 6;
    }

    public void SavePageSize(int pageSize)
    {
        var value = Math.Clamp(pageSize, 1, 10);
        var patch = new PatchFile(_config.DefaultCustomPath);
        patch.SetValue("menu/page_size", PatchFile.FormatInt(value));
        patch.Save();
    }

    private static List<string> ParseStates(
        string value,
        string[] lines,
        int index,
        int end,
        int indent)
    {
        var result = new List<string>();
        var trimmed = value.Trim();

        if (trimmed.StartsWith("[", StringComparison.Ordinal))
        {
            var buffer = new StringBuilder(trimmed);
            var cursor = index + 1;
            while (!buffer.ToString().Contains(']') && cursor < end)
                buffer.Append(' ').Append(lines[cursor++].Trim());

            var text = buffer.ToString();
            var close = text.LastIndexOf(']');
            if (close > 0)
                text = text[1..close];

            foreach (var item in SplitInlineList(text))
            {
                var state = Unquote(StripComment(item).Trim());
                if (state.Length > 0)
                    result.Add(state);
            }

            return result;
        }

        if (trimmed.Length > 0)
        {
            var single = Unquote(StripComment(trimmed).Trim());
            if (single.Length > 0)
                result.Add(single);
            return result;
        }

        // Block form:
        // states:
        //   - 中文
        //   - 英文
        for (var i = index + 1; i < end; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var lineIndent = CountIndent(line);
            if (lineIndent <= indent)
                break;

            var item = line.Trim();
            if (!item.StartsWith("- ", StringComparison.Ordinal))
                break;

            var state = Unquote(StripComment(item[2..]).Trim());
            if (state.Length > 0)
                result.Add(state);
        }

        return result;
    }

    private static List<string> SplitInlineList(string content)
    {
        var result = new List<string>();
        var buffer = new StringBuilder();
        var single = false;
        var doubleQuote = false;

        foreach (var c in content)
        {
            if (c == '\'' && !doubleQuote)
                single = !single;
            else if (c == '"' && !single)
                doubleQuote = !doubleQuote;

            if (c == ',' && !single && !doubleQuote)
            {
                result.Add(buffer.ToString().Trim());
                buffer.Clear();
            }
            else
            {
                buffer.Append(c);
            }
        }

        if (buffer.Length > 0)
            result.Add(buffer.ToString().Trim());

        return result;
    }

    private static Dictionary<string, string> ParseInlineMap(string text)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var content = text.Trim();
        if (content.StartsWith("{", StringComparison.Ordinal))
            content = content[1..];
        if (content.EndsWith("}", StringComparison.Ordinal))
            content = content[..^1];

        foreach (var part in SplitInlineList(content))
        {
            var (key, value) = SplitKeyValue(part.Trim());
            if (key is not null)
                result[key] = value.Trim();
        }

        return result;
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

    private static int ParseInt(string value)
    {
        var text = StripComment(value).Trim();
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : 0;
    }

    private static bool IsTopLevelKey(string line)
    {
        if (string.IsNullOrWhiteSpace(line) || CountIndent(line) != 0)
            return false;

        var trimmed = line.TrimStart();
        return !trimmed.StartsWith("#", StringComparison.Ordinal) &&
               trimmed.IndexOf(':') > 0;
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
}
