using System.Globalization;
using System.Text;

namespace burroQueue.Infrastructure.Persistence;

public static class TomlDocumentSerializer
{
    public static (Dictionary<string, Dictionary<string, string>> Sections, Dictionary<string, List<Dictionary<string, string>>> TableArrays) Parse(string tomlText)
    {
        Dictionary<string, Dictionary<string, string>> sections = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, List<Dictionary<string, string>>> tableArrays = new(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(tomlText))
        {
            return (sections, tableArrays);
        }

        Dictionary<string, string> activeMap = GetOrCreateSection(sections, string.Empty);
        ReadOnlySpan<char> span = tomlText.AsSpan();

        int pos = 0;
        while (pos < span.Length)
        {
            int lineEnd = span[pos..].IndexOfAny('\r', '\n');
            ReadOnlySpan<char> rawLine;
            if (lineEnd < 0)
            {
                rawLine = span[pos..];
                pos = span.Length;
            }
            else
            {
                rawLine = span.Slice(pos, lineEnd);
                pos += lineEnd + 1;
                if (pos < span.Length && span[pos - 1] == '\r' && span[pos] == '\n')
                {
                    pos++;
                }
            }

            ReadOnlySpan<char> line = StripComment(rawLine).Trim();
            if (line.IsEmpty)
            {
                continue;
            }

            if (line.Length >= 5 && line.StartsWith("[[", StringComparison.Ordinal) && line.EndsWith("]]", StringComparison.Ordinal))
            {
                string arrayName = line[2..^2].Trim().ToString();
                if (!tableArrays.TryGetValue(arrayName, out List<Dictionary<string, string>>? list))
                {
                    list = [];
                    tableArrays[arrayName] = list;
                }

                Dictionary<string, string> entry = new(StringComparer.OrdinalIgnoreCase);
                list.Add(entry);
                activeMap = entry;
                continue;
            }

            if (line.Length >= 3 && line[0] == '[' && line[^1] == ']')
            {
                string sectionName = line[1..^1].Trim().ToString();
                activeMap = GetOrCreateSection(sections, sectionName);
                continue;
            }

            int eqIdx = line.IndexOf('=');
            if (eqIdx <= 0)
            {
                continue;
            }

            string key = line[..eqIdx].Trim().ToString();
            string rawVal = line[(eqIdx + 1)..].Trim().ToString();
            activeMap[key] = rawVal;
        }

        return (sections, tableArrays);
    }

    public static string EscapeString(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "\"\"";
        }

        StringBuilder sb = new(value.Length + 4);
        sb.Append('"');
        foreach (char c in value)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default: sb.Append(c); break;
            }
        }

        sb.Append('"');
        return sb.ToString();
    }

    public static string UnescapeString(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return string.Empty;
        }

        ReadOnlySpan<char> span = rawValue.AsSpan().Trim();
        if (span.Length >= 2 && span[0] == '"' && span[^1] == '"')
        {
            span = span[1..^1];
            StringBuilder sb = new(span.Length);
            for (int i = 0; i < span.Length; i++)
            {
                if (span[i] == '\\' && i + 1 < span.Length)
                {
                    i++;
                    sb.Append(span[i] switch
                    {
                        'n' => '\n',
                        'r' => '\r',
                        't' => '\t',
                        '"' => '"',
                        '\\' => '\\',
                        _ => span[i]
                    });
                }
                else
                {
                    sb.Append(span[i]);
                }
            }

            return sb.ToString();
        }

        return span.ToString();
    }

    public static bool ParseBool(Dictionary<string, string>? section, string key, bool fallback)
    {
        if (section is not null && section.TryGetValue(key, out string? raw) && bool.TryParse(raw.Trim(), out bool parsed))
        {
            return parsed;
        }

        return fallback;
    }

    public static int ParseInt(Dictionary<string, string>? section, string key, int fallback)
    {
        if (section is not null && section.TryGetValue(key, out string? raw) && int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
        {
            return parsed;
        }

        return fallback;
    }

    public static double ParseDouble(Dictionary<string, string>? section, string key, double fallback)
    {
        if (section is not null && section.TryGetValue(key, out string? raw) && double.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
        {
            return parsed;
        }

        return fallback;
    }

    public static string ParseString(Dictionary<string, string>? section, string key, string fallback = "")
    {
        if (section is not null && section.TryGetValue(key, out string? raw))
        {
            return UnescapeString(raw);
        }

        return fallback;
    }

    public static string FormatStringArray(IEnumerable<string> items)
    {
        StringBuilder sb = new("[");
        bool first = true;
        foreach (string item in items)
        {
            if (!first)
            {
                sb.Append(", ");
            }

            sb.Append(EscapeString(item));
            first = false;
        }

        sb.Append(']');
        return sb.ToString();
    }

    public static List<string> ParseStringArray(Dictionary<string, string>? section, string key)
    {
        List<string> results = [];
        if (section is null || !section.TryGetValue(key, out string? raw) || string.IsNullOrWhiteSpace(raw))
        {
            return results;
        }

        ReadOnlySpan<char> span = raw.AsSpan().Trim();
        if (span.Length < 2 || span[0] != '[' || span[^1] != ']')
        {
            return results;
        }

        span = span[1..^1];
        bool inQuotes = false;
        bool escaped = false;
        StringBuilder current = new();

        for (int i = 0; i < span.Length; i++)
        {
            char c = span[i];
            if (escaped)
            {
                current.Append('\\').Append(c);
                escaped = false;
                continue;
            }

            if (c == '\\' && inQuotes)
            {
                escaped = true;
                continue;
            }

            if (c == '"')
            {
                inQuotes = !inQuotes;
                current.Append(c);
                continue;
            }

            if (c == ',' && !inQuotes)
            {
                string token = current.ToString().Trim();
                if (token.Length > 0)
                {
                    results.Add(UnescapeString(token));
                }

                current.Clear();
                continue;
            }

            current.Append(c);
        }

        string last = current.ToString().Trim();
        if (last.Length > 0)
        {
            results.Add(UnescapeString(last));
        }

        return results;
    }

    public static async Task WriteAtomicallyAsync(string targetPath, string content, CancellationToken cancellationToken = default)
    {
        string? directory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string tempPath = $"{targetPath}.tmp";
        await File.WriteAllTextAsync(tempPath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), cancellationToken).ConfigureAwait(false);
        File.Move(tempPath, targetPath, overwrite: true);
    }

    private static Dictionary<string, string> GetOrCreateSection(Dictionary<string, Dictionary<string, string>> sections, string name)
    {
        if (!sections.TryGetValue(name, out Dictionary<string, string>? dict))
        {
            dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            sections[name] = dict;
        }

        return dict;
    }

    private static ReadOnlySpan<char> StripComment(ReadOnlySpan<char> line)
    {
        bool inQuotes = false;
        bool escaped = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (c == '\\' && inQuotes)
            {
                escaped = true;
                continue;
            }

            if (c == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (c == '#' && !inQuotes)
            {
                return line[..i];
            }
        }

        return line;
    }
}

