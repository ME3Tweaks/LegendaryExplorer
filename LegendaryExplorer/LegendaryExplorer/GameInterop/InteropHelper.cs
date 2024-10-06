using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Diagnostics;
using LegendaryExplorer.GameInterop.InteropTargets;
using LegendaryExplorer.Misc;
using LegendaryExplorerCore.Misc.ME3Tweaks;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorer.GameInterop
{
    public class InteropHelper
    {
        #region COMMON COMMANDS

        // Todo: Maybe move to target class

        /// <summary>
        /// Triggers a console event in kismet
        /// </summary>
        /// <param name="eventName"></param>
        /// <param name="game"></param>
        public static void CauseEvent(string eventName, MEGame game)
        {
            SendMessageToGame($"CAUSEEVENT {eventName}", game);
        }

        /// <summary>
        /// Triggers a remote event in kismet
        /// </summary>
        /// <param name="eventName"></param>
        /// <param name="game"></param>
        public static void RemoteEvent(string eventName, MEGame game)
        {
            // This doesn't work! We don't know how to trigger these in native right now. Would be good to figure out though.
            SendMessageToGame($"REMOTEEVENT {eventName}", game);
        }
        #endregion

        //Currently will not work, as ASIs are not included in LEX due to anti-virus software freaking out about them :(
        /*public static void InstallInteropASI(MEGame game)
        {
            string interopASIWritePath = GetInteropAsiWritePath(game);
            if (File.Exists(interopASIWritePath))
            {
                File.Delete(interopASIWritePath);
            }
            
            DeletePreLEXInteropASI(game);

            File.Copy(Path.Combine(AppDirectories.ExecFolder, GameController.InteropAsiName(game)), interopASIWritePath);
        }*/

        /// <summary>
        /// Checks if an ASI with the matching md5 is installd in the specified folder.
        /// </summary>
        /// <param name="md5"></param>
        /// <param name="asiPath"></param>
        /// <returns></returns>
        private static bool IsASIInstalled(string md5ToMatch, string asiFolder)
        {
            if (Directory.Exists(asiFolder))
            {
                var files = Directory.GetFiles(asiFolder, "*.asi", SearchOption.TopDirectoryOnly);
                foreach (var f in files)
                {
                    var md5 = CalculateMD5(f);
                    if (md5 == md5ToMatch)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static void DeletePreLEXInteropASI(MEGame game)
        {
            string asiDir = GetAsiDir(game);
            string oldASIFileName = GameController.GetInteropTargetForGame(game).OldInteropASIName;
            if (oldASIFileName != null)
            {
                string oldASIPath = Path.Combine(asiDir, oldASIFileName);
                if (File.Exists(oldASIPath))
                {
                    File.Delete(oldASIPath);
                }
            }
        }

        public static string GetAsiDir(MEGame game)
        {
            string asiDir = MEDirectories.GetASIPath(game);
            Directory.CreateDirectory(asiDir);
            return asiDir;
        }

        public static bool IsGameClosed(MEGame game) => !GameController.TryGetMEProcess(game, out _);

        public static void KillGame(MEGame game)
        {
            if (GameController.TryGetMEProcess(game, out Process process))
            {
                process.Kill();
            }
            CommandManager.InvalidateRequerySuggested();
        }

        public static bool IsASILoaderInstalled(MEGame game)
        {
            if (!IsGameInstalled(game))
            {
                return false;
            }

            InteropTarget target = GameController.GetInteropTargetForGame(game);
            string dllDir = MEDirectories.GetExecutableFolderPath(game);
            if (game.IsOTGame())
            {
                string binkw23Path = Path.Combine(dllDir, "binkw23.dll");
                string binkw32Path = Path.Combine(dllDir, "binkw32.dll");
                // const string me1binkw23MD5 = "d9e2a3b9303ca80560218af9f6eebaae";
                // const string me1binkw32MD5 = "30660f25ab7f7435b9f3e1a08422411a";

                return File.Exists(binkw23Path) && File.Exists(binkw32Path)
                                                && target.BinkBypassMD5 == CalculateMD5(binkw32Path)
                                                && target.OriginalBinkMD5 == CalculateMD5(binkw23Path);
            }
            else if (game.IsLEGame())
            {
                string binkPath = Path.Combine(dllDir, "bink2w64.dll");
                string originalBinkPath = Path.Combine(dllDir, "bink2w64_original.dll");
                var binkVersionInfo = FileVersionInfo.GetVersionInfo(binkPath);
                var binkProductName = binkVersionInfo.ProductName ?? "";

                if (!File.Exists(binkPath) || !File.Exists(originalBinkPath)) return false;
                var hash = CalculateMD5(originalBinkPath);

                // This extra hash is enhanced 2022.05 bink version (Mod Manager 8.1 installs this)
                return (target.OriginalBinkMD5 == hash || @"31d1d74866061bf66baad1cc4db3c19e" == hash)
                       && binkProductName.StartsWith("LEBinkProxy", StringComparison.CurrentCultureIgnoreCase)
                       && binkVersionInfo.ProductMajorPart >= 2;
            }
            return false;
        }

        public static bool IsME3ConsoleExtensionInstalled()
        {
            const MEGame game = MEGame.ME3;
            if (!IsGameInstalled(game))
            {
                return false;
            }
            string asiDir = GetAsiDir(game);

            // Using source generator this might be possible to parse from asi mods endpoint on ME3Tweaks
            return IsASIInstalled("bce3183d90af020768bb98f9539467bd", asiDir);
        }

        public static bool IsInteropASIInstalled(MEGame game)
        {
            if (!IsGameInstalled(game))
            {
                return false;
            }

            DeletePreLEXInteropASI(game);

            // 05/15/2022
            // Change to scanning for matching md5
            // This way you can install an asi with any name and it will determine if another same-asi
            // is installed (this won't handle different builds/versions like mod manager but it's better
            // than not doing any check at all)
            // - Mgamerz
#if DEBUG
            // If you're developing the LEX Interop ASI you can 
            // force it to think it's installed and ignore the MD5 check.
            if (game is MEGame.LE1 or MEGame.LE3)
                return true; // DEV ONLY
#endif
            string asiDir = GetAsiDir(game);
            string asiMD5 = GameController.GetInteropTargetForGame(game).InteropASIMD5;
            return IsASIInstalled(asiMD5, asiDir);
        }

        //https://stackoverflow.com/a/10520086
        public static string CalculateMD5(string filename)
        {
            using var md5 = MD5.Create();
            using var stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            byte[] hash = md5.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        public static void OpenASILoaderDownload(MEGame game)
        {
            // 06/06/2022 
            // Allow bink installation request from ME3TweaksModManager if 
            // the build number is 126 or higher (ME3Tweaks Mod Manager 8.0 Beta 3 - June 06 2022)

            bool requestedInstall = false;
            if (ModManagerIntegration.GetModManagerBuildNumber() >= 126)
            {
                requestedInstall = ModManagerIntegration.RequestBinkInstallation(game);
            }

            if (!requestedInstall)
            {
                if (game.IsLEGame())
                {
                    var result = MessageBox.Show("Install the ASI loader with ME3Tweaks Mod Manager, in the 'Tools > Bink Bypasses' menu. Click OK to open the Mod Manager download page.",
                        "ASI Loader Installation Instructions", MessageBoxButton.OKCancel, MessageBoxImage.Information);
                    if (result == MessageBoxResult.OK)
                    {
                        HyperlinkExtensions.OpenURL("https://me3tweaks.com/modmanager/");
                    }
                }
                else
                {
                    HyperlinkExtensions.OpenURL("https://github.com/Erik-JS/masseffect-binkw32");
                }
            }
        }

        public static void OpenME3ConsoleExtensionDownload()
        {
            HyperlinkExtensions.OpenURL("https://github.com/ME3Tweaks/ME3-ASI-Plugins/releases/tag/v1.0-ConsoleExtension");
        }

        public static void OpenInteropASIDownload(MEGame game)
        {
            // Allow if the build number is 127 or higher (ME3Tweaks Mod Manager 8.0.1 Beta)

            bool requestedInstall = false;
            if (ModManagerIntegration.GetModManagerBuildNumber() >= 127)
            {
                switch (game)
                {
                    case MEGame.LE1:
                        requestedInstall = ModManagerIntegration.RequestASIInstallation(game, ASIModIDs.LE1_LEX_INTEROP);
                        break;
                    case MEGame.LE2:
                        requestedInstall = ModManagerIntegration.RequestASIInstallation(game, ASIModIDs.LE2_LEX_INTEROP);
                        break;
                    case MEGame.LE3:
                        requestedInstall = ModManagerIntegration.RequestASIInstallation(game, ASIModIDs.LE3_LEX_INTEROP);
                        break;
                }
            }

            if (!requestedInstall)
            {
                HyperlinkExtensions.OpenURL(GameController.GetInteropTargetForGame(game).InteropASIDownloadLink);
            }
        }

        public static bool IsGameInstalled(MEGame game) => MEDirectories.GetExecutablePath(game) is string exePath && File.Exists(exePath);

        public static void SelectGamePath(MEGame game) => GameController.GetInteropTargetForGame(game).SelectGamePath();

        public static bool TryPadFile(string tempFilePath, int paddedSize = 52_428_800 /* 50 MB */)
        {
            using FileStream fs = File.OpenWrite(tempFilePath);
            fs.Seek(0, SeekOrigin.End);
            long size = fs.Position;
            if (size <= paddedSize)
            {
                var paddingSize = paddedSize - size;
                fs.WriteZeros((uint)paddingSize);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Sends a message to a game via a pipe. The game must have the LEX Interop ASI installed that handles the command for it to do anything.
        /// </summary>
        /// <param name="message">Must have fewer than 1024 characters</param>
        /// <param name="game"></param>
        public static void SendMessageToGame(string message, MEGame game)
        {
            Guard.IsLessThan(message.Length, 1024);
            if (IsGameClosed(game))
            {
                Debug.WriteLine($"{game} is not running! Cannot send command {message}");
                return;
            }

            // We make new pipe and connect to game every command
            using var client = new NamedPipeClientStream(".", $"LEX_{game}_COMM_PIPE", PipeDirection.Out);
            client.Connect(3000);

            var pipeWriter = new StreamWriter(client);

            // For debugging
            // Thread.Sleep(3000);
            Debug.WriteLine($"SendMessageToGame({game}): {message}");
            pipeWriter.Write(message);
            pipeWriter.Write('\0');
            pipeWriter.Write('\r'); //prevent old versions of the interop asi from OOB writes/reads
            pipeWriter.Flush();
        }

        /// <summary>
        /// Used to track memory-packages. We will want to identify when we can dump this - such as when all tools the interact with game directly are closed
        /// </summary>
        private static Dictionary<MEGame, CaseInsensitiveDictionary<IMEPackage>> FilesSentToGame = new();

        /// <summary>
        /// Sends a file to a game via a pipe. This file will be loaded instead of an on-disk version (one time!)
        /// The game must have the LEX Interop ASI installed that handles the command for it to do anything.
        /// </summary>
        /// <param name="fileNameOverride">Filename to send - without extension</param>
        public static void SendFileToGame(IMEPackage pcc, string fileNameOverride = null)
        {
            MEGame game = pcc.Game;
            fileNameOverride ??= pcc.FileNameNoExtension;
            if (IsGameClosed(game))
            {
                Debug.WriteLine($"{game} is not running! Cannot send file");
                return;
            }

            MemoryStream savedFile = pcc.SaveToStream(false);
            savedFile.Position = 0;

            // We make new pipe and connect to game every command
            using var client = new NamedPipeClientStream(".", $"LEX_{game}_COMM_PIPE", PipeDirection.Out);
            client.Connect(3000);

            var pipeWriter = new StreamWriter(client);

            pipeWriter.Write($"PRECACHE_FILE {fileNameOverride} {savedFile.Length}");
            pipeWriter.Write('\0');
            pipeWriter.Flush();

            //underlying array will almost certainly be bigger than the actual data.
            //Slice it so we don't waste time and memory copying junk
            ReadOnlySpan<byte> buffer = savedFile.GetBuffer().AsSpan()[..(int)savedFile.Length];
            client.Write(buffer);
            client.Flush();
            if (!FilesSentToGame.TryGetValue(pcc.Game, out var map))
            {
                map = new CaseInsensitiveDictionary<IMEPackage>();
                FilesSentToGame[pcc.Game] = map;
            }
            map[pcc.FilePath] = pcc;
        }

        public static CaseInsensitiveDictionary<IMEPackage> GetFilesSentToGame(MEGame game)
        {
            if (!FilesSentToGame.TryGetValue(game, out var map))
            {
                // Don't return null
                map = new CaseInsensitiveDictionary<IMEPackage>();
                FilesSentToGame[game] = map;
            }

            return map;
        }
    }

}
