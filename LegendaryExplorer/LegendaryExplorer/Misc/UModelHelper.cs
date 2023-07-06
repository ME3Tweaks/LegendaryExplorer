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
using LegendaryExplorer.Dialogs;
using LegendaryExplorerCore.Unreal.Classes;

namespace LegendaryExplorer.Misc
{
    public static class UModelHelper
    {
        public const int SupportedUModelBuildNum = 1589;
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
                // 07/04/2023: Support alternate mesh formats that UEViewer supports
                var prompt = new DropdownPromptDialog("Select a mesh output format. The default is PSK.",
                    "Select mesh format", "Mesh format", new List<string>() { "psk", "gltf", "md5" }, window);
                prompt.ShowDialog();
                if (prompt.DialogResult == true)
                {
                    var meshFormat = prompt.Response;

                    var bw = new BackgroundWorker();
                    bw.DoWork += async (_, _) =>
                    {
                        string umodel = Path.Combine(AppDirectories.StaticExecutablesDirectory, "umodel", "umodel.exe");
                        var args = new List<string>
                        {
                            "-export",
                            $"-{meshFormat}",
                            $"-out=\"{dlg.FileName}\"",
                            export.FileRef.FilePath,
                            export.ObjectNameString,
                            export.ClassName
                        };

                        // TEMP WORKAROUND UNTIL UMODEL IS FIXED
                        if (meshFormat == "psk")
                        {
                            args = new List<string>
                            {
                                "-export",
                                // $"-{meshFormat}",
                                $"-out=\"{dlg.FileName}\"",
                                export.FileRef.FilePath,
                                export.ObjectNameString,
                                export.ClassName
                            };
                        }

                        // This doesn't properly quote things technically. It's just for review.
                        Debug.WriteLine($"Executing process: {umodel} {string.Join(" ", args)}");

                        // Maybe make this method return a callback to set the logs?
                        var result = await Cli.Wrap(umodel)
                            .WithArguments(args)
                            .WithValidation(CommandResultValidation.None)
                            .WithStandardOutputPipe(PipeTarget.ToDelegate((line) => Debug.WriteLine(line)))
                            .ExecuteAsync();

                        if (result.ExitCode != 0)
                        {
                            Debug.WriteLine($"Error running umodel: result code was {result.ExitCode}");
                            return; // There was an error. Maybe we should communicate this to the user somehow.
                        }

                        // 07/04/2023: Search the Texture2D folder and try to export larger resolution versions since
                        // UModel seems unable to handle TFC mips - it's probably how we are calling it, not doing game
                        // scan

                        var t2dF = Path.Combine(dlg.FileName, export.FileRef.FileNameNoExtension, "Texture2D");
                        if (Directory.Exists(t2dF))
                        {
                            var textureFiles = Directory.GetFiles(t2dF, "*.tga", SearchOption.TopDirectoryOnly);
                            foreach (var textureFile in textureFiles)
                            {
                                var textureName = Path.GetFileNameWithoutExtension(textureFile);
                                var possibleTextures = export.FileRef.Exports
                                    .Where(x => x.IsTexture() && x.ObjectName == textureName).ToList();
                                if (possibleTextures.Count == 1)
                                {
                                    var t2d = new Texture2D(possibleTextures[0]);
                                    if (t2d.Mips.Any(x => !x.IsPackageStored))
                                    {
                                        // We need to extract textures
                                        t2d.ExportToFile(textureFile);
                                    }
                                }
                            }
                        }

                        Process.Start("explorer", dlg.FileName);
                    };
                    bw.RunWorkerAsync();
                }
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
