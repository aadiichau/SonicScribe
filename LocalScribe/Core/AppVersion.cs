using System.Reflection;

namespace LocalScribe.Core;

public static class AppVersion
{
    public static string Current
    {
        get
        {
            var informational = Assembly
                .GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;

            if (!string.IsNullOrWhiteSpace(informational))
            {
                var plusIndex = informational.IndexOf('+', StringComparison.Ordinal);
                return plusIndex >= 0 ? informational[..plusIndex] : informational;
            }

            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return version is null ? "1.0.0" : $"{version.Major}.{version.Minor}.{version.Build}";
        }
    }

    public static bool IsNewer(string latestVersion, string currentVersion)
    {
        if (!TryParse(latestVersion, out var latest) || !TryParse(currentVersion, out var current))
        {
            return false;
        }

        return latest > current;
    }

    private static bool TryParse(string value, out Version version)
    {
        value = value.Trim().TrimStart('v', 'V');
        var parts = value.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            version = new Version(0, 0);
            return false;
        }

        var major = int.Parse(parts[0]);
        var minor = int.Parse(parts[1]);
        var build = parts.Length > 2 ? int.Parse(parts[2]) : 0;
        var revision = parts.Length > 3 ? int.Parse(parts[3]) : 0;
        version = new Version(major, minor, build, revision);
        return true;
    }
}