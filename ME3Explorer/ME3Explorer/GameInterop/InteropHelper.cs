using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Windows.Input;
using ME3Explorer.Properties;
using ME3ExplorerCore.GameFilesystem;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.Packages;
using Microsoft.Win32;

namespace ME3Explorer.GameInterop
{
    public class InteropHelper
    {
        public static void InstallInteropASI(MEGame game)
        {
            string interopASIWritePath = GetInteropAsiWritePath(game);
            if (File.Exists(interopASIWritePath))
            {
                File.Delete(interopASIWritePath);
            }
            File.Copy(Path.Combine(App.ExecFolder, GameController.InteropAsiName(game)), interopASIWritePath);
        }

        public static string GetInteropAsiWritePath(MEGame game)
        {
            string exeDirPath = MEDirectories.GetExecutableFolderPath(game);
            string asiDir = Path.Combine(exeDirPath, "ASI");
            Directory.CreateDirectory(asiDir);
            string interopASIWritePath = Path.Combine(asiDir, GameController.InteropAsiName(game));
            return interopASIWritePath;
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
            Process.Start("https://github.com/Erik-JS/masseffect-binkw32");
        }

        public static bool IsGameInstalled(MEGame game) => MEDirectories.GetExecutablePath(game) is string exePath && File.Exists(exePath);

        public static void SelectGamePath(MEGame game)
        {
            if (game is MEGame.ME3)
            {
                OpenFileDialog ofd = new()
                {
                    Title = "Select Mass Effect 3 executable",
                    Filter = "MassEffect3.exe|MassEffect3.exe"
                };
                if (ofd.ShowDialog() == true)
                {
                    string gamePath = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(ofd.FileName)));

                    Settings.Default.ME3Directory = ME3Directory.DefaultGamePath = gamePath;
                    Settings.Default.Save();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
            else //ME2
            {
                OpenFileDialog ofd = new()
                {
                    Title = "Select Mass Effect 2 executable",
                    Filter = "MassEffect2.exe|MassEffect2.exe"
                };
                if (ofd.ShowDialog() == true)
                {
                    string gamePath = Path.GetDirectoryName(Path.GetDirectoryName(ofd.FileName));

                    Settings.Default.ME2Directory = ME2Directory.DefaultGamePath = gamePath;
                    Settings.Default.Save();
                    CommandManager.InvalidateRequerySuggested();
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
