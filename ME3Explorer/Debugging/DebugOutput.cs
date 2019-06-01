using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace ME3Explorer.Debugging
{
    /// <summary>
    /// Provides a threadsafe (hopefully) set of debug logging output.
    /// </summary>
    public static class DebugOutput
    {
        static RichTextBox rtb;
        static FileStream fs = null;
        static readonly object _sync = new object();
        static readonly object PrintLock = new object();
        static int count = 0;
        static StringBuilder waiting = new StringBuilder();
        static DateTime LastPrint;

        private static Timer UpdateTimer;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int SendMessage(IntPtr hWnd, int wMsg, IntPtr wParam, IntPtr lParam);
        private const int WM_VSCROLL = 277;
        private const int SB_PAGEBOTTOM = 7;


        /// <summary>
        /// Scrolls to bottom of textbox, regardless of focus.
        /// </summary>
        /// <param name="MyRichTextBox">Textbox to scroll.</param>
        public static void ScrollToBottom(RichTextBox MyRichTextBox)
        {
            SendMessage(MyRichTextBox.Handle, WM_VSCROLL, (IntPtr)SB_PAGEBOTTOM, IntPtr.Zero);
        }


        /// <summary>
        /// Starts debugger if not already started. Prints basic info if required.
        /// </summary>
        /// <param name="toolName">Name of tool where debugging is to be started from.</param>
        public static void StartDebugger(string toolName)
        {
            string appender = "";
            if (DebugOutput.rtb == null)
            {
                UpdateTimer = new Timer();

                // KFreon: Deal with file in use
                for (int i = 0; i < 10; i++)
                {
                    try
                    {
                        fs = new FileStream(Path.GetDirectoryName(Application.ExecutablePath) + "\\DebugOutput" + appender + ".txt", FileMode.Create, FileAccess.Write);
                        break;
                    }
                    catch
                    {
                        var t = i;
                        appender = t.ToString();
                    }
                }

                if (fs == null)
                    MessageBox.Show("Failed to open any debug output files. Disk cached debugging disabled for this session.");

                // KFreon: Thread debugger
                Task.Factory.StartNew(() =>
                {
                    DebugWindow debugger = new DebugWindow();
                    debugger.ShowDialog();
                }, TaskCreationOptions.LongRunning);

                // KFreon: Print basic info
                System.Threading.Thread.Sleep(200);
                DebugOutput.PrintLn("-----New Execution of " + toolName + "-----");
                DebugOutput.PrintLn("Build Version: " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString());
                PrintLn("Using file: DebugOutput" + appender + ".txt");
                DebugOutput.PrintLn("Using debug file: DebugOutput" + appender + ".txt");
            }
            else
                DebugOutput.PrintLn("-----New Execution of " + toolName + "-----");
        }


        /// <summary>
        /// Sets textbox to output to.
        /// </summary>
        /// <param name="box">Textbox to output debug info to.</param>
        public static void SetBox(RichTextBox box)
        {
            try
            {
                LastPrint = DateTime.Now;
                UpdateTimer.Interval = 500;
                UpdateTimer.Tick += UpdateTimer_Tick;
                UpdateTimer.Enabled = true;
                rtb = box;
            }
            catch { }
        }


        /// <summary>
        /// Checks whether the specified textbox can be written to atm.
        /// </summary>
        /// <returns>True if it can be written to.</returns>
        private static bool CheckRTB()
        {
            if (rtb != null && rtb.Parent != null && rtb.Parent.Created == true)
                return true;
            else
                return false;
        }


        static void UpdateTimer_Tick(object sender, EventArgs e)
        {
            bool check = false;
            lock (_sync)
                check = CheckRTB();

            // KFreon: Update textbox if it can be written to
            if (check)
            {
                // KFreon: If currently printing, do nothing
                if (System.Threading.Monitor.TryEnter(PrintLock))
                {
                    UpdateTimer.Stop();
                    string tempwaiting = null;
                    lock (_sync)
                    {
                        tempwaiting = waiting.ToString();
                        waiting.Clear();
                    }


                    if (tempwaiting.Length != 0)
                    {
                        rtb.Invoke(new Action(() =>
                        {
                            rtb.AppendText(tempwaiting);
                            ScrollToBottom(rtb);
                        }));
                    }
                    UpdateTimer.Start();
                    System.Threading.Monitor.Exit(PrintLock);
                }
            }
        }


        /// <summary>
        /// Converts a string to a byte[].
        /// </summary>
        /// <param name="s">String to convert.</param>
        /// <returns>byte[] of string data.</returns>
        public static byte[] Str2Arr(string s)
        {
            byte[] res = new byte[s.Length];
            for (int i = 0; i < s.Length; i++)
                res[i] = (byte)s[i];
            return res;
        }


        /// <summary>
        /// Prints a blank line to the textbox.
        /// </summary>
        /// <param name="update">OPTIONAL: Updates textbox immediately if true. Defaults to true.</param>
        public static void PrintLn(bool update = true)
        {
            PrintLn("", update);
        }


        /// <summary>
        /// Prints text on a new line.
        /// </summary>
        /// <param name="s">String to write to textbox.</param>
        /// <param name="update">OPTIONAL: Updates textbox immediately if true. Defaults to true.</param>
        public static void PrintLn(string s, bool update = true)
        {
            lock (_sync)
            {
                // KFreon: Writes to textbox if available.
                if (CheckRTB())
                {
                    try
                    {
                        string res = DateTime.Now.ToLongTimeString() + ":  " + s;
                        waiting.AppendLine(res);
                        res = res.Replace("\n", System.Environment.NewLine);

                        try
                        {
                            if (fs != null)
                                fs.Write(Str2Arr(res), 0, res.Length);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("Unable to append line to file. Reason: " + e.Message);
                        }

                    }
                    catch { }
                }
            }
        }


        /// <summary>
        /// Prints text to textbox.
        /// </summary>
        /// <param name="s">Text to print to textbox.</param>
        /// <param name="update">OPTIONAL: Updates textbox now if true. Defaults to true.</param>
        public static void Print(string s, bool update = true)
        {
            lock (_sync)
            {
                // KFreon: Write to textbox if available.
                if (CheckRTB())
                {
                    try
                    {
                        string res = DateTime.Now.ToLongTimeString() + ":  " + s + '\n';
                        waiting.Append(res);
                        res = res.Replace("\n", System.Environment.NewLine);

                        try
                        {
                            if (fs != null)
                                fs.Write(Str2Arr(res), 0, res.Length);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("Unable to append line to file. Reason: " + e.Message);
                        }

                    }
                    catch { }
                }
            }
        }


        /// <summary>
        /// Clears textbox.
        /// </summary>
        /// <param name="update">OPTIONAL: Updates textbox now if true.</param>
        public static void Clear(bool update = true)
        {
            lock (_sync)
            {
                if (CheckRTB())
                {
                    try
                    {
                        rtb.Invoke(new Action(() => rtb.Clear()));
                    }
                    catch { }
                }
            }
        }

        public static void NullifyRTB()
        {
            lock (PrintLock)
            {
                rtb = null;
                fs.Close();
            }
        }
    }
}
