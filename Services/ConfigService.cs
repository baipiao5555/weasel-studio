using System.IO;
using System.Text;
using WeaselStudio.Models;

namespace WeaselStudio.Services;

public sealed class ConfigService
{
    public RimeEnvironment Environment { get; }

    public ConfigService(RimeEnvironment environment)
    {
        Environment = environment;
    }

    public string UserDataDir => Environment.UserDataDir;
    public string DefaultCustomPath => Path.Combine(UserDataDir, "default.custom.yaml");
    public string WeaselCustomPath => Path.Combine(UserDataDir, "weasel.custom.yaml");
    public string WeaselBasePath => Path.Combine(UserDataDir, "weasel.yaml");
    public string DefaultBasePath => Path.Combine(UserDataDir, "default.yaml");

    public Dictionary<object, object>? LoadPatch(string path)
    {
        var root = YamlConfig.LoadMap(path);
        return YamlConfig.Get(root, "patch") as Dictionary<object, object>;
    }

    public IReadOnlyList<string> LoadEnabledSchemaIds()
    {
        var patch = LoadPatch(DefaultCustomPath);
        var list = YamlConfig.Get(patch, "schema_list");

        if (list is null)
        {
            var defaultRoot = YamlConfig.LoadMap(DefaultBasePath);
            list = YamlConfig.Get(defaultRoot, "schema_list");
        }

        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(object? value)
        {
            string? id;
            if (value is Dictionary<object, object> map)
                id = YamlConfig.AsString(YamlConfig.Get(map, "schema"));
            else
                id = YamlConfig.AsString(value);

            if (!string.IsNullOrWhiteSpace(id) && seen.Add(id))
                result.Add(id);
        }

        if (list is Dictionary<object, object> single)
        {
            Add(single);
        }
        else
        {
            foreach (var item in YamlConfig.AsList(list))
                Add(item);
        }

        return result;
    }

    public IReadOnlyList<SchemaInfo> LoadSchemas()
    {
        var enabledIds = LoadEnabledSchemaIds();
        var enabledSet = new HashSet<string>(enabledIds, StringComparer.OrdinalIgnoreCase);
        var result = new List<SchemaInfo>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (Directory.Exists(UserDataDir))
        {
            foreach (var file in Directory.EnumerateFiles(UserDataDir, "*.schema.yaml")
                         .OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                var metadata = SchemaMetadataReader.Read(file);
                var id = metadata.Id;

                if (!seen.Add(id))
                    continue;

                result.Add(new SchemaInfo
                {
                    Id = id,
                    Name = metadata.Name,
                    Version = metadata.Version,
                    Author = metadata.Author,
                    Description = metadata.Description,
                    FilePath = file,
                    IsEnabled = enabledSet.Contains(id)
                });
            }
        }

        foreach (var enabledId in enabledIds.Where(id => !seen.Contains(id)))
        {
            result.Add(new SchemaInfo
            {
                Id = enabledId,
                Name = "（未找到方案文件）",
                IsEnabled = true,
                IsMissing = true,
                Description = "default.custom.yaml 引用了这个方案，但用户目录中没有对应的 .schema.yaml 文件。"
            });
        }

        var ordered = new List<SchemaInfo>();
        foreach (var id in enabledIds)
        {
            var schema = result.FirstOrDefault(
                x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
            if (schema is not null)
                ordered.Add(schema);
        }

        ordered.AddRange(result
            .Where(x => !ordered.Contains(x))
            .OrderBy(x => x.DisplayName, StringComparer.CurrentCultureIgnoreCase));

        return ordered;
    }

    public void SaveSchemaList(IEnumerable<string> ids)
    {
        var list = ids
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (list.Count == 0)
            throw new InvalidOperationException("至少保留一个输入方案，否则小狼毫无法切换方案。");

        var patch = new PatchFile(DefaultCustomPath);
        patch.SetBlock(
            "schema_list",
            list.Select(id => $"- {{schema: {PatchFile.FormatScalar(id)}}}"));
        patch.Save();
    }

    public StyleSettings LoadStyle()
    {
        var baseRoot = YamlConfig.LoadMap(WeaselBasePath);
        var patch = LoadPatch(WeaselCustomPath);

        object? Raw(string path) =>
            YamlConfig.GetPatch(patch, path) ??
            YamlConfig.Get(baseRoot, path.Split('/'));

        string Str(string path, string fallback) =>
            YamlConfig.AsString(Raw(path)) ?? fallback;

        bool Bool(string path, bool fallback) =>
            YamlConfig.AsBool(Raw(path)) ?? fallback;

        int Int(string path, int fallback) =>
            YamlConfig.AsInt(Raw(path)) ?? fallback;

        double Double(string path, double fallback) =>
            YamlConfig.AsDouble(Raw(path)) ?? fallback;

        var fontPoint = Double("style/font_point", 12);
        var fontFace = Str("style/font_face", "Microsoft YaHei");
        var layoutType = Str("style/layout/type", "");

        var horizontal = Bool("style/horizontal", false);
        if (Raw("style/horizontal") is null &&
            layoutType.Contains("horizontal", StringComparison.OrdinalIgnoreCase))
        {
            horizontal = true;
        }

        var verticalText = Bool("style/vertical_text", false);
        if (Raw("style/vertical_text") is null &&
            layoutType.Contains("vertical_text", StringComparison.OrdinalIgnoreCase))
        {
            verticalText = true;
        }

        return new StyleSettings
        {
            ColorScheme = Str("style/color_scheme", "default"),
            ColorSchemeDark = Str("style/color_scheme_dark", ""),
            Horizontal = horizontal,
            VerticalText = verticalText,
            FontFace = fontFace,
            FontPoint = fontPoint,
            LabelFontFace = Str("style/label_font_face", fontFace),
            LabelFontPoint = Double("style/label_font_point", fontPoint),
            CommentFontFace = Str("style/comment_font_face", fontFace),
            CommentFontPoint = Double("style/comment_font_point", fontPoint),
            InlinePreedit = Bool("style/inline_preedit", false),
            PreeditType = Str("style/preedit_type", "composition"),
            CornerRadius = Int(
                "style/layout/corner_radius",
                Int("style/layout/round_corner", 8)),
            BorderWidth = Int(
                "style/layout/border_width",
                Int("style/layout/border", 0)),
            MarginX = Int("style/layout/margin_x", 8),
            MarginY = Int("style/layout/margin_y", 8),
            Spacing = Int("style/layout/spacing", 10),
            CandidateSpacing = Int("style/layout/candidate_spacing", 22),
            HilitePadding = Int("style/layout/hilite_padding", 8),
            ShadowRadius = Int("style/layout/shadow_radius", 0),
            ShadowOffsetX = Int("style/layout/shadow_offset_x", 4),
            ShadowOffsetY = Int("style/layout/shadow_offset_y", 4),
            MaxHeight = Int("style/layout/max_height", 600),
            MinWidth = Int("style/layout/min_width", 145),
            CandidateAbbreviateLength = Int("style/candidate_abbreviate_length", 30)
        };
    }

    public IReadOnlyList<ColorSchemeInfo> LoadColorSchemes()
    {
        var schemes = new Dictionary<string, ColorSchemeInfo>(StringComparer.OrdinalIgnoreCase);
        var baseRoot = YamlConfig.LoadMap(WeaselBasePath);
        var patch = LoadPatch(WeaselCustomPath);

        if (YamlConfig.Get(baseRoot, "preset_color_schemes") is Dictionary<object, object> baseSchemes)
        {
            foreach (var (key, value) in baseSchemes)
            {
                var id = key?.ToString();
                if (string.IsNullOrWhiteSpace(id) || value is not Dictionary<object, object> map)
                    continue;

                schemes[id] = BuildScheme(id, map);
            }
        }

        if (YamlConfig.Get(patch, "preset_color_schemes") is Dictionary<object, object> patchSchemes)
        {
            foreach (var (key, value) in patchSchemes)
            {
                var id = key?.ToString();
                if (string.IsNullOrWhiteSpace(id) || value is not Dictionary<object, object> map)
                    continue;

                if (schemes.TryGetValue(id, out var existing))
                    existing.Merge(ToDictionary(map));
                else
                    schemes[id] = BuildScheme(id, map);
            }
        }

        if (patch is not null)
        {
            foreach (var (key, value) in patch)
            {
                var flatKey = key?.ToString();
                if (flatKey is null ||
                    !flatKey.StartsWith("preset_color_schemes/", StringComparison.Ordinal))
                {
                    continue;
                }

                var parts = flatKey.Split('/');
                if (parts.Length < 3)
                    continue;

                var id = parts[1];
                var property = parts[2];

                if (!schemes.TryGetValue(id, out var scheme))
                {
                    scheme = new ColorSchemeInfo { Id = id, Name = id };
                    schemes[id] = scheme;
                }

                var stringValue = value as string ?? YamlConfig.AsString(value);
                if (stringValue is not null)
                    scheme.SetProperty(property, stringValue);
            }
        }

        foreach (var scheme in schemes.Values)
            scheme.RecomputePreview();

        if (schemes.Count == 0)
        {
            var fallback = new ColorSchemeInfo { Id = "default", Name = "默认（未找到配色方案）" };
            fallback.RecomputePreview();
            schemes["default"] = fallback;
        }

        return schemes.Values
            .OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public string[] GetEditableFiles()
    {
        var files = new List<string>();

        void Add(string path)
        {
            if (File.Exists(path) && !files.Contains(path, StringComparer.OrdinalIgnoreCase))
                files.Add(path);
        }

        Add(DefaultCustomPath);
        Add(WeaselCustomPath);

        if (Directory.Exists(UserDataDir))
        {
            foreach (var file in Directory.EnumerateFiles(UserDataDir, "*.custom.yaml")
                         .OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                Add(file);
            }
        }

        return files.ToArray();
    }

    public string ReadText(string path)
    {
        return File.Exists(path) ? File.ReadAllText(path) : "";
    }

    public void WriteText(string path, string content)
    {
        if (!string.IsNullOrWhiteSpace(content) && YamlConfig.ParseMap(content) is null)
            throw new InvalidOperationException("YAML 语法校验失败：根节点必须是一个映射。");

        BackupService.Backup(path);

        var hasBom = false;
        if (File.Exists(path))
        {
            var bytes = File.ReadAllBytes(path);
            hasBom = bytes.Length >= 3 &&
                     bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        }

        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directory))
            throw new InvalidOperationException("无法确定配置文件目录。");

        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(
            directory,
            "." + Path.GetFileName(path) + "." + Guid.NewGuid().ToString("N") + ".tmp");

        try
        {
            File.WriteAllText(temporary, content, new UTF8Encoding(hasBom));
            File.Move(temporary, path, true);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    private static ColorSchemeInfo BuildScheme(
        string id,
        Dictionary<object, object> map)
    {
        var scheme = new ColorSchemeInfo { Id = id, Name = id };

        foreach (var (key, value) in map)
        {
            var property = key?.ToString();
            var stringValue = value as string ?? YamlConfig.AsString(value);
            if (!string.IsNullOrWhiteSpace(property) && stringValue is not null)
                scheme.SetProperty(property, stringValue);
        }

        if (string.IsNullOrWhiteSpace(scheme.Name))
            scheme.Name = id;

        return scheme;
    }

    private static Dictionary<string, string> ToDictionary(Dictionary<object, object> map)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in map)
        {
            var property = key?.ToString();
            var stringValue = value as string ?? YamlConfig.AsString(value);
            if (!string.IsNullOrWhiteSpace(property) && stringValue is not null)
                result[property] = stringValue;
        }

        return result;
    }
}
