using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using ME3Explorer.Packages;
using Keys = System.Windows.Forms.Keys;

namespace ME3Explorer.GameInterop
{
    public static class GameController
    {
        public const string TempMapName = "AAAME3EXPDEBUGLOAD";
        public static bool TryGetME3Process(out Process me3Process)
        {
            me3Process = Process.GetProcessesByName("MassEffect3").FirstOrDefault();
            return me3Process != null;
        }

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

        public static event Action<string> RecieveME3Message; 
        public static void SendKey(IntPtr hWnd, Keys key) => SendKey(hWnd, (int)key);

        public static void ExecuteConsoleCommands(IntPtr hWnd, MEGame game, params string[] commands) => ExecuteConsoleCommands(hWnd, game, commands.AsEnumerable());

        public static void ExecuteConsoleCommands(IntPtr hWnd, MEGame game, IEnumerable<string> commands)
        {
            const string execFileName = "me3expinterop";
            string execFilePath = Path.Combine(MEDirectories.GamePath(game), "Binaries", execFileName);
            File.WriteAllText(execFilePath, string.Join(Environment.NewLine, commands));
            DirectExecuteConsoleCommand(hWnd, $"exec {execFileName}");
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
            const uint SENT_FROM_ME3 = 0x02AC00C2;
            if (msg == WM_COPYDATA)
            {
                COPYDATASTRUCT cds = Marshal.PtrToStructure<COPYDATASTRUCT>(lParam);
                if (cds.dwData == SENT_FROM_ME3)
                {
                    string value = Marshal.PtrToStringUni(cds.lpData);
                    RecieveME3Message?.Invoke(value);
                    handled = true;
                    return (IntPtr)1;
                }
            }

            return IntPtr.Zero;
        }

        [DllImport("user32.dll")]
        static extern bool PostMessage(IntPtr hWnd, uint Msg, int wParam, int lParam);
        const int WM_SYSKEYDOWN = 0x0104;

        private static void SendKey(IntPtr hWnd, int key) => PostMessage(hWnd, WM_SYSKEYDOWN, key, 0);

        /// <summary>
        /// Executes a console command on the game whose window handle is passed.
        /// <param name="command"/> can ONLY contain [a-z0-9 ] 
        /// </summary>
        /// <param name="gameWindowHandle"></param>
        /// <param name="command"></param>
        private static void DirectExecuteConsoleCommand(IntPtr gameWindowHandle, string command)
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

        static readonly Dictionary<char, Keys> characterMapping = new Dictionary<char, Keys>
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
