using System.Collections.Generic;
using OdysseyCards.Core;

namespace OdysseyCards.Application.Ports
{
    public interface ICardResourceLoader
    {
        bool ResourceExists(string path);
        CardRewardPool LoadCardPool(string path);
        ICardData LoadCardData(string path);
        IReadOnlyList<ICardData> LoadCardDataList(IEnumerable<string> paths);
    }
}
