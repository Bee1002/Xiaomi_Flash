using System.Threading;
using Xiaomi_Flash.Ui;

namespace Xiaomi_Flash
{
    /// <summary>
    /// Un solo fastboot.exe a la vez. Las operaciones críticas bloquean el poll de dispositivos.
    /// </summary>
    internal static class FastbootGate
    {
        internal static readonly object Sync = new object();
        static int criticalDepth;

        public static bool BlocksBackgroundPoll =>
            FastbootAutoProbe.IsProbing || criticalDepth > 0;

        public static void EnterCritical()
        {
            Interlocked.Increment(ref criticalDepth);
        }

        public static void ExitCritical()
        {
            if (criticalDepth > 0)
                Interlocked.Decrement(ref criticalDepth);
        }
    }
}
