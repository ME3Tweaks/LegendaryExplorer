using AmaroK86.ImageFormat;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Gibbed.IO;
using System.Drawing.Drawing2D;
using SaltTPF;
using System.Diagnostics;
using KFreonLib.Textures;
using KFreonLib.PCCObjects;
using KFreonLib.MEDirectories;
using KFreonLib.Debugging;

namespace KFreonLib.Misc
{
    /// <summary>
    /// Provides miscellaneous methods for general tools. 
    /// </summary>
    public static class Methods
    {
        public static List<bool> CheckGameState(MEDirectories.MEDirectories MEExDirecs, bool askIfNotFound, out List<string> messages)
        {
            DebugOutput.PrintLn("In CheckGameState, checkin ur gamez.");

            List<bool> results = new List<bool>() { false, false, false };
            messages = MEExDirecs.SetupPathing(askIfNotFound);
            for (int i = 0; i < 3; i++)
            {
                string message = messages[i];
                DebugOutput.PrintLn("Message from MEExDirecs: " + message);
                DebugOutput.PrintLn("Setting indicators...");

                // KFreon: Set visual cues
                if (!message.Contains("game files not found"))
                {
                    //if (Directory.Exists(MEExDirecs.GetDifferentPathBIOGame(i + 1)))
                    string temp = MEExDirecs.GetDifferentPathBIOGame(i + 1).TrimEnd(Path.DirectorySeparatorChar);
                    string tocPath = null;
                    switch(i)
                    {
                        case 0:
                            temp = Path.GetDirectoryName(temp);
                            tocPath = Path.Combine(temp, "Binaries\\MassEffect.exe");
                            break;
                        case 1:
                            temp = Path.GetDirectoryName(temp);
                            tocPath = Path.Combine(temp, "Binaries\\MassEffect2.exe");
                            break;
                        case 2:
                            tocPath = Path.Combine(temp, "PCConsoleTOC.bin");
                            break;
                    }
                    DebugOutput.PrintLn("Path of file to check if game files present: " + tocPath);

                    if (File.Exists(tocPath))
                        results[i] = true;

                    DebugOutput.PrintLn("BIOGame Path = " + MEExDirecs.GetDifferentPathBIOGame(i + 1) + "  found using: " + (message.Contains("Found installation") ? "ME" + i + "Directory Class." : "Texplorer Settings."));
                }
            }
            return results;
        }


        public static List<string> GetInstalledDLC(string DLCBasePath, bool SortByPriority = false)
        {
            if (Directory.Exists(DLCBasePath))
            {
                List<string> result = Directory.EnumerateDirectories(DLCBasePath).ToList();
                if (SortByPriority)
                {
                    List<Tuple<int, string>> sorted = new List<Tuple<int, string>>();
                    foreach (string s in result)
                    {
                        string foldername = Path.GetFileName(s);
                        if (!foldername.StartsWith("DLC_"))
                        {
                            continue;
                        }
                        int prio = GetDLCPriority(s);
                        if (sorted.Count == 0 || prio > sorted[0].Item1)
                            sorted.Insert(0, new Tuple<int, string>(prio, s));
                        bool added = false;
                        for (int i = 0; i < sorted.Count; i++)
                        {
                            if (prio < sorted[i].Item1 && (i == sorted.Count - 1 || prio > sorted[i + 1].Item1))
                            {
                                sorted.Insert(i + 1, new Tuple<int, string>(prio, s));
                                added = true;
                                break;
                            }
                        }
                        if (!added)
                            sorted.Add(new Tuple<int, string>(prio, s));
                    }
                    result.Clear();
                    foreach (Tuple<int, string> item in sorted)
                    {
                        result.Add(item.Item2);
                    }
                    return result; 
                }
                else
                {
                    return result;
                }
            }
            return new List<string>();
        }

        public static int GetDLCPriority(string DLCBasePath)
        {
            try
            {
                using (System.IO.FileStream s = new FileStream(DLCBasePath + "\\CookedPCConsole\\Mount.dlc", FileMode.Open))
                {
                    byte[] pdata = new byte[2];
                    s.Seek(16, SeekOrigin.Begin);
                    s.Read(pdata, 0, 2);
                    // swap bytes
                    byte b = pdata[0];
                    pdata[0] = pdata[1];
                    pdata[1] = b;
                    return BitConverter.ToInt16(pdata, 0);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return -1;
            }
        }

        /// <summary>
        /// Enumerates the given game folder using a filter.
        /// </summary>
        /// <param name="GameVersion">Version of game being searched.</param>
        /// <param name="searchPath">Path to search.</param>
        /// <param name="filter">OPTIONAL: Filter to use. Null uses defaults.</param>
        /// <returns></returns>
        public static List<string> EnumerateGameFiles(int GameVersion, string searchPath, bool recurse = true, Predicate<string> predicate = null)
        {
            List<string> files = new List<string>();

            files = Directory.EnumerateFiles(searchPath, "*", recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly).ToList();
            files = EnumerateGameFiles(GameVersion, files, predicate);
            return files;
        }

        public static List<string> EnumerateGameFiles(int GameVersion, List<string> files, Predicate<string> predicate = null)
        {
            if (predicate == null)
            {
                switch (GameVersion)
                {
                    case 1:
                        predicate = s => s.ToLowerInvariant().EndsWith(".upk", true, null) || s.ToLowerInvariant().EndsWith(".u", true, null) || s.ToLowerInvariant().EndsWith(".sfm", true, null);
                        break;
                    case 2:
                        predicate = s => s.ToLowerInvariant().EndsWith(".pcc", true, null);
                        break;
                    case 3:
                        predicate = s => s.ToLowerInvariant().EndsWith(".pcc", true, null) || s.ToLowerInvariant().EndsWith(".tfc", true, null);
                        break;
                }
            }


            return files.Where(t => predicate(t)).ToList();
        }


        /// <summary>
        /// Sets the BIOGame path in the MEDirectory classes.
        /// </summary>
        /// <param name="GameVers">Which game to set path of.</param>
        /// <param name="path">New BIOGame path.</param>
        public static void SetGamePath(int GameVers, string path)
        {
            switch (GameVers)
            {
                case 1:
                    ME1Directory.gamePath = path;
                    break;
                case 2:
                    ME2Directory.gamePath = path;
                    break;
                case 3:
                    ME3Directory.gamePath = path;
                    break;
            }
        }


        /// <summary>
        /// Gets user input to select a game exe.
        /// </summary>
        /// <param name="GameVers">Game to select.</param>
        /// <returns>Path to game exe.</returns>
        public static string SelectGameLoc(int GameVers)
        {
            string retval = null;
            string gameExe = "MassEffect" + (GameVers != 1 ? GameVers.ToString() : "") + ".exe";
            OpenFileDialog selectDir = new OpenFileDialog();
            selectDir.FileName = gameExe;
            selectDir.Filter = "ME" + GameVers + " exe file|" + gameExe;
            selectDir.Title = "Select the Mass Effect " + GameVers + " executable file";
            if (selectDir.ShowDialog() == DialogResult.OK)
                retval = Path.GetDirectoryName(Path.GetDirectoryName(selectDir.FileName)) + @"\";
			if (GameVers == 3)
                retval = Path.GetDirectoryName(Path.GetDirectoryName(retval));
            return retval;
        }


        /// <summary>
        /// Returns path of executing program.
        /// </summary>
        /// <returns>Path of executing program.</returns>
        public static string GetExecutingLoc()
        {
            return Path.GetDirectoryName(Application.ExecutablePath);
        }


        /// <summary>
        /// Runs commands in a shell. (WV's code I believe)
        /// </summary>
        /// <param name="cmd">Commands to run.</param>
        /// <param name="args">Arguments to give to commands.</param>
        public static void RunShell(string cmd, string args)
        {
            System.Diagnostics.ProcessStartInfo procStartInfo = new System.Diagnostics.ProcessStartInfo(cmd, args);
            procStartInfo.WorkingDirectory = Path.GetDirectoryName(cmd);
            procStartInfo.RedirectStandardOutput = true;
            procStartInfo.UseShellExecute = false;
            procStartInfo.CreateNoWindow = true;
            System.Diagnostics.Process proc = new System.Diagnostics.Process();
            proc.StartInfo = procStartInfo;
            proc.Start();
            proc.WaitForExit();
        }


        /// <summary>
        /// Displays a Yes/No dialog box with customizable title and prompt. Returns true or false.
        /// </summary>
        /// <param name="message">Message prompt to display.</param>
        /// <param name="title">Window title to use.</param>
        /// <returns>True if yes, False if no.</returns>
        public static bool DisplayYesNoDialogBox(string message, string title)
        {
            DialogResult dr = MessageBox.Show(message, title, MessageBoxButtons.YesNo);
            if (dr == DialogResult.Yes)
                return true;
            else
                return false;
        }


        /// <summary>
        /// Returns which game has called this function. For use to figure out calling functions
        /// </summary>
        /// <returns>Number of which game has been called.</returns>
        public static int GetMEType()
        {
            StackTrace st = new StackTrace();

            for (int i = 0; i < st.FrameCount; i++)
            {
                string name = st.GetFrame(i).GetMethod().ReflectedType.FullName;
                if (name.Contains("ME1"))
                    return 1;
                else if (name.Contains("ME2"))
                    return 2;
                else if (name.Contains("ME3"))
                    return 3;
            }
            return -1;
        }


        /// <summary>
        /// Find a string in the stackframes. I use it to find calling functions.
        /// </summary>
        /// <param name="FrameName">FrameName to find.</param>
        /// <returns>True if FrameName is found in stack.</returns>
        public static bool FindInStack(string FrameName)
        {
            StackTrace st = new StackTrace();
            for (int i = 0; i < st.FrameCount; i++)
            {
                string name = st.GetFrame(i).GetMethod().ReflectedType.FullName;
                if (name.Contains(FrameName))
                    return true;
            }
            return false;
        }


        /// <summary>
        /// Gets number of processor cores without hyperthreading.
        /// </summary>
        /// <returns>Number of physical cores.</returns>
        public static int GetNumCores()
        {
            int coreCount = 0;
            foreach (var item in new System.Management.ManagementObjectSearcher("Select * from Win32_Processor").Get())
                coreCount += int.Parse(item["NumberOfCores"].ToString());

            return coreCount;
        }


        /// <summary>
        /// Sets number of threads in a program.
        /// </summary>
        /// <param name="User">True if dialog required.</param>
        /// <returns>Number of threads to use.</returns>
        public static int SetNumThreads(bool User)
        {
            int threads = 0;
            if (User)
                // KFreon: Get user input
                while (true)
                    if (int.TryParse(Microsoft.VisualBasic.Interaction.InputBox("Set number of threads to use in multi-threaded programs: ", "Threads", "4"), out threads))
                        break;
                    else
                        threads = GetNumCores();

            // KFreon: Checks - Capped at 8 for now
            if (threads > 0 && threads <= 8)
                return threads;
            else
                return 4;
        }


        /// <summary>
        /// Returns name of DLC if path is part of a DLC.
        /// </summary>
        /// <param name="path">Path to search for DLC names in.</param>
        /// <returns>Name of DLC or empty string if not found.</returns>
        public static string GetDLCNameFromPath(string path)
        {
            if (path.Contains("DLC_"))
            {
                List<string> parts = path.Split('\\').ToList();
                string retval = new List<string>(parts.Where(part => part.Contains("DLC_")))[0];
                if (retval.Contains("metadata"))
                    return null;
                else
                    return retval;
            }
            else 
                return null;
        }
    }
}
