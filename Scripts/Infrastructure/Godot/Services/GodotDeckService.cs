using Godot;
using OdysseyCards.Application.Ports;
using OdysseyCards.Core;

namespace OdysseyCards.Infrastructure.Godot.Services
{
    public sealed class GodotDeckService : IDeckService
    {
        public bool AddCardToDeck(object cardResource)
        {
            if (cardResource == null)
            {
                return false;
            }

            if (GameManager.Instance == null)
            {
                return false;
            }

            var resource = cardResource as Resource;
            if (resource == null)
            {
                return false;
            }

            return GameManager.Instance.AddCardToDeck(resource);
        }
    }
}
