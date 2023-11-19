using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Serilog;
using Serilog.Core;

namespace LegendaryExplorerCore.DebugTools
{
    public static class LECLog
    {
        internal static ILogger logger;

        /// <summary>
        /// If debug messages should be logged. Defaults to false.
        /// </summary>
        public static bool LogDebug;
        private const string Prefix = @"[LEC] ";

        public static void Information(string message)
        {
            logger?.Information($"{Prefix}{message}");
        }
        public static void Warning(string message)
        {
            logger?.Warning($"{Prefix}{message}");
        }
        public static void Error(string message)
        {
            logger?.Error($"{Prefix}{message}");
        }

        public static void Debug(string message, bool shouldLog = true)
        {
            if (shouldLog && LogDebug)
            {
                logger?.Debug($"{Prefix}{message}");
            }
        }
    }
}
