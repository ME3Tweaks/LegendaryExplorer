using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Gibbed.IO;
using AmaroK86.MassEffect3;
using System.Windows.Forms;
using KFreonLib.Debugging;

namespace ME3Explorer.Tools
{
    class DLCHelper
    {
        private static String listpath;
        public static Boolean repackEnabled = false;
        private static int ModNum = 0;
        private static object ModLock = new object();

        public static void SetDLCCachePath(string path)
        {
            Properties.Settings.Default.TexplorerME3Path = path;
            Properties.Settings.Default.Save();
        }

        public static String GetDLCCachePath()
        {
            if (Properties.Settings.Default.TexplorerME3Path == null)
                throw new NullReferenceException("DLC Cache Path not set");
            return Properties.Settings.Default.TexplorerME3Path;
        }

        public static void AddFileForRepacking(string dlcname, string filename)
        {
            if (!Directory.Exists(GetDLCCachePath()))
                throw new FileNotFoundException("DLC Cache Path doesn't exist");

            if (listpath == null)
                listpath = Path.Combine(GetDLCCachePath(), "RepackList");
            using (FileStream fs = new FileStream(listpath, FileMode.Append, FileAccess.Write))
            {
                using (StreamWriter writer = new StreamWriter(fs))
                {
                    writer.WriteLine(Path.Combine(dlcname, filename));
                }
            }
            repackEnabled = true;
        }

        public static String[] GetFilesForRepacking()
        {
            if (!Directory.Exists(GetDLCCachePath()))
                throw new FileNotFoundException("DLC Cache Path not found");
            if (listpath == null)
                listpath = Path.Combine(GetDLCCachePath(), "RepackList");

            List<String> files = new List<string>();
            if (!File.Exists(listpath))
                return new String[0];

            using (FileStream fs = new FileStream(listpath, FileMode.Open, FileAccess.Read))
            {
                using (StreamReader reader = new StreamReader(fs))
                {
                    String templine = reader.ReadLine();
                    while (templine != null)
                    {
                        files.Add(templine);
                        templine = reader.ReadLine();
                    }
                }
            }
            return files.ToArray();
        }

        public static void RepackDLCs(bool MakeBackup = true)
        {
            if (!repackEnabled)
                return;
            String[] dlcupdates = GetFilesForRepacking();
            if (!Directory.Exists(GetDLCCachePath()))
                throw new FileNotFoundException("DLC Cache Path not found");
            if (dlcupdates.Length == 0)
            {
                DebugOutput.PrintLn("No updates found!");
                return;
            }

            List<String> tempdlcupdates = new List<string>();
            for (int i = 0; i < dlcupdates.Length; i++)
            {
                bool filefound = false;
                for (int j = 0; j < tempdlcupdates.Count; j++)
                {
                    if (String.Compare(dlcupdates[i], tempdlcupdates[j]) == 0)
                    {
                        filefound = true;
                        break;
                    }
                }
                if (!filefound)
                    tempdlcupdates.Add(dlcupdates[i]);
            }
            dlcupdates = tempdlcupdates.ToArray();

            DebugOutput.PrintLn("Repacking modified DLCs...");
            String DLCPath = Path.GetDirectoryName(listpath);
            string[] DLCs = Directory.EnumerateDirectories(DLCPath, "*", SearchOption.TopDirectoryOnly).ToArray();

            for (int i = 0; i < DLCs.Length; i++)
            {
                DirectoryInfo dinfo = new DirectoryInfo(DLCs[i]);
                FileInfo[] files = dinfo.GetFiles();
                List<String> updates = new List<string>();
                string commonname = Path.GetFileName(DLCs[i]);
                for (int j = 0; j < dlcupdates.Length; j++)
                {
                    if (Path.GetFileName(Path.GetDirectoryName(dlcupdates[j])) != commonname)
                        continue;

                    for (int k = 0; k < files.Length; k++)
                    {
                        if (String.Compare(files[k].Name, Path.GetFileName(dlcupdates[j]), true) == 0)
                        {
                            updates.Add(dlcupdates[j]);
                            break;
                        }
                    }
                }

                if (updates.Count <= 0)
                    continue;

                DebugOutput.PrintLn("DLC Updates found for " + commonname + ". Now fixing PCConsoleTOC.bin and repacking...");

                DLCBase dlcbase;
                try
                {
                    string[] tempdlc = Directory.GetFiles(Path.Combine(Path.GetDirectoryName(DLCPath), "DLC", commonname), "Default.sfar", SearchOption.AllDirectories);
                    if (tempdlc == null || tempdlc.Length == 0)
                        throw new FileNotFoundException("DLC File not found!");

                    // KFreon: Allow no backup file creation
                    if (MakeBackup)
                    {
                        if (!File.Exists(Path.ChangeExtension(tempdlc[0], ".bak")))
                        {
                            DebugOutput.PrintLn("DLC backup file not found. Creating...");
                            File.Copy(tempdlc[0], Path.ChangeExtension(tempdlc[0], ".bak"), true);
                        }
                    }
                    dlcbase = new DLCBase(tempdlc[0]);
                }
                catch (FileNotFoundException)
                {
                    DebugOutput.PrintLn("DLC File Not Found");
                    continue;
                }
                catch 
                { 
                    DebugOutput.PrintLn("DLC Opening failed");
                    continue;
                }

                DLCEditor editor = new DLCEditor(dlcbase);

                String tocpath = null;
                for (int j = 0; j < files.Length; j++)
                {
                    if (String.Compare(Path.GetFileName(files[j].Name), "PCConsoleTOC.bin", true) == 0)
                    {
                        DLCTocFix(files[j].FullName);
                        tocpath = files[j].FullName;
                        break;
                    }
                }

                for (int j = 0; j < updates.Count; j++)
                {
                    String tempname = dlcbase.getFullNameOfEntry(updates[j]);
                    if (tempname == null)
                        throw new FileNotFoundException("Filename not found in DLC's files");
                    editor.setReplaceFile(tempname, updates[j]);
                }

                // Also add toc.bin
                String temptoc = dlcbase.getFullNameOfEntry(tocpath);
                if (temptoc == null)
                    throw new FileNotFoundException("TOC not found in DLC's files");
                editor.setReplaceFile(temptoc, tocpath);

                editor.Execute(Path.ChangeExtension(dlcbase.fileName, ".new"), null, Properties.Settings.Default.NumThreads);
                if (File.Exists(Path.ChangeExtension(dlcbase.fileName, ".new")))
                {
                    File.Copy(Path.ChangeExtension(dlcbase.fileName, ".new"), dlcbase.fileName, true);
                    File.Delete(Path.ChangeExtension(dlcbase.fileName, ".new"));
                }
                DebugOutput.PrintLn("Finished updating " + commonname, true);
            }
            File.Delete(listpath); // Reset the repack file
        }

        public static void DLCTocFix(String path)
        {
            if (String.Compare(Path.GetFileName(path), "PCConsoleTOC.bin", true) != 0)
                throw new FileFormatException("Incorrect file passed!");
            else if (!File.Exists(path))
                throw new FileNotFoundException("PCConsoleTOC.bin file not found!");

            TOCeditor tc = new TOCeditor();
            FileInfo[] files = new DirectoryInfo(Path.GetDirectoryName(path)).GetFiles();
            for (int i = 0; i < files.Length; i++)
            {
                if (String.Compare(".bin", files[i].Extension, true) == 0)
                    continue;

                tc.UpdateFile("\\" + Path.GetFileName(files[i].FullName), (uint)files[i].Length, path);
            }
        }
    }
}
