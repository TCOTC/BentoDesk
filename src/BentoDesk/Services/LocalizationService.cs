using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace BentoDesk.Services;

public sealed class LocalizationService
{
    public const string LanguageChinese = "zh-CN";

    /// <summary>
    /// Retained for existing subscribers. Language switching is not supported.
    /// </summary>
#pragma warning disable CS0067 // Event is retained for existing subscribers but never raised.
    public event Action? LanguageChanged;
#pragma warning restore CS0067

    public LocalizationService(SettingsService? _ = null)
    {
    }

    public string LanguageSetting => LanguageChinese;

    public string CurrentCultureName => LanguageChinese;

    public string T(string key)
    {
        return ZhCn.TryGetValue(key, out string? value) ? value : key;
    }

    public string Format(string key, params object[] args)
    {
        return string.Format(CultureInfo.CurrentCulture, T(key), args);
    }

    public static string DefaultText(string key)
    {
        return ZhCn.TryGetValue(key, out string? value) ? value : key;
    }

    public static string DefaultFormat(string key, params object[] args)
    {
        return string.Format(CultureInfo.CurrentCulture, DefaultText(key), args);
    }

    private static Dictionary<string, string>? _zhCn;
    private static readonly object s_loadLock = new();

    private static Dictionary<string, string> ZhCn
    {
        get
        {
            if (_zhCn is not null) return _zhCn;
            lock (s_loadLock)
            {
                _zhCn ??= LoadStringResource("BentoDesk.Strings.zh-CN.json");
            }
            return _zhCn;
        }
    }

    private static Dictionary<string, string> LoadStringResource(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            System.Diagnostics.Debug.WriteLine($"[LocalizationService] Resource not found: {resourceName}");
            return [];
        }
        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [];
    }
}
