using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using WeaselStudio.Models;
using WeaselStudio.Services;

var failures = 0;
var checks = 0;

void Check(string name, bool condition, string? detail = null)
{
    checks++;
    if (condition)
    {
        Console.WriteLine($"PASS  {name}");
    }
    else
    {
        failures++;
        Console.WriteLine($"FAIL  {name}{(detail is null ? "" : $"  ({detail})")}");
    }
}

string FileHash(string path) =>
    File.Exists(path) ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) : "";

bool Throws(Action action)
{
    try
    {
        action();
        return false;
    }
    catch
    {
        return true;
    }
}

var environment = RimeEnvironment.Detect();
Console.WriteLine($"user dir : {environment.UserDataDir}");
Console.WriteLine($"weasel   : {environment.WeaselRoot}");
Console.WriteLine($"deployer : {environment.DeployerPath}");
Console.WriteLine($"version  : {environment.Version}");

Check("detect user data dir", Directory.Exists(environment.UserDataDir), environment.UserDataDir);
Check("detect deployer", File.Exists(environment.DeployerPath), environment.DeployerPath);

var config = new ConfigService(environment);
var schemas = config.LoadSchemas();
Check("load schemas", schemas.Count > 0, $"{schemas.Count} schema files");
Check("load enabled schemas", schemas.Any(x => x.IsEnabled),
    string.Join(", ", schemas.Where(x => x.IsEnabled).Select(x => x.Id)));

foreach (var schema in schemas.Take(3))
    Console.WriteLine($"schema    : {schema.Id} | {schema.Name} | author={schema.Author}");

var style = config.LoadStyle();
Console.WriteLine($"style    : scheme={style.ColorScheme}, font={style.FontFace} {style.FontPoint}, horizontal={style.Horizontal}, corner={style.CornerRadius}");
Check("load style font", !string.IsNullOrWhiteSpace(style.FontFace), style.FontFace);

var schemes = config.LoadColorSchemes();
Check("load color schemes", schemes.Count > 0, $"{schemes.Count} schemes");
Check("selected scheme exists", schemes.Any(x => x.Id == style.ColorScheme), style.ColorScheme);

var temp = Path.Combine(Path.GetTempPath(), "weasel-studio-test-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temp);
try
{
    var custom = Path.Combine(temp, "weasel.custom.yaml");
    if (File.Exists(config.WeaselCustomPath))
        File.Copy(config.WeaselCustomPath, custom);
    else
        File.WriteAllText(custom, "patch:\n  # keep this comment\n  \"style/horizontal\": false\n");

    var patch = new PatchFile(custom);
    patch.SetValue("style/horizontal", "true");
    patch.SetValue("style/color_scheme", "\"mint_dark_blue\"");
    patch.SetValue("style/layout/corner_radius", "12");
    patch.SetValue("style/layout/test_new_value", "42");
    patch.Save();

    var text = File.ReadAllText(custom);
    Console.WriteLine("--- patched weasel.custom.yaml (tail) ---");
    Console.WriteLine(string.Join(Environment.NewLine, text.Split('\n').TakeLast(12)));

    Check("patch parses", YamlConfig.ParseMap(text) is not null);
    Check("existing comment preserved", text.Contains("固定碧皓青浅色") || text.Contains("keep this comment"));
    Check("flat value updated", text.Contains("\"style/horizontal\": true"));
    Check("new nested path appended", text.Contains("\"style/layout/corner_radius\": 12"));
    Check("new value appended", text.Contains("\"style/layout/test_new_value\": 42"));

    var parsed = YamlConfig.ParseMap(text);
    var patchMap = YamlConfig.Get(parsed, "patch") as Dictionary<object, object>;
    Check("patch map exists", patchMap is not null);
    Check("horizontal reads true", YamlConfig.AsBool(YamlConfig.GetPatch(patchMap, "style/horizontal")) == true);
    Check("new value reads 42", YamlConfig.AsInt(YamlConfig.GetPatch(patchMap, "style/layout/test_new_value")) == 42);

    patch.RemoveValue("style/layout/test_new_value");
    patch.Save();
    var removed = File.ReadAllText(custom);
    Check("remove value", !removed.Contains("test_new_value"));

    var nested = Path.Combine(temp, "nested.custom.yaml");
    File.WriteAllText(nested, "patch:\n  style:\n    color_scheme: old_scheme\n    font_point: 11\n  # trailing comment\n");
    var nestedPatch = new PatchFile(nested);
    nestedPatch.SetValue("style/color_scheme", "\"new_scheme\"");
    nestedPatch.SetValue("style/font_point", "13");
    nestedPatch.Save();
    var nestedText = File.ReadAllText(nested);
    Console.WriteLine("--- nested patch ---");
    Console.WriteLine(nestedText);
    Check("nested value updated", nestedText.Contains("color_scheme: \"new_scheme\""));
    Check("nested numeric updated", nestedText.Contains("font_point: 13"));
    Check("nested comment preserved", nestedText.Contains("# trailing comment"));
    Check("nested parses", YamlConfig.ParseMap(nestedText) is not null);

    var defaultCustom = Path.Combine(temp, "default.custom.yaml");
    if (File.Exists(config.DefaultCustomPath))
        File.Copy(config.DefaultCustomPath, defaultCustom);
    else
        File.WriteAllText(defaultCustom, "patch:\n  # schema list comment\n  schema_list:\n    - {schema: rime_mint}\n");

    var defaultPatch = new PatchFile(defaultCustom);
    defaultPatch.SetBlock("schema_list",
        new[] { "- {schema: rime_mint}", "- {schema: double_pinyin}", "- {schema: melt_eng}" });
    defaultPatch.Save();
    var defaultText = File.ReadAllText(defaultCustom);
    Console.WriteLine("--- default.custom.yaml ---");
    Console.WriteLine(defaultText);
    Check("schema block updated", defaultText.Contains("- {schema: double_pinyin}"));
    Check("schema comment preserved", defaultText.Contains("schema list comment") || defaultText.Contains("customization"));
    Check("schema yaml parses", YamlConfig.ParseMap(defaultText) is not null);

    // --- 0.2.0: input habits / phrases, isolated from the live config ---
    var liveFiles = new[]
    {
        config.DefaultCustomPath,
        config.WeaselCustomPath,
        Path.Combine(config.UserDataDir, "rime_mint.schema.yaml"),
        Path.Combine(config.UserDataDir, "custom_phrase.txt")
    };
    var liveBefore = liveFiles.Select(FileHash).ToArray();

    var habitsDir = Path.Combine(temp, "habits");
    Directory.CreateDirectory(habitsDir);

    foreach (var name in new[] { "rime_mint.schema.yaml", "default.yaml", "default.custom.yaml", "custom_phrase.txt" })
    {
        var source = Path.Combine(config.UserDataDir, name);
        if (File.Exists(source))
            File.Copy(source, Path.Combine(habitsDir, name));
    }

    var habitsEnvironment = new RimeEnvironment
    {
        UserDataDir = habitsDir,
        WeaselRoot = environment.WeaselRoot,
        DeployerPath = environment.DeployerPath,
        Version = environment.Version,
        IsCustomUserDir = true
    };
    var habitsConfig = new ConfigService(habitsEnvironment);
    var habitsService = new InputHabitsService(habitsConfig);
    var mint = habitsConfig.LoadSchemas().FirstOrDefault(x => x.Id == "rime_mint");
    Check("habits schema loaded", mint is not null);

    if (mint is not null)
    {
        var switches = habitsService.LoadSwitches(mint).ToList();
        Check("habits switches loaded", switches.Count >= 4, $"{switches.Count} switches");

        var ascii = switches.FirstOrDefault(x => x.Name == "ascii_mode");
        var fullShape = switches.FirstOrDefault(x => x.Name == "full_shape");
        Check("ascii switch found", ascii is not null);
        Check("ascii states", ascii?.States.Count == 2,
            string.Join(",", ascii?.States ?? new List<string>()));

        if (ascii is not null && fullShape is not null)
        {
            ascii.Reset = 1;
            fullShape.Reset = 1;

            habitsService.SaveSwitches(mint, switches);
            var customPath = habitsService.GetSchemaCustomPath(mint);
            var customText = File.ReadAllText(customPath);
            Check("schema patch parses", YamlConfig.ParseMap(customText) is not null);

            var customRoot = YamlConfig.ParseMap(customText);
            var customPatch = YamlConfig.Get(customRoot, "patch") as Dictionary<object, object>;
            var saved = YamlConfig.AsList(YamlConfig.Get(customPatch, "switches"))
                .OfType<Dictionary<object, object>>()
                .ToList();
            var savedAscii = saved.FirstOrDefault(
                x => YamlConfig.AsString(YamlConfig.Get(x, "name")) == "ascii_mode");
            Check("ascii reset saved",
                YamlConfig.AsInt(YamlConfig.Get(savedAscii, "reset")) == 1);
            Check("ascii states preserved",
                YamlConfig.AsList(YamlConfig.Get(savedAscii, "states")).Count() == 2);

            var reloaded = habitsService.LoadSwitches(mint);
            Check("ascii reset reloaded",
                reloaded.First(x => x.Name == "ascii_mode").Reset == 1);
        }

        habitsService.SavePageSize(8);
        Check("page size saved", habitsService.LoadPageSize() == 8);
    }

    var phrases = new PhrasesService(habitsConfig);
    var phraseFile = Path.Combine(habitsDir, "custom_phrase.txt");
    var (phraseComments, phraseEntries) = phrases.Load(phraseFile);
    Check("phrases loaded", phraseEntries.Count > 0, $"{phraseEntries.Count} entries");
    phraseEntries.Add(new PhraseEntry { Text = "测试短语", Code = "test", Weight = "1" });
    phrases.Save(phraseFile, phraseComments, phraseEntries);
    var (_, phraseEntriesReloaded) = phrases.Load(phraseFile);
    Check("phrase saved",
        phraseEntriesReloaded.Any(x => x.Text == "测试短语" && x.Code == "test"));

    var phraseTest = Path.Combine(habitsDir, "phrase_test.txt");
    File.WriteAllText(phraseTest, "# header\n\nhello\tworld\n");
    var (testComments, testEntries) = phrases.Load(phraseTest);
    testEntries.Add(new PhraseEntry { Text = "你好", Code = "nihao" });
    phrases.Save(phraseTest, testComments, testEntries);
    var phraseText = File.ReadAllText(phraseTest);
    Check("phrase comment preserved", phraseText.StartsWith("# header", StringComparison.Ordinal));
    Check("phrase tab format", phraseText.Contains("你好\tnihao", StringComparison.Ordinal));

    // --- 0.3.0: fuzzy pinyin, isolated from the live config ---
    var fuzzyDir = Path.Combine(temp, "fuzzy");
    Directory.CreateDirectory(fuzzyDir);

    foreach (var name in new[] { "rime_mint.schema.yaml", "default.yaml", "default.custom.yaml" })
    {
        var source = Path.Combine(config.UserDataDir, name);
        if (File.Exists(source))
            File.Copy(source, Path.Combine(fuzzyDir, name));
    }

    var fuzzyEnvironment = new RimeEnvironment
    {
        UserDataDir = fuzzyDir,
        WeaselRoot = environment.WeaselRoot,
        DeployerPath = environment.DeployerPath,
        Version = environment.Version,
        IsCustomUserDir = true
    };
    var fuzzyConfig = new ConfigService(fuzzyEnvironment);
    var fuzzyService = new FuzzyPinyinService(fuzzyConfig);
    var fuzzySchema = fuzzyConfig.LoadSchemas().FirstOrDefault(x => x.Id == "rime_mint");
    Check("fuzzy schema loaded", fuzzySchema is not null);

    if (fuzzySchema is not null)
    {
        var baseRules = fuzzyService.LoadBaseAlgebra(fuzzySchema);
        Check("fuzzy base algebra", baseRules.Count > 10, $"{baseRules.Count} rules");

        var groups = FuzzyPinyinService.CreateGroups().ToList();
        Check("fuzzy groups", groups.Count == 5);

        var zh = groups.First(x => x.Id == "zh_z");
        var nasal = groups.First(x => x.Id == "en_eng");
        zh.IsEnabled = true;
        nasal.IsEnabled = true;

        var merged = fuzzyService.MergeRules(
            baseRules, Array.Empty<string>(), groups);
        Check("fuzzy rules merged",
            merged.Contains("derive/^([zcs])h/$1/") &&
            merged.Contains("derive/([aei])n$/$1ng/"));
        Check("fuzzy base preserved", merged.Contains(baseRules[0]));

        fuzzyService.SaveAlgebra(fuzzySchema, merged);
        var fuzzyCustomPath = fuzzyService.GetCustomPath(fuzzySchema);
        var fuzzyCustomText = File.ReadAllText(fuzzyCustomPath);
        Check("fuzzy custom parses", YamlConfig.ParseMap(fuzzyCustomText) is not null);

        var reloaded = fuzzyService.LoadCustomAlgebra(fuzzySchema);
        Check("fuzzy algebra saved", reloaded.Count == merged.Count,
            $"{reloaded.Count}/{merged.Count}");
        Check("fuzzy algebra has rules",
            reloaded.Contains("derive/^([zcs])h/$1/"));

        foreach (var group in groups)
            group.IsEnabled = false;

        var cleared = fuzzyService.MergeRules(baseRules, reloaded, groups);
        Check("fuzzy clear removes rules",
            !cleared.Contains("derive/^([zcs])h/$1/"));
        Check("fuzzy clear keeps base",
            cleared.Count == baseRules.Distinct().Count(),
            $"{cleared.Count}/{baseRules.Count} raw/{baseRules.Distinct().Count()} distinct");

        fuzzyService.SaveAlgebra(fuzzySchema, cleared);
        var clearedReloaded = fuzzyService.LoadCustomAlgebra(fuzzySchema);
        Check("fuzzy clear saved",
            clearedReloaded.Count == cleared.Count,
            $"{clearedReloaded.Count}/{cleared.Count}");
    }

    // --- dictionary enumeration (read-only) ---
    var dictionaryService = new DictionaryService(config);
    var dictionaries = dictionaryService.GetUserDictionaries();
    Check("dictionary list", dictionaries.Count > 0, string.Join(", ", dictionaries));
    Check("dictionary sync dir",
        dictionaryService.GetSyncDirectory().Contains("sync", StringComparison.OrdinalIgnoreCase));

    // --- 0.4.0: URL guard, SyncClipboard protocol and DPAPI settings ---
    Check("sync url rejects localhost",
        Throws(() => SyncUrlValidator.Normalize("http://localhost:5000", false)));
    Check("sync url rejects 127.0.0.1",
        Throws(() => SyncUrlValidator.Normalize("http://127.0.0.1:5000", false)));
    Check("sync url rejects private 192.168",
        Throws(() => SyncUrlValidator.Normalize("http://192.168.1.50:5000", false)));
    Check("sync url allows localhost opt-in",
        SyncUrlValidator.Normalize("http://localhost:5000", true).Host == "localhost");
    Check("sync url rejects ftp",
        Throws(() => SyncUrlValidator.Normalize("ftp://example.com/x", true)));

    var jsonUrl = SyncUrlValidator.GetJsonUrl(new Uri("http://example.com/clipboard"));
    Check("sync json url", jsonUrl.EndsWith("/clipboard/SyncClipboard.json",
        StringComparison.Ordinal));

    var handler = new FakeSyncHandler
    {
        Json = "{\"type\":\"Text\",\"text\":\"你好\",\"hash\":\"ABC\",\"hasData\":false}"
    };

    using (var syncClient = new SyncClipboardClient(
               "http://localhost:5000/clipboard", "u", "p", true, handler))
    {
        var (pulled, etag) = await syncClient.PullAsync(null, CancellationToken.None);
        Check("sync client pull", pulled?.Text == "你好" && etag == "\"abc\"");
        Check("sync client auth", handler.LastAuthorizationStartsWithBasic);

        var (notModified, _) = await syncClient.PullAsync("\"abc\"", CancellationToken.None);
        Check("sync client 304", notModified is null);

        await syncClient.PushTextAsync("hello", "HASH", CancellationToken.None);
        Check("sync client put",
            handler.LastMethod == "PUT" &&
            handler.LastBody?.Contains("\"Text\"", StringComparison.Ordinal) == true &&
            handler.LastBody?.Contains("HASH", StringComparison.Ordinal) == true);
    }

    var settingsDir = Path.Combine(temp, "sync-settings");
    var settingsStore = new SyncSettingsStore(settingsDir);
    var savedSettings = new SyncSettings
    {
        Enabled = true,
        ServerUrl = "http://example.com/clipboard",
        Username = "user",
        Password = "secret",
        IntervalSeconds = 7,
        MinimizeToTray = true,
        AutoStart = false,
        AllowPrivateAddresses = true
    };
    settingsStore.Save(savedSettings);
    var loadedSettings = settingsStore.Load();
    Check("sync settings roundtrip",
        loadedSettings.Enabled &&
        loadedSettings.ServerUrl == savedSettings.ServerUrl &&
        loadedSettings.Username == "user" &&
        loadedSettings.Password == "secret" &&
        loadedSettings.IntervalSeconds == 7 &&
        loadedSettings.AllowPrivateAddresses);
    Check("sync settings password not plaintext",
        !File.ReadAllText(settingsStore.SettingsPath).Contains("secret", StringComparison.Ordinal));

    var liveAfter = liveFiles.Select(FileHash).ToArray();
    Check("live config untouched", liveBefore.SequenceEqual(liveAfter));
}
finally
{
    try { Directory.Delete(temp, true); } catch { }
}

Console.WriteLine();
Console.WriteLine($"{checks - failures}/{checks} checks passed.");
return failures == 0 ? 0 : 1;

internal sealed class FakeSyncHandler : HttpMessageHandler
{
    public string Json { get; set; } = "{}";
    public string? LastMethod { get; private set; }
    public string? LastBody { get; private set; }
    public bool LastAuthorizationStartsWithBasic { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        LastMethod = request.Method.Method;
        LastAuthorizationStartsWithBasic =
            request.Headers.Authorization?.Scheme == "Basic";

        if (request.Content is not null)
            LastBody = await request.Content.ReadAsStringAsync(cancellationToken);

        if (request.Method == HttpMethod.Get &&
            request.Headers.TryGetValues("If-None-Match", out var values) &&
            values.Contains("\"abc\""))
        {
            return new HttpResponseMessage(HttpStatusCode.NotModified);
        }

        var response = new HttpResponseMessage(HttpStatusCode.OK);
        if (request.Method == HttpMethod.Get)
        {
            response.Content = new StringContent(
                Json, Encoding.UTF8, "application/json");
        }

        response.Headers.ETag = new EntityTagHeaderValue("\"abc\"");
        return response;
    }
}
