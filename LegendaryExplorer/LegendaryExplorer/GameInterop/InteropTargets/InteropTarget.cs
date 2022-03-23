using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorer.GameInterop.InteropTargets
{
    /// <summary>
    /// Abstract class representing a game that can be used for interop
    /// </summary>
    public abstract class InteropTarget
    {
        public event Action<string> GameReceiveMessage;
        private const string ExecFileName = "lexinterop";
        public abstract MEGame Game { get; }
        public abstract bool CanExecuteConsoleCommands { get; }
        public abstract bool CanUpdateTOC { get; }
        public abstract string InteropASIName { get; }
        /// <summary>
        /// The file name of a deprecated ASI, if any.
        /// </summary>
        public virtual string OldInteropASIName => null;
        public abstract string InteropASIDownloadLink { get; }
        public abstract string InteropASIMD5 { get; }
        /// <summary>
        /// MD5 of Bink Bypass. Only required for OT games
        /// </summary>
        public abstract string BinkBypassMD5 { get; }
        public abstract string OriginalBinkMD5 { get; }
        public abstract InteropModInfo ModInfo { get; }
        public abstract string ProcessName { get; }
        public abstract uint GameMessageSignature { get; }

        public virtual bool TryGetProcess(out Process process)
        {
            process = Process.GetProcessesByName(ProcessName).FirstOrDefault();
            return process != null;
        }

        public void ExecuteConsoleCommands(params string[] commands) =>
            ExecuteConsoleCommands(commands.AsEnumerable());
        public void ExecuteConsoleCommands(IEnumerable<string> commands)
        {
            if (CanExecuteConsoleCommands && TryGetProcess(out Process gameProcess))
            {
                ExecuteConsoleCommands(gameProcess.MainWindowHandle, commands);
            }
        }

        public void ExecuteConsoleCommands(IntPtr hWnd, params string[] commands) => ExecuteConsoleCommands(hWnd, commands.AsEnumerable());
        public void ExecuteConsoleCommands(IntPtr hWnd, IEnumerable<string> commands)
        {
            string execFilePath = Path.Combine(MEDirectories.GetDefaultGamePath(Game), "Binaries", ExecFileName);

            File.WriteAllText(execFilePath, string.Join(Environment.NewLine, commands));
            GameController.DirectExecuteConsoleCommand(hWnd, $"exec {ExecFileName}");
        }

        public bool IsGameInstalled() => MEDirectories.GetExecutablePath(Game) is string exePath && File.Exists(exePath);

        public abstract void SelectGamePath();

        internal void RaiseReceivedMessage(string message)
        {
            GameReceiveMessage?.Invoke(message);
        }
    }
}