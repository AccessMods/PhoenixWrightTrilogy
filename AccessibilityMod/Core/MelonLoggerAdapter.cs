using MelonLoader;
using UnityAccessibilityLib;

namespace AccessibilityMod.Core
{
    /// <summary>
    /// Adapter to use MelonLoader's logger with MelonAccessibilityLib.
    /// </summary>
    public class MelonLoggerAdapter : IAccessibilityLogger
    {
        private readonly MelonLogger.Instance _logger;

        public MelonLoggerAdapter(MelonLogger.Instance logger)
        {
            _logger = logger;
        }

        public void Msg(string message) => _logger.Msg(message);

        public void Warning(string message) => _logger.Warning(message);

        public void Error(string message) => _logger.Error(message);
    }
}
