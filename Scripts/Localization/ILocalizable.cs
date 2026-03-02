using System.Collections.Generic;

namespace OdysseyCards.Localization;

public interface ILocalizable
{
    string LocalizationPrefix { get; }
    string LocalizationId { get; }

    LocalStr Local(string field, Dictionary<string, object> parameters = null);

    bool HasLocal(string field);
}

public static class LocalizableExtensions
{
    public static LocalStr Local(this ILocalizable localizable, string field, Dictionary<string, object> parameters = null)
    {
        string key = $"{localizable.LocalizationPrefix}.{localizable.LocalizationId}.{field}";
        return new LocalStr(key, parameters);
    }

    public static bool HasLocal(this ILocalizable localizable, string field)
    {
        string key = $"{localizable.LocalizationPrefix}.{localizable.LocalizationId}.{field}";
        return Localization.HasKey(key);
    }
}
