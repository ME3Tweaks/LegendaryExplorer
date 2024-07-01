using System;
using System.Diagnostics;

namespace LegendaryExplorerCore.DebugTools
{
    /// <summary>
    /// Constructing this starts a stopwatch, and disposing stops it and writes the elapsed milliseconds to the debug log.
    /// </summary>
    public class DebugStopWatch : IDisposable
    {
        private readonly string _logMessage;
        private readonly bool _logInReleaseMode;
        public readonly Stopwatch Stopwatch;
        public DebugStopWatch(string logMessage, bool logInReleaseMode = false)
        {
            _logMessage = logMessage;
            _logInReleaseMode = logInReleaseMode;
            Stopwatch = new Stopwatch();
            Stopwatch.Start();
        }

        public void Dispose()
        {
            Stopwatch.Stop();
            if (_logInReleaseMode)
            {
                Debugger.Log(1, "", $"{_logMessage} {Stopwatch.ElapsedMilliseconds}ms\n");
            }
            else
            {
                Debug.WriteLine($"{_logMessage} {Stopwatch.ElapsedMilliseconds}ms");
            }
        }
    }
}
