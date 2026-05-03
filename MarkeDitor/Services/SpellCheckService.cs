using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WeCantSpell.Hunspell;

namespace MarkeDitor.Services;

/// <summary>
/// Loads one or more Hunspell dictionaries from /usr/share/hunspell (Linux)
/// or a fallback bundled location and exposes Check / Suggest. A word is
/// considered correct if it's known to ANY loaded dictionary OR present in
/// the user's custom dictionary stored in settings.
/// </summary>
public class SpellCheckService
{
    private readonly List<WordList> _dictionaries = new();
    private HashSet<string> _custom = new(StringComparer.Ordinal);

    public bool IsReady => _dictionaries.Count > 0;
    public IReadOnlyList<string> LoadedLanguages { get; private set; } = Array.Empty<string>();

    private static readonly string[] SearchPaths =
    {
        "/usr/share/hunspell",
        "/usr/share/myspell/dicts",
        "/usr/local/share/hunspell",
    };

    public void Load(IEnumerable<string> languageCodes, IEnumerable<string> customWords)
    {
        _dictionaries.Clear();
        var loaded = new List<string>();

        foreach (var lang in languageCodes)
        {
            var (aff, dic) = FindFiles(lang);
            if (aff == null || dic == null) continue;
            try
            {
                var wl = WordList.CreateFromFiles(dic, aff);
                _dictionaries.Add(wl);
                loaded.Add(lang);
            }
            catch { /* skip broken dict */ }
        }

        _custom = new HashSet<string>(customWords ?? Array.Empty<string>(), StringComparer.Ordinal);
        LoadedLanguages = loaded;
    }

    public void AddToCustom(string word)
    {
        if (!string.IsNullOrWhiteSpace(word)) _custom.Add(word);
    }

    public bool Check(string word)
    {
        if (string.IsNullOrEmpty(word)) return true;
        if (_custom.Contains(word)) return true;
        foreach (var d in _dictionaries)
            if (d.Check(word)) return true;
        return false;
    }

    public IEnumerable<string> Suggest(string word, int max = 8)
    {
        if (string.IsNullOrEmpty(word)) yield break;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var d in _dictionaries)
        {
            foreach (var s in d.Suggest(word))
            {
                if (seen.Add(s))
                {
                    yield return s;
                    if (seen.Count >= max) yield break;
                }
            }
        }
    }

    private static (string? aff, string? dic) FindFiles(string lang)
    {
        foreach (var dir in SearchPaths)
        {
            if (!Directory.Exists(dir)) continue;
            var aff = Path.Combine(dir, lang + ".aff");
            var dic = Path.Combine(dir, lang + ".dic");
            if (File.Exists(aff) && File.Exists(dic)) return (aff, dic);
            // Try lowercased / loose match
            var match = Directory.GetFiles(dir, lang + "*.dic").FirstOrDefault();
            if (match != null)
            {
                var matchAff = Path.ChangeExtension(match, ".aff");
                if (File.Exists(matchAff)) return (matchAff, match);
            }
        }
        return (null, null);
    }
}
