using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Windows.Input;
using ME3Explorer.Properties;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.MEDirectories;
using Microsoft.Win32;

namespace ME3Explorer.GameInterop
{
    public class InteropHelper
    {
        public static void InstallInteropASI()
        {
            string interopASIWritePath = GetInteropAsiWritePath();
            if (File.Exists(interopASIWritePath))
            {
                File.Delete(interopASIWritePath);
            }
            File.Copy(Path.Combine(App.ExecFolder, GameController.Me3ExplorerinteropAsiName), interopASIWritePath);
        }

        public static string GetInteropAsiWritePath()
        {
            string binariesWin32Dir = Path.GetDirectoryName(ME3Directory.ExecutablePath);
            string asiDir = Path.Combine(binariesWin32Dir, "ASI");
            Directory.CreateDirectory(asiDir);
            string interopASIWritePath = Path.Combine(asiDir, GameController.Me3ExplorerinteropAsiName);
            return interopASIWritePath;
        }

        public static bool IsME3Closed() => !GameController.TryGetME3Process(out _);

        public static void KillME3()
        {
            if (GameController.TryGetME3Process(out Process me3Process))
            {
                me3Process.Kill();
            }
            CommandManager.InvalidateRequerySuggested();
        }

        public static bool IsASILoaderInstalled()
        {
            if (!IsME3Installed())
            {
                return false;
            }
            string binariesWin32Dir = Path.GetDirectoryName(ME3Directory.ExecutablePath);
            string binkw23Path = Path.Combine(binariesWin32Dir, "binkw23.dll");
            string binkw32Path = Path.Combine(binariesWin32Dir, "binkw32.dll");
            const string binkw23MD5 = "128b560ef70e8085c507368da6f26fe6";
            const string binkw32MD5 = "1acccbdae34e29ca7a50951999ed80d5";

            return File.Exists(binkw23Path) && File.Exists(binkw32Path) && binkw23MD5 == CalculateMD5(binkw23Path) && binkw32MD5 == CalculateMD5(binkw32Path);

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

        public static bool IsME3Installed() => ME3Directory.ExecutablePath is string exePath && File.Exists(exePath);

        public static void SelectME3Path()
        {
            OpenFileDialog ofd = new OpenFileDialog
            {
                Title = "Select Mass Effect 3 executable.",
                Filter = "MassEffect3.exe|MassEffect3.exe"
            };
            if (ofd.ShowDialog() == true)
            {
                string gamePath = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(ofd.FileName)));

                Settings.Default.ME3Directory = ME3Directory.gamePath = gamePath;
                Settings.Default.Save();
                CommandManager.InvalidateRequerySuggested();
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
