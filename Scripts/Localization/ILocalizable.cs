using System.Collections.Generic;

namespace OdysseyCards.Localization;

public interface ILocalizable
{
    string LocalizationPrefix { get; }
    string LocalizationId { get; }

    LocalStr Local(string field, Dictionary<string, object> parameters = null)
    {
        string key = $"{LocalizationPrefix}.{LocalizationId}.{field}";
        return new LocalStr(key, parameters);
    }

    bool HasLocal(string field)
    {
        string key = $"{LocalizationPrefix}.{LocalizationId}.{field}";
        return Localization.HasKey(key);
    }
}
