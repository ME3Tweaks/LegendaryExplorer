using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using LegendaryExplorerCore.Packages;
using Microsoft.WindowsAPICodePack.Dialogs;
using SlavaGu.ConsoleAppLauncher;

namespace LegendaryExplorer.Misc
{
    public static class UModelHelper
    {
        public const int SupportedUModelBuildNum = 1555;
        public static int GetLocalUModelVersion()
        {
            int version = 0;
            var umodel = Path.Combine(AppDirectories.StaticExecutablesDirectory, @"umodel", @"umodel.exe");
            if (File.Exists(umodel))
            {
                try
                {
                    var umodelProc = new ConsoleApp(umodel, @"-version");
                    umodelProc.ConsoleOutput += (o, args2) =>
                    {
                        if (version != 0)
                            return; // don't care

                        string str = args2.Line;
                        if (str != null)
                        {
                            if (str.StartsWith("Compiled "))
                            {
                                var buildNum = str.Substring(str.LastIndexOf(" ", StringComparison.InvariantCultureIgnoreCase) + 1);
                                buildNum = buildNum.Substring(0, buildNum.IndexOf(")", StringComparison.InvariantCultureIgnoreCase)); // This is just in case build num changes drastically for some reason

                                if (int.TryParse(buildNum, out var parsedBuildNum))
                                {
                                    version = parsedBuildNum;
                                }
                            }

                        }
                    };
                    umodelProc.Run();
                    umodelProc.WaitForExit();
                }
                catch
                {
                    // ?
                    // This can happen if image has issues like incomplete exe 
                }

            }

            return version; // not found
        }

        /// <summary>
        /// Exports a mesh via UModel
        /// </summary>
        /// <param name="window">Exporting window, needed to open file dialogs</param>
        /// <param name="export">Mesh to export</param>
        public static void ExportViaUModel(Window window, ExportEntry export)
        {
            var dlg = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                EnsurePathExists = true,
                Title = "Select output directory"
            };
            if (dlg.ShowDialog(window) == CommonFileDialogResult.Ok)
            {
                var bw = new BackgroundWorker();
                bw.DoWork += (_, _) =>
                {
                    string umodel = Path.Combine(AppDirectories.StaticExecutablesDirectory, "umodel", "umodel.exe");
                    var args = new List<string>
                    {
                        "-export",
                        $"-out=\"{dlg.FileName}\"",
                        $"\"{export.FileRef.FilePath}\"",
                        export.ObjectNameString,
                        export.ClassName
                    };

                    var arguments = string.Join(" ", args);
                    Debug.WriteLine("Running process: " + umodel + " " + arguments);
                    //Log.Information("Running process: " + exe + " " + args);


                    var umodelProcess = new ConsoleApp(umodel, arguments);
                    umodelProcess.ConsoleOutput += (_, args2) => { Debug.WriteLine(args2.Line); };
                    umodelProcess.Run();
                    while (umodelProcess.State == AppState.Running)
                    {
                        Thread.Sleep(100); //this is kind of hacky but it works
                    }

                    Process.Start("explorer", dlg.FileName);
                };
                bw.RunWorkerAsync();
            }
        }

        /// <summary>
        /// Downloads and caches UModel. Returns error message string if error, null if OK
        /// </summary>
        public static string EnsureUModel(Action setDownloading, Action<int> setMaxProgress, Action<int> setCurrentProgress, Action<string> setText)
        {
            if (UModelHelper.GetLocalUModelVersion() < UModelHelper.SupportedUModelBuildNum)
            {
                void progressCallback(long bytesDownloaded, long bytesToDownload)
                {
                    setMaxProgress?.Invoke((int)bytesToDownload);
                    setCurrentProgress?.Invoke((int)bytesDownloaded);
                }

                setDownloading?.Invoke();
                setText?.Invoke("Downloading umodel");
                setCurrentProgress?.Invoke(0);
                return OnlineContent.EnsureStaticZippedExecutable("umodel_win32.zip", "umodel", "umodel.exe",
                        progressCallback, forceDownload: true);
            }
            else
            {
                return null; // OK
            }
        }
    }
}
