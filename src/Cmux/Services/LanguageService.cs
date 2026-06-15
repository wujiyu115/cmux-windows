using System.Windows;

namespace Cmux.Services;

public static class LanguageService
{
    private static ResourceDictionary? _currentDictionary;

    public static string Current { get; private set; } = "en";

    public static void SetLanguage(string lang)
    {
        Current = lang;
        var uri = new System.Uri($"Strings/Strings.{lang}.xaml", System.UriKind.Relative);
        var newDict = new ResourceDictionary { Source = uri };

        var mergedDicts = Application.Current.Resources.MergedDictionaries;
        if (_currentDictionary != null)
            mergedDicts.Remove(_currentDictionary);

        mergedDicts.Add(newDict);
        _currentDictionary = newDict;
    }

    public static string Lang(string key)
    {
        return Application.Current.TryFindResource(key) as string ?? key;
    }

    public static string Lang(string key, params object[] args)
    {
        var template = Application.Current.TryFindResource(key) as string ?? key;
        return string.Format(template, args);
    }
}
