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

        /// <summary>
        /// Writes a pre-message and a stacktrace to the log.
        /// </summary>
        /// <param name="exception"></param>
        /// <param name="preMessage"></param>
        /// <param name="fatal"></param>
        /// <param name="customPrefix"></param>
        public static void Exception(Exception exception, string preMessage, bool fatal = false)
        {
            Log.Error($@"{Prefix}{preMessage}");

            // Log exception
            while (exception != null)
            {
                var line1 = exception.GetType().Name + @": " + exception.Message;
                foreach (var line in line1.Split("\n")) // do not localize
                {
                    if (fatal)
                        Log.Fatal(line);
                    else
                        Log.Error(line);

                }

                if (exception.StackTrace != null)
                {
                    foreach (var line in exception.StackTrace.Split("\n")) // do not localize
                    {
                        if (fatal)
                            Log.Fatal(line);
                        else
                            Log.Error(line);
                    }
                }

                exception = exception.InnerException;
            }
        }
    }
}
