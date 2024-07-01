using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using LegendaryExplorer.GameInterop.InteropTargets;
using LegendaryExplorer.Libraries;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Packages;
using Keys = System.Windows.Forms.Keys;

namespace LegendaryExplorer.GameInterop
{
    /// <summary>
    /// Handles communication between LEX and a game executable via the interop ASI
    /// </summary>
    public static class GameController
    {
        public const string TempMapName = "AAAME3EXPDEBUGLOAD";

        private static readonly Dictionary<MEGame, InteropTarget> Targets = new()
        {
            { MEGame.LE1, new LE1InteropTarget() },
            { MEGame.LE2, new LE2InteropTarget() },
            { MEGame.LE3, new LE3InteropTarget() },
            { MEGame.ME2, new ME2InteropTarget() },
            { MEGame.ME3, new ME3InteropTarget() }
        };

        public static InteropTarget GetInteropTargetForGame(MEGame game)
        {
            if (Targets.TryGetValue(game, out InteropTarget target))
            {
                return target;
            }
            return null;
        }

        public static bool IsGameOpen(MEGame game) => TryGetMEProcess(game, out _);

        public static bool TryGetMEProcess(MEGame game, out Process meProcess)
        {
            meProcess = null;
            return GetInteropTargetForGame(game)?.TryGetProcess(out meProcess) ?? false;
        }



        #region For delegates for things like tools to determine which game is running
        /// <summary>
        /// Returns the running game
        /// </summary>
        /// <returns></returns>
        public static MEGame? GetRunningMEGame(MEGame[] games = null)
        {
            foreach (var game in games ?? Enum.GetValues<MEGame>())
            {
                if (IsGameOpen(game))
                {
                    return game;
                }
            }

            return null;
        }

        /// <summary>
        /// Returns the running game as a string
        /// </summary>
        /// <returns></returns>
        public static Func<string> GetRunningMEGameStrDelegate(MEGame[] games = null)
        {
            return () => GetRunningMEGame(games)?.ToString();
        }
        #endregion

        private static bool hasRegisteredForMessages;
        public static void InitializeMessageHook(Window window)
        {
            if (hasRegisteredForMessages) return;
            hasRegisteredForMessages = true;
            if (PresentationSource.FromVisual(window) is HwndSource hwndSource)
            {
                hwndSource.AddHook(WndProc);
            }
        }

        public static bool SendME3TOCUpdateMessage()
        {
            return ((ME3InteropTarget)Targets[MEGame.ME3]).SendTOCUpdateMessage();
        }

        //private
        #region Internal support functions
        [StructLayout(LayoutKind.Sequential)]
        struct COPYDATASTRUCT
        {
            public ulong dwData;
            public uint cbData;
            public IntPtr lpData;
        }
        private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_COPYDATA = 0x004a;
            // const uint SENT_FROM_ME3 = 0x02AC00C2;
            // const uint SENT_FROM_ME2 = 0x02AC00C3;
            // const uint SENT_FROM_ME1 = 0x02AC00C4;
            // const uint SENT_FROM_LE3 = 0x02AC00C5;
            // const uint SENT_FROM_LE2 = 0x02AC00C6;
            // const uint SENT_FROM_LE1 = 0x02AC00C7;
            if (msg == WM_COPYDATA)
            {
                COPYDATASTRUCT cds = Marshal.PtrToStructure<COPYDATASTRUCT>(lParam);
                foreach (var target in Targets.Values)
                {
                    if (cds.dwData == target.GameMessageSignature)
                    {
                        string value = Marshal.PtrToStringUni(cds.lpData);
                        handled = true;
                        target.RaiseReceivedMessage(value);
                        return (IntPtr)1;
                    }
                }
            }
            return IntPtr.Zero;
        }

        const int WM_SYSKEYDOWN = 0x0104;

        private static void SendKey(IntPtr hWnd, Keys key) => SendKey(hWnd, (int)key);
        private static void SendKey(IntPtr hWnd, int key) => WindowsAPI.PostMessage(hWnd, WM_SYSKEYDOWN, key, 0);

        /// <summary>
        /// Executes a console command on the game whose window handle is passed.
        /// <param name="command"/> can ONLY contain [a-z0-9 ] 
        /// </summary>
        /// <param name="gameWindowHandle"></param>
        /// <param name="command"></param>
        internal static void DirectExecuteConsoleCommand(IntPtr gameWindowHandle, string command)
        {
            SendKey(gameWindowHandle, Keys.Tab);
            foreach (char c in command)
            {
                if (characterMapping.TryGetValue(c, out Keys key))
                {
                    SendKey(gameWindowHandle, key);
                }
                else
                {
                    throw new ArgumentException("Invalid characters!", nameof(command));
                }
            }
            SendKey(gameWindowHandle, Keys.Enter);
        }

        internal static bool SendTOCMessage(IntPtr hWnd, uint Msg)
        {
            return WindowsAPI.SendMessage(hWnd, Msg, 0, 0) != 0;
        }

        static readonly Dictionary<char, Keys> characterMapping = new()
        {
            ['a'] = Keys.A,
            ['b'] = Keys.B,
            ['c'] = Keys.C,
            ['d'] = Keys.D,
            ['e'] = Keys.E,
            ['f'] = Keys.F,
            ['g'] = Keys.G,
            ['h'] = Keys.H,
            ['i'] = Keys.I,
            ['j'] = Keys.J,
            ['k'] = Keys.K,
            ['l'] = Keys.L,
            ['m'] = Keys.M,
            ['n'] = Keys.N,
            ['o'] = Keys.O,
            ['p'] = Keys.P,
            ['q'] = Keys.Q,
            ['r'] = Keys.R,
            ['s'] = Keys.S,
            ['t'] = Keys.T,
            ['u'] = Keys.U,
            ['v'] = Keys.V,
            ['w'] = Keys.W,
            ['x'] = Keys.X,
            ['y'] = Keys.Y,
            ['z'] = Keys.Z,
            ['0'] = Keys.D0,
            ['1'] = Keys.D1,
            ['2'] = Keys.D2,
            ['3'] = Keys.D3,
            ['4'] = Keys.D4,
            ['5'] = Keys.D5,
            ['6'] = Keys.D6,
            ['7'] = Keys.D7,
            ['8'] = Keys.D8,
            ['9'] = Keys.D9,
            [' '] = Keys.Space,
        };

        #endregion
    }
}
