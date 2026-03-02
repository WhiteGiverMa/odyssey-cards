namespace OdysseyCards.Localization;

public class ConcatLocalStr
{
    private readonly object _left;
    private readonly object _right;

    public ConcatLocalStr(object left, object right)
    {
        _left = left;
        _right = right;
    }

    public string Resolve()
    {
        string leftStr = _left switch
        {
            null => "",
            LocalStr ls => ls.Resolve(),
            ConcatLocalStr cs => cs.Resolve(),
            string s => s,
            _ => _left.ToString() ?? ""
        };

        string rightStr = _right switch
        {
            null => "",
            LocalStr ls => ls.Resolve(),
            ConcatLocalStr cs => cs.Resolve(),
            string s => s,
            _ => _right.ToString() ?? ""
        };

        return leftStr + rightStr;
    }

    public override string ToString() => Resolve();

    public static implicit operator string(ConcatLocalStr concatStr) => concatStr?.Resolve();
}
