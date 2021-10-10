using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Windows.Input;
using LegendaryExplorer.GameInterop.InteropTargets;
using LegendaryExplorer.Misc;
using LegendaryExplorer.Misc.AppSettings;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using Microsoft.Win32;

namespace LegendaryExplorer.GameInterop
{
    public class InteropHelper
    {
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

        public static string GetInteropAsiWritePath(MEGame game)
        {
            string asiDir = GetAsiDir(game);
            string interopASIWritePath = Path.Combine(asiDir, GameController.GetInteropTargetForGame(game).InteropASIName);
            return interopASIWritePath;
        }

        private static string GetAsiDir(MEGame game)
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

                return File.Exists(binkPath) && File.Exists(originalBinkPath)
                                             && target.OriginalBinkMD5 == CalculateMD5(originalBinkPath)
                                             && binkProductName.StartsWith("LEBinkProxy")
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
            string asiPath = Path.Combine(asiDir, "ConsoleExtension-v1.0.asi");
            const string asiMD5 = "bce3183d90af020768bb98f9539467bd";
            return File.Exists(asiPath) && asiMD5 == CalculateMD5(asiPath);
        }

        public static bool IsInteropASIInstalled(MEGame game)
        {
            if (!IsGameInstalled(game))
            {
                return false;
            }

            DeletePreLEXInteropASI(game);
            string asiPath = GetInteropAsiWritePath(game);
            string asiMD5 = GameController.GetInteropTargetForGame(game).InteropASIMD5;
            return File.Exists(asiPath) && asiMD5 == CalculateMD5(asiPath);
        }

        //https://stackoverflow.com/a/10520086
        public static string CalculateMD5(string filename)
        {
            using var md5 = MD5.Create();
            using var stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            byte[] hash = md5.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        public static void OpenASILoaderDownload()
        {
            HyperlinkExtensions.OpenURL("https://github.com/Erik-JS/masseffect-binkw32");
        }

        public static void OpenConsoleExtensionDownload()
        {
            HyperlinkExtensions.OpenURL("https://github.com/ME3Tweaks/ME3-ASI-Plugins/releases/tag/v1.0-ConsoleExtension");
        }

        public static void OpenInteropASIDownload(MEGame game) => HyperlinkExtensions.OpenURL(GameController.GetInteropTargetForGame(game).InteropASIDownloadLink);

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
    }
}
