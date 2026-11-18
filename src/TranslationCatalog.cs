using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;
using Newtonsoft.Json;

namespace RTLC.FallenAces
{
    public sealed class TranslationCatalog
    {
        private readonly Dictionary<string, string> entries = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> cache = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly List<(Regex pattern, string replacement)> templates = new List<(Regex, string)>();
        private static readonly Regex Tag = new Regex("(<[^>]+>)", RegexOptions.Compiled);
        private static readonly Regex Placeholder = new Regex(@"\{(\d+)(?:[^{}]*)\}");
        public int Count => entries.Count;

        public void Load(string directory)
        {
            LoadTexts(Directory.GetFiles(directory, "*.json")
                .OrderBy(p => p, StringComparer.Ordinal)
                .Select(p => (name: p, contents: File.ReadAllText(p))));
        }

        public void LoadTexts(IEnumerable<(string name, string contents)> catalogs)
        {
            entries.Clear(); cache.Clear(); templates.Clear();
            foreach (var catalog in catalogs)
            {
                var data = JsonConvert.DeserializeObject<Dictionary<string, string>>(catalog.contents);
                if (data == null) throw new InvalidDataException("Empty translation file: " + catalog.name);
                foreach (var pair in data)
                    if (!string.IsNullOrEmpty(pair.Key) && !string.IsNullOrEmpty(pair.Value)) entries[pair.Key] = pair.Value;
            }
            // Several shipped text files are Windows-1252, but the game reads them as
            // UTF-8. Match those exact in-memory replacement characters as well.
            foreach (var pair in entries.ToArray())
            {
                string alias = LegacyAlias(pair.Key);
                if (alias != null && alias != pair.Key && !entries.ContainsKey(alias)) entries.Add(alias, pair.Value);
            }
            foreach (var pair in entries.Where(p => Placeholder.IsMatch(p.Key)).OrderByDescending(p => p.Key.Length))
            {
                int position = 0;
                string pattern = "\\A";
                var groups = new HashSet<string>();
                foreach (Match m in Placeholder.Matches(pair.Key))
                {
                    pattern += Regex.Escape(pair.Key.Substring(position, m.Index - position));
                    string group = "p" + m.Groups[1].Value;
                    pattern += groups.Add(group) ? "(?<" + group + ">.+?)" : "\\k<" + group + ">";
                    position = m.Index + m.Length;
                }
                pattern += Regex.Escape(pair.Key.Substring(position)) + "\\z";
                templates.Add((new Regex(pattern, RegexOptions.Singleline, TimeSpan.FromMilliseconds(20)), pair.Value));
            }
        }

        private static string LegacyAlias(string source)
        {
            // Unity strips I18N.West: do not require Encoding.GetEncoding(1252).
            const string high = "€\u0081‚ƒ„…†‡ˆ‰Š‹Œ\u008dŽ\u008f\u0090‘’“”•–—˜™š›œ\u009džŸ";
            var bytes = new byte[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                char c = source[i];
                if (c < 128 || (c >= 160 && c <= 255)) bytes[i] = (byte)c;
                else
                {
                    int index = high.IndexOf(c);
                    if (index < 0) return null;
                    bytes[i] = (byte)(128 + index);
                }
            }
            return Encoding.UTF8.GetString(bytes);
        }

        public string Translate(string source)
        {
            if (string.IsNullOrEmpty(source)) return source;
            if (entries.TryGetValue(source, out string exact)) return exact;
            if (cache.TryGetValue(source, out string previous)) return previous;
            string result = TranslatePart(source);
            if (result == source && source.IndexOf('<') >= 0)
                result = string.Concat(Tag.Split(source).Select(s => s.StartsWith("<", StringComparison.Ordinal) ? s : TranslatePart(s)));
            // Bound the cache: timers, counters and user-entered text are not an unbounded history.
            if (cache.Count >= 4096) cache.Clear();
            cache[source] = result;
            return result;
        }

        private string TranslatePart(string source)
        {
            if (entries.TryGetValue(source, out string exact)) return exact;
            string trimmed = source.Trim();
            if (entries.TryGetValue(trimmed, out exact))
            {
                int offset = source.IndexOf(trimmed, StringComparison.Ordinal);
                return source.Substring(0, offset) + exact + source.Substring(offset + trimmed.Length);
            }
            foreach (var template in templates)
            {
                try
                {
                    Match match = template.pattern.Match(source);
                    if (match.Success) return Placeholder.Replace(template.replacement, p =>
                    {
                        string captured = match.Groups["p" + p.Groups[1].Value].Value;
                        // Captures can be item names or objective titles. Strictly shrinking
                        // input prevents recursive templates from matching themselves forever.
                        return captured.Length < source.Length ? Translate(captured) : captured;
                    });
                }
                catch (RegexMatchTimeoutException) { }
            }
            // Each shorter line may contain its own rich-text tag. Re-enter the
            // public translation path so mixed translated/plain lines do not
            // skip tag-aware translation on warnings in save-slot details.
            if (source.IndexOf('\n') >= 0) return string.Join("\n", source.Split('\n').Select(Translate));
            return source;
        }
    }
}
