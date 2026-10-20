using System.IO;
using WeaselStudio.Models;

namespace WeaselStudio.Services;

public sealed class FuzzyPinyinService
{
    private readonly ConfigService _config;

    public FuzzyPinyinService(ConfigService config)
    {
        _config = config;
    }

    public string GetCustomPath(SchemaInfo schema) =>
        Path.Combine(_config.UserDataDir, schema.Id + ".custom.yaml");

    public static IReadOnlyList<FuzzyRuleGroup> CreateGroups() => new List<FuzzyRuleGroup>
    {
        new()
        {
            Id = "zh_z",
            Name = "平翘舌",
            Description = "zh/ch/sh 与 z/c/s 互认",
            Rules = new[]
            {
                "derive/^([zcs])h/$1/",
                "derive/^([zcs])([^h])/$1h$2/"
            }
        },
        new()
        {
            Id = "n_l",
            Name = "鼻边音",
            Description = "n 与 l 互认",
            Rules = new[]
            {
                "derive/^n/l/",
                "derive/^l/n/"
            }
        },
        new()
        {
            Id = "f_h",
            Name = "唇齿音",
            Description = "f 与 h 互认",
            Rules = new[]
            {
                "derive/^f/h/",
                "derive/^h/f/"
            }
        },
        new()
        {
            Id = "en_eng",
            Name = "前后鼻音",
            Description = "an/ang、en/eng、in/ing 互认",
            Rules = new[]
            {
                "derive/([aei])n$/$1ng/",
                "derive/([aei])ng$/$1n/"
            }
        },
        new()
        {
            Id = "ian_iang",
            Name = "ian/iang、uan/uang",
            Description = "ian/iang、uan/uang 互认",
            Rules = new[]
            {
                "derive/([iu])an$/$1ang/",
                "derive/([iu])ang$/$1an/"
            }
        }
    };

    public IReadOnlyList<string> LoadBaseAlgebra(SchemaInfo schema)
    {
        var rules = new List<string>();

        if (string.IsNullOrWhiteSpace(schema.FilePath) || !File.Exists(schema.FilePath))
            return rules;

        string[] lines;
        try
        {
            lines = File.ReadAllLines(schema.FilePath);
        }
        catch
        {
            return rules;
        }

        var spellerStart = -1;
        for (var i = 0; i < lines.Length; i++)
        {
            if (IsTopLevelKey(lines[i], "speller"))
            {
                spellerStart = i + 1;
                break;
            }
        }

        if (spellerStart < 0)
            return rules;

        var spellerEnd = lines.Length;
        for (var i = spellerStart; i < lines.Length; i++)
        {
            if (IsTopLevelKey(lines[i]))
            {
                spellerEnd = i;
                break;
            }
        }

        var algebraIndex = -1;
        for (var i = spellerStart; i < spellerEnd; i++)
        {
            if (TryParseKeyLine(lines[i], out var indent, out var key, out _) &&
                indent == 2 &&
                string.Equals(key, "algebra", StringComparison.Ordinal))
            {
                algebraIndex = i;
                break;
            }
        }

        if (algebraIndex < 0)
            return rules;

        for (var i = algebraIndex + 1; i < spellerEnd; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line) ||
                line.TrimStart().StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            if (CountIndent(line) <= 2)
                break;

            var trimmed = line.Trim();
            if (!trimmed.StartsWith("- ", StringComparison.Ordinal))
                continue;

            var rule = StripInlineComment(trimmed[2..]).Trim();
            if (rule.Length > 0)
                rules.Add(rule);
        }

        return rules;
    }

    public IReadOnlyList<string> LoadCustomAlgebra(SchemaInfo schema)
    {
        var path = GetCustomPath(schema);
        if (!File.Exists(path))
            return Array.Empty<string>();

        var root = YamlConfig.LoadMap(path);
        var patch = YamlConfig.Get(root, "patch") as Dictionary<object, object>;
        var value = YamlConfig.GetPatch(patch, "speller/algebra");

        return YamlConfig.AsList(value)
            .Select(YamlConfig.AsString)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .ToList();
    }

    public IReadOnlyList<string> MergeRules(
        IReadOnlyList<string> baseRules,
        IReadOnlyList<string> customRules,
        IEnumerable<FuzzyRuleGroup> groups)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void Add(string rule)
        {
            if (!string.IsNullOrWhiteSpace(rule) && seen.Add(rule))
                result.Add(rule);
        }

        foreach (var rule in baseRules)
            Add(rule);

        var allFuzzyRules = new HashSet<string>(
            CreateGroups().SelectMany(x => x.Rules),
            StringComparer.Ordinal);

        // Keep algebra rules the user added manually, but drop old fuzzy rules
        // so that unchecking a group really removes it on the next save.
        foreach (var rule in customRules.Where(
                     rule => !allFuzzyRules.Contains(rule)))
        {
            Add(rule);
        }

        foreach (var rule in groups.Where(x => x.IsEnabled).SelectMany(x => x.Rules))
            Add(rule);

        return result;
    }

    public void SaveAlgebra(SchemaInfo schema, IReadOnlyList<string> rules)
    {
        var patch = new PatchFile(GetCustomPath(schema));
        patch.SetBlock(
            "speller/algebra",
            rules.Select(rule => "- " + rule));
        patch.Save();
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
               string.Equals(trimmed[..colon].Trim().Trim('"', '\''), key,
                   StringComparison.Ordinal);
    }

    private static bool TryParseKeyLine(
        string line, out int indent, out string key, out string rest)
    {
        indent = CountIndent(line);
        key = "";
        rest = "";

        var content = line[indent..];
        if (content.StartsWith("#", StringComparison.Ordinal))
            return false;

        var colon = content.IndexOf(':');
        if (colon <= 0)
            return false;

        key = content[..colon].Trim().Trim('"', '\'');
        rest = content[(colon + 1)..].Trim();
        return key.Length > 0;
    }

    private static string StripInlineComment(string text)
    {
        var index = text.IndexOf(" #", StringComparison.Ordinal);
        return index >= 0 ? text[..index] : text;
    }

    private static int CountIndent(string line)
    {
        var indent = 0;
        while (indent < line.Length && line[indent] == ' ')
            indent++;
        return indent;
    }
}
