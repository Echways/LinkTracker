using System.Text;
using Microsoft.Extensions.Configuration;

namespace LinkTracker.EnvReader;

public static class DotEnv
{
    public static IReadOnlyDictionary<string, string?> Load(string path, bool optional = true)
    {
        if (!File.Exists(path))
        {
            if (optional)
            {
                return new Dictionary<string, string?>();
            }

            throw new FileNotFoundException($"'.env' не найден: {path}");
        }

        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in File.ReadAllLines(path, Encoding.UTF8))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            if (line.StartsWith("export ", StringComparison.OrdinalIgnoreCase))
            {
                line = line["export ".Length..].TrimStart();
            }

            var idx = line.IndexOf('=');
            if (idx <= 0)
            {
                continue;
            }

            var key = NormalizeKey(line[..idx].Trim());
            var value = TrimQuotes(line[(idx + 1)..].Trim());

            result[key] = value;
        }

        return result;
    }

    public static string NormalizeKey(string key)
    {
        return key.Replace("__", ConfigurationPath.KeyDelimiter);
    }

    private static string TrimQuotes(string value)
    {
        if (value.Length >= 2)
        {
            var first = value[0];
            var last = value[^1];
            if ((first == '"' && last == '"') || (first == '\'' && last == '\''))
            {
                return value[1..^1];
            }
        }

        return value;
    }
}