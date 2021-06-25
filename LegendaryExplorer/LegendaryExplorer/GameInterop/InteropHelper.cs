using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Windows.Input;
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
            if (game is MEGame.ME2 or MEGame.ME3)
            {
                string oldASIName = Path.Combine(asiDir, GameController.OldInteropAsiName(game));
                if (File.Exists(oldASIName))
                {
                    File.Delete(oldASIName);
                }
            }
        }

        public static string GetInteropAsiWritePath(MEGame game)
        {
            string asiDir = GetAsiDir(game);
            string interopASIWritePath = Path.Combine(asiDir, GameController.InteropAsiName(game));
            return interopASIWritePath;
        }

        private static string GetAsiDir(MEGame game)
        {
            string exeDirPath = MEDirectories.GetExecutableFolderPath(game);
            string asiDir = Path.Combine(exeDirPath, "ASI");
            Directory.CreateDirectory(asiDir);
            return asiDir;
        }

        public static bool IsME3Closed() => !GameController.TryGetMEProcess(MEGame.ME3, out _);

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
            string dllDir = MEDirectories.GetExecutableFolderPath(game);
            string binkw23Path = Path.Combine(dllDir, "binkw23.dll");
            string binkw32Path = Path.Combine(dllDir, "binkw32.dll");
            const string me3binkw23MD5 = "128b560ef70e8085c507368da6f26fe6";
            const string me3binkw32MD5 = "1acccbdae34e29ca7a50951999ed80d5";
            const string me2binkw23MD5 = "56a99d682e752702604533b2d5055a5e";
            const string me2binkw32MD5 = "a5318e756893f6232284202c1196da13";
            const string me1binkw23MD5 = "d9e2a3b9303ca80560218af9f6eebaae";
            const string me1binkw32MD5 = "30660f25ab7f7435b9f3e1a08422411a";

            // TODO: use a different method for LEbinkproxy detection, as that one may be getting updates (unlike the OT ASIs).

            return File.Exists(binkw23Path) && File.Exists(binkw32Path)
                && game switch {
                    MEGame.ME1 => me1binkw23MD5,
                    MEGame.ME2 => me2binkw23MD5,
                    MEGame.ME3 => me3binkw23MD5,
                    _ => throw new ArgumentOutOfRangeException(nameof(game), game, null)
                } == CalculateMD5(binkw23Path)
                && game switch
                {
                    MEGame.ME1 => me1binkw32MD5,
                    MEGame.ME2 => me2binkw32MD5,
                    MEGame.ME3 => me3binkw32MD5,
                    _ => throw new ArgumentOutOfRangeException(nameof(game), game, null)
                } == CalculateMD5(binkw32Path);

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
            const string me2MD5 = "a65d9325dd3b0ec5ea4184cc10e5e692";
            const string me3MD5 = "7ac354e16e62434de656f7eea3259316";
            return File.Exists(asiPath) && game switch
            {
                MEGame.ME2 => me2MD5,
                MEGame.ME3 => me3MD5,
                _ => throw new ArgumentOutOfRangeException(nameof(game), game, null)
            } == CalculateMD5(asiPath);
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

        public static void OpenInteropASIDownload(MEGame game)
        {
            switch (game)
            {
                case MEGame.ME3:
                    HyperlinkExtensions.OpenURL("https://github.com/ME3Tweaks/ME3-ASI-Plugins/releases/tag/v2.0-LegendaryExplorerInterop");
                    break;
                case MEGame.ME2:
                    HyperlinkExtensions.OpenURL("https://github.com/ME3Tweaks/ME2-ASI-Plugins/releases/tag/v2.0-LegendaryExplorerInterop");
                    break;
            }
        }

        public static bool IsGameInstalled(MEGame game) => MEDirectories.GetExecutablePath(game) is string exePath && File.Exists(exePath);

        public static void SelectGamePath(MEGame game)
        {
            switch (game)
            {
                case MEGame.ME2:
                {
                    OpenFileDialog ofd = new()
                    {
                        Title = "Select Mass Effect 2 executable",
                        Filter = "MassEffect2.exe|MassEffect2.exe"
                    };
                    if (ofd.ShowDialog() == true)
                    {
                        string gamePath = Path.GetDirectoryName(Path.GetDirectoryName(ofd.FileName));

                        Settings.Global_ME2Directory = ME2Directory.DefaultGamePath = gamePath;
                        CommandManager.InvalidateRequerySuggested();
                    }
                    break;
                }
                case MEGame.ME3:
                {
                    OpenFileDialog ofd = new()
                    {
                        Title = "Select Mass Effect 3 executable",
                        Filter = "MassEffect3.exe|MassEffect3.exe"
                    };
                    if (ofd.ShowDialog() == true)
                    {
                        string gamePath = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(ofd.FileName)));

                        Settings.Global_ME3Directory = ME3Directory.DefaultGamePath = gamePath;
                        CommandManager.InvalidateRequerySuggested();
                    }

                    break;
                }
            }
        }

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
