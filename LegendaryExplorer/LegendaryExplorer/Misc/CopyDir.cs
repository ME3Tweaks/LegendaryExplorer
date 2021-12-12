/**
 * This class was ported from ALOT Installer
 */

using System;
using System.ComponentModel;
using System.IO;

namespace LegendaryExplorer.Misc
{
    public class CopyDir
    {
        public const string UPDATE_PROGRESSBAR_INDETERMINATE = "UPDATE_PROGRESSBAR_INDETERMINATE";
        public const string UPDATE_CURRENT_FILE_TEXT = "UPDATE_CURRENT_FILE_TEXT";
        public const string UPDATE_PROGRESSBAR_VALUE = "UPDATE_PROGRESSBAR_VALUE";
        public const string UPDATE_PROGRESSBAR_MAXVALUE = "UPDATE_PROGRESSBAR_MAXVALUE";

        //Commented out to prevent usage. May need to use later.
        /*public static void Copy(string sourceDirectory, string targetDirectory)
        {
            DirectoryInfo diSource = new DirectoryInfo(sourceDirectory);
            DirectoryInfo diTarget = new DirectoryInfo(targetDirectory);

            CopyAll(diSource, diTarget);
        }

        //Commented out 
        /*public static void CopyAll(DirectoryInfo source, DirectoryInfo target, BackgroundWorker worker = null)
        {
            Directory.CreateDirectory(target.FullName);

            // Copy each file into the new directory.
            foreach (FileInfo fi in source.GetFiles())
            {
                if (fi.FullName.EndsWith(".txt"))
                {
                    continue; //don't copy logs
                }
                //Log.Information(@"Copying {0}\{1}", target.FullName, fi.Name);
                try
                {
                    fi.CopyTo(Path.Combine(target.FullName, fi.Name), true);
                }
                catch (Exception e)
                {
                    Log.Error("Error copying file: " + fi + " -> " + Path.Combine(target.FullName, fi.Name));
                    throw e;
                }
            }

            // Copy each subdirectory using recursion.
            foreach (DirectoryInfo diSourceSubDir in source.GetDirectories())
            {
                DirectoryInfo nextTargetSubDir =
                    target.CreateSubdirectory(diSourceSubDir.Name);
                CopyAll(diSourceSubDir, nextTargetSubDir);
            }
        }*/

        public static int CopyAll_ProgressBar(DirectoryInfo source, DirectoryInfo target, BackgroundWorker worker, int total = -1, int done = 0, string[] ignoredExtensions = null)
        {
            if (total < 0)
            {
                //calculate number of files
                total = Directory.GetFiles(source.FullName, "*.*", SearchOption.AllDirectories).Length;
            }
            worker.ReportProgress(0, new ThreadCommand(UPDATE_PROGRESSBAR_INDETERMINATE, false));

            int numdone = done;
            Directory.CreateDirectory(target.FullName);

            // Copy each file into the new directory.
            var files = source.GetFiles();
            worker.ReportProgress(0, new ThreadCommand(UPDATE_PROGRESSBAR_MAXVALUE, files.Length));
            worker.ReportProgress(0, new ThreadCommand(UPDATE_PROGRESSBAR_VALUE, 0));
            foreach (FileInfo fi in files)
            {
                if (ignoredExtensions != null)
                {
                    bool skip = false;
                    foreach (string str in ignoredExtensions)
                    {
                        if (fi.Name.ToLower().EndsWith(str))
                        {
                            skip = true;
                            break;
                        }
                    }
                    if (skip)
                    {
                        numdone++;
                        worker.ReportProgress(0, new ThreadCommand(UPDATE_PROGRESSBAR_VALUE, numdone));
                        continue;
                    }
                }
                string displayName = fi.Name;
                string path = Path.Combine(target.FullName, fi.Name);
                worker.ReportProgress(done, new ThreadCommand(UPDATE_CURRENT_FILE_TEXT, displayName));
                fi.CopyTo(Path.Combine(target.FullName, fi.Name), true);
                // Log.Information(@"Copying {0}\{1}", target.FullName, fi.Name);
                numdone++;
                worker.ReportProgress(0, new ThreadCommand(UPDATE_PROGRESSBAR_VALUE, numdone));
            }

            // Copy each subdirectory using recursion.
            foreach (DirectoryInfo diSourceSubDir in source.GetDirectories())
            {
                DirectoryInfo nextTargetSubDir =
                    target.CreateSubdirectory(diSourceSubDir.Name);
                numdone = CopyAll_ProgressBar(diSourceSubDir, nextTargetSubDir, worker, total, numdone, ignoredExtensions);
            }
            return numdone;
        }
    }

    /// <summary>
    /// Class for passing data between threads when using ReportProgress()
    /// </summary>
    public class ThreadCommand
    {
        /// <summary>
        /// Creates a new thread command object with the specified command and data object. This constructor is used for passing data to another thread. The receiver will need to read the command then cast the data.
        /// </summary>
        /// <param name="command">command for this thread communication.</param>
        /// <param name="data">data to pass to another thread</param>
        public ThreadCommand(string command, object data)
        {
            this.Command = command;
            this.Data = data;
        }

        /// <summary>
        /// Creates a new thread command object with the specified command. This constructor is used for notifying other threads something has happened.
        /// </summary>
        /// <param name="command">command for this thread communication.</param>
        /// <param name="data">data to pass to another thread</param>
        public ThreadCommand(string command)
        {
            this.Command = command;
        }

        public string Command;
        public object Data;
    }
}