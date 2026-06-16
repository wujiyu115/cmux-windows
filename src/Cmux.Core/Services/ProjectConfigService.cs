using System.Text.Json;
using Cmux.Core.Models;

namespace Cmux.Core.Services;

public static class ProjectConfigService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static ProjectConfig? Parse(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<ProjectConfig>(json, Options);
        }
        catch
        {
            return null;
        }
    }

    public static string? FindConfigPath(string? directory)
    {
        if (string.IsNullOrEmpty(directory)) return null;

        var current = directory;
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        while (!string.IsNullOrEmpty(current))
        {
            var candidate = Path.Combine(current, ".cmux", "cmux.json");
            if (File.Exists(candidate)) return candidate;

            candidate = Path.Combine(current, "cmux.json");
            if (File.Exists(candidate)) return candidate;

            if (string.Equals(current, home, StringComparison.OrdinalIgnoreCase))
                break;

            var parent = Directory.GetParent(current)?.FullName;
            if (parent == current) break;
            current = parent;
        }

        return null;
    }

    public static ProjectConfig? LoadForDirectory(string? directory)
    {
        var path = FindConfigPath(directory);
        if (path == null) return null;
        try
        {
            var json = File.ReadAllText(path);
            return Parse(json);
        }
        catch
        {
            return null;
        }
    }
}
