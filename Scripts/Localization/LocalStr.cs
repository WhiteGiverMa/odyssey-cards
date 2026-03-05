using System.Collections.Generic;

namespace OdysseyCards.Localization;

public class LocalStr
{
    private readonly string _key;
    private readonly Dictionary<string, object> _parameters;

    public LocalStr(string key, Dictionary<string, object> parameters = null)
    {
        _key = key;
        _parameters = parameters;
    }

    public string Resolve()
    {
        return Localization.T(_key, _key, _parameters);
    }

    public override string ToString() => Resolve();

    public static ConcatLocalStr operator +(LocalStr left, LocalStr right) => new(left, right);
    public static ConcatLocalStr operator +(LocalStr left, string right) => new(left, right);
    public static ConcatLocalStr operator +(string left, LocalStr right) => new(left, right);

    public static ConcatLocalStr Add(LocalStr left, LocalStr right) => new(left, right);
    public static ConcatLocalStr Add(LocalStr left, string right) => new(left, right);
    public static ConcatLocalStr Add(string left, LocalStr right) => new(left, right);

    public static implicit operator string(LocalStr localStr) => localStr?.Resolve();
}
