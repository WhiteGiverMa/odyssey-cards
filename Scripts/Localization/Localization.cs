using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Godot;

namespace OdysseyCards.Localization;

public static class Localization
{
    private static readonly Dictionary<string, Dictionary<string, string>> _translations = new();
    private static string _currentLanguage = "en";
    private static bool _initialized;

    private const string LocalizationPath = "res://Resources/Localization/";

    public static string CurrentLanguage
    {
        get => _currentLanguage;
        set => SetLanguage(value);
    }

    public static IReadOnlyList<string> AvailableLanguages
    {
        get
        {
            List<string> languages = new(_translations.Keys);
            if (languages.Count == 0)
            {
                languages.Add("en");
            }

            return languages;
        }
    }

    public static event Action<string> OnLanguageChanged;

    public static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        LoadAllTranslations();
        _initialized = true;
        GD.Print($"[Localization] Initialized with {AvailableLanguages.Count} languages: {string.Join(", ", AvailableLanguages)}");
    }

    public static void SetLanguage(string language)
    {
        if (string.IsNullOrEmpty(language))
        {
            return;
        }

        if (!_translations.ContainsKey(language))
        {
            GD.PrintErr($"[Localization] Language '{language}' not found. Available: {string.Join(", ", AvailableLanguages)}");
            return;
        }

        if (_currentLanguage == language)
        {
            return;
        }

        _currentLanguage = language;
        OnLanguageChanged?.Invoke(language);
        GD.Print($"[Localization] Language changed to: {language}");
    }

    public static string T(string key, string defaultValue = null, Dictionary<string, object> parameters = null)
    {
        if (!_initialized)
        {
            Initialize();
        }

        if (string.IsNullOrEmpty(key))
        {
            return defaultValue ?? key;
        }

        string translation = GetTranslation(key);

        if (translation == null)
        {
            return defaultValue ?? key;
        }

        if (parameters != null && parameters.Count > 0)
        {
            translation = SubstitutePlaceholders(translation, parameters);
        }

        return translation;
    }

    public static string T(string key, Dictionary<string, object> parameters)
    {
        return T(key, null, parameters);
    }

    public static string T(string key, string defaultValue, params (string key, object value)[] parameters)
    {
        Dictionary<string, object> dict = new();
        foreach ((string k, object v) in parameters)
        {
            dict[k] = v;
        }

        return T(key, defaultValue, dict);
    }

    public static bool HasKey(string key, string language = null)
    {
        if (!_initialized)
        {
            Initialize();
        }

        string lang = language ?? _currentLanguage;

        if (!_translations.TryGetValue(lang, out Dictionary<string, string> langDict))
        {
            return false;
        }

        return langDict.ContainsKey(key);
    }

    public static string GetTranslation(string key, string language = null)
    {
        string lang = language ?? _currentLanguage;

        if (!_translations.TryGetValue(lang, out Dictionary<string, string> langDict))
        {
            return null;
        }

        return langDict.TryGetValue(key, out string value) ? value : null;
    }

    private static void LoadAllTranslations()
    {
        _translations.Clear();

        using DirAccess dir = DirAccess.Open(LocalizationPath);
        if (dir == null)
        {
            GD.Print($"[Localization] Directory not found: {LocalizationPath}, creating default 'en' entry");
            _translations["en"] = new Dictionary<string, string>();
            return;
        }

        dir.ListDirBegin();
        string fileName = dir.GetNext();

        while (!string.IsNullOrEmpty(fileName))
        {
            if (!dir.CurrentIsDir() && (fileName.EndsWith(".yaml", StringComparison.Ordinal) || fileName.EndsWith(".yml", StringComparison.Ordinal)))
            {
                string langCode = Path.GetFileNameWithoutExtension(fileName);
                string filePath = LocalizationPath + fileName;
                LoadTranslationFile(filePath, langCode);
            }

            fileName = dir.GetNext();
        }

        dir.ListDirEnd();

        if (!_translations.ContainsKey("en"))
        {
            _translations["en"] = new Dictionary<string, string>();
        }
    }

    private static void LoadTranslationFile(string filePath, string languageCode)
    {
        using FileAccess file = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);
        if (file == null)
        {
            GD.PrintErr($"[Localization] Failed to open file: {filePath}");
            return;
        }

        string content = file.GetAsText();
        Dictionary<string, object> parsed = YamlParser.Parse(content);
        Dictionary<string, string> flattened = YamlParser.Flatten(parsed);

        _translations[languageCode] = flattened;
        GD.Print($"[Localization] Loaded {flattened.Count} translations for language: {languageCode}");
    }

    private static string SubstitutePlaceholders(string template, Dictionary<string, object> parameters)
    {
        if (string.IsNullOrEmpty(template))
        {
            return template;
        }

        string result = template;

        foreach (KeyValuePair<string, object> kvp in parameters)
        {
            string placeholder = $"{{{kvp.Key}}}";
            string value = kvp.Value?.ToString() ?? string.Empty;
            result = result.Replace(placeholder, value);
        }

        Regex placeholderPattern = new(@"\{(\w+)\}");
        result = placeholderPattern.Replace(result, match =>
        {
            string key = match.Groups[1].Value;
            if (parameters.TryGetValue(key, out object paramValue))
            {
                return paramValue?.ToString() ?? string.Empty;
            }

            return match.Value;
        });

        return result;
    }

    public static void Reload()
    {
        _initialized = false;
        _translations.Clear();
        Initialize();
    }

    public static int GetTranslationCount(string language = null)
    {
        string lang = language ?? _currentLanguage;

        if (_translations.TryGetValue(lang, out Dictionary<string, string> langDict))
        {
            return langDict.Count;
        }

        return 0;
    }
}
