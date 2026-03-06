using System.Collections.Generic;
using Godot;
using OdysseyCards.Application.Ports;
using OdysseyCards.Core;

namespace OdysseyCards.Infrastructure.Godot.ResourceLoading
{
    public sealed class GodotCardResourceLoader : ICardResourceLoader
    {
        public bool ResourceExists(string path)
        {
            return ResourceLoader.Exists(path);
        }

        public CardRewardPool LoadCardPool(string path)
        {
            if (!ResourceLoader.Exists(path))
            {
                return null;
            }

            return ResourceLoader.Load<CardRewardPool>(path);
        }

        public ICardData LoadCardData(string path)
        {
            if (!ResourceLoader.Exists(path))
            {
                return null;
            }

            var resource = ResourceLoader.Load<Resource>(path);
            return resource as ICardData;
        }

        public IReadOnlyList<ICardData> LoadCardDataList(IEnumerable<string> paths)
        {
            var result = new List<ICardData>();
            foreach (var path in paths)
            {
                var cardData = LoadCardData(path);
                if (cardData != null)
                {
                    result.Add(cardData);
                }
            }
            return result;
        }
    }
}
