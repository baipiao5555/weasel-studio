using System.IO;
using System.Text;
using WeaselStudio.Models;

namespace WeaselStudio.Services;

public sealed class PhrasesService
{
    private readonly ConfigService _config;

    public PhrasesService(ConfigService config)
    {
        _config = config;
    }

    public IReadOnlyList<string> GetPhraseFiles()
    {
        var result = new List<string>();

        void Add(string name)
        {
            var path = Path.Combine(_config.UserDataDir, name);
            if (File.Exists(path))
                result.Add(path);
        }

        Add("custom_phrase.txt");
        Add("custom_phrase_double.txt");

        if (result.Count == 0)
            result.Add(Path.Combine(_config.UserDataDir, "custom_phrase.txt"));

        return result;
    }

    public (List<string> Comments, List<PhraseEntry> Entries) Load(string path)
    {
        var comments = new List<string>();
        var entries = new List<PhraseEntry>();

        if (!File.Exists(path))
            return (comments, entries);

        foreach (var line in File.ReadAllLines(path))
        {
            if (string.IsNullOrWhiteSpace(line) ||
                line.TrimStart().StartsWith("#", StringComparison.Ordinal))
            {
                comments.Add(line);
                continue;
            }

            var parts = line.Split('\t');
            if (parts.Length < 2)
                continue;

            entries.Add(new PhraseEntry
            {
                Text = parts[0].Trim(),
                Code = parts[1].Trim(),
                Weight = parts.Length > 2 ? parts[2].Trim() : ""
            });
        }

        return (comments, entries);
    }

    public void Save(string path, IEnumerable<string> comments, IEnumerable<PhraseEntry> entries)
    {
        var lines = new List<string>();
        lines.AddRange(comments);

        var row = 1;
        foreach (var entry in entries)
        {
            row++;
            var text = entry.Text.Trim();
            var code = entry.Code.Trim();
            var weight = entry.Weight.Trim();

            if (text.Length == 0 && code.Length == 0)
                continue;

            if (text.Length == 0)
                throw new InvalidOperationException($"第 {row} 行缺少短语。");

            if (code.Length == 0)
                throw new InvalidOperationException($"第 {row} 行缺少编码。");

            if (text.Contains('\t') || text.Contains('\r') || text.Contains('\n'))
                throw new InvalidOperationException($"第 {row} 行的短语不能包含制表符或换行。");

            if (code.Contains('\t') || code.Contains('\r') || code.Contains('\n') ||
                code.Contains(' '))
            {
                throw new InvalidOperationException($"第 {row} 行的编码不能包含空格、制表符或换行。");
            }

            if (weight.Contains('\t') || weight.Contains('\r') || weight.Contains('\n'))
                throw new InvalidOperationException($"第 {row} 行的权重不能包含制表符或换行。");

            var line = $"{text}\t{code}";
            if (weight.Length > 0)
                line += $"\t{weight}";

            lines.Add(line);
        }

        BackupService.Backup(path);

        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directory))
            throw new InvalidOperationException("无法确定短语文件目录。");

        Directory.CreateDirectory(directory);

        var hasBom = false;
        if (File.Exists(path))
        {
            var bytes = File.ReadAllBytes(path);
            hasBom = bytes.Length >= 3 &&
                     bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        }

        File.WriteAllLines(path, lines, new UTF8Encoding(hasBom));
    }
}
