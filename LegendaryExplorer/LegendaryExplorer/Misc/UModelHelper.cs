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
using CliWrap;
using DocumentFormat.OpenXml.Drawing.Charts;

namespace LegendaryExplorer.Misc
{
    public static class UModelHelper
    {
        public const int SupportedUModelBuildNum = 1555;
        public static async Task<int> GetLocalUModelVersionAsync()
        {
            int version = 0;

            void handleLine(string str)
            {
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
            }


            var umodel = Path.Combine(AppDirectories.StaticExecutablesDirectory, @"umodel", @"umodel.exe");
            if (File.Exists(umodel))
            {
                try
                {
                    var result = await Cli.Wrap(umodel)
                        .WithArguments("-version")
                        .WithStandardOutputPipe(PipeTarget.ToDelegate(handleLine))
                        .ExecuteAsync();
                    if (result.ExitCode != 0)
                    {
                        Debug.WriteLine($"Error determining umodel version: result code was {result.ExitCode}");
                        return 0; // There was an error 
                    }
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
                bw.DoWork += async (_, _) =>
                {
                    string umodel = Path.Combine(AppDirectories.StaticExecutablesDirectory, "umodel", "umodel.exe");
                    var args = new List<string>
                    {
                        "-export",
                        $"-out=\"{dlg.FileName}\"",
                        export.FileRef.FilePath,
                        export.ObjectNameString,
                        export.ClassName
                    };

                    // Maybe make this method return a callback to set the logs?
                    var result = await Cli.Wrap(umodel)
                        .WithArguments(args)
                        .WithValidation(CommandResultValidation.None)
                        .WithStandardOutputPipe(PipeTarget.ToDelegate((line) => Debug.WriteLine(line)))
                        .ExecuteAsync();
                    
                    if (result.ExitCode != 0)
                    {
                        Debug.WriteLine($"Error determining umodel version: result code was {result.ExitCode}");
                        return; // There was an error. Maybe we should communicate this to the user somehow.
                    }
                    
                    Process.Start("explorer", dlg.FileName);
                };
                bw.RunWorkerAsync();
            }
        }

        /// <summary>
        /// Downloads and caches UModel. Returns error message string if error, null if OK. This is a blocking call; it should run on a background thread.
        /// </summary>
        public static string EnsureUModel(Action setDownloading, Action<int> setMaxProgress, Action<int> setCurrentProgress, Action<string> setText)
        {
            if (UModelHelper.GetLocalUModelVersionAsync().Result < UModelHelper.SupportedUModelBuildNum)
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
