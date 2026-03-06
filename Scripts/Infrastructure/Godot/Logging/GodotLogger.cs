using Godot;
using OdysseyCards.Application.Ports;

namespace OdysseyCards.Infrastructure.Godot.Logging
{
    public sealed class GodotLogger : ILogger
    {
        public void Log(string message)
        {
            GD.Print(message);
        }

        public void LogWarning(string message)
        {
            GD.PushWarning(message);
        }

        public void LogError(string message)
        {
            GD.PushError(message);
        }
    }
}
