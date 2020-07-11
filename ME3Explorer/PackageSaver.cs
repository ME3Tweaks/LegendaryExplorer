using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using ME3Explorer.GameInterop;
using ME3Explorer.Packages;
using ME3Explorer.Unreal;
using Newtonsoft.Json;
using StreamHelpers;

namespace ME3Explorer
{
    public static class PackageSaver
    {
        private static List<string> _me1TextureFiles;
        public static List<string> ME1TextureFiles => _me1TextureFiles ??= JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(Path.Combine(App.ExecFolder, "ME1TextureFiles.json")));

        public static bool CanReconstruct(this IMEPackage pcc) => CanReconstruct(pcc, pcc.FilePath);

        public static bool CanReconstruct(IMEPackage pckg, string path) =>
            pckg.Game == MEGame.UDK ||
            pckg.Game == MEGame.ME3 ||
            pckg.Game == MEGame.ME2 ||
            pckg.Game == MEGame.ME1 && ME1TextureFiles.TrueForAll(texFilePath => !path.EndsWith(texFilePath));

        private static Action<MEPackage, string, bool> MESaveDelegate;
        private static Action<UDKPackage, string, bool> UDKSaveDelegate;

        public static void Initialize()
        {
            UDKSaveDelegate = UDKPackage.RegisterSaver();
            MESaveDelegate = MEPackage.RegisterSaver();
        }

        public static void Save(this IMEPackage package)
        {
            if (package == null)
            {
                return;
            }
            switch (package)
            {
                case MEPackage mePackage:
                    Save(mePackage, package.FilePath);
                    break;
                case UDKPackage udkPackage:
                    Save(udkPackage, udkPackage.FilePath);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(package));
            }
        }

        public static void Save(this IMEPackage package, string path)
        {
            if (package == null)
            {
                return;
            }
            switch (package)
            {
                case MEPackage mePackage:
                    Save(mePackage, path);
                    break;
                case UDKPackage udkPackage:
                    Save(udkPackage, path);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(package));
            }
        }

        private static void Save(MEPackage pcc, string path)
        {
            bool isSaveAs = path != pcc.FilePath;
            int originalLength = -1;
            if (pcc.Game == MEGame.ME3 && !isSaveAs && pcc.FilePath.StartsWith(ME3Directory.BIOGamePath) && GameController.TryGetME3Process(out _))
            {
                try
                {
                    originalLength = (int)new FileInfo(pcc.FilePath).Length;
                }
                catch
                {
                    originalLength = -1;
                }
            }
            try
            {
                if (CanReconstruct(pcc, path))
                {
                    MESaveDelegate(pcc, path, isSaveAs);
                }
                else
                {
                    MessageBox.Show($"Cannot save ME1 packages with externally referenced textures. Please make an issue on github: {App.BugReportURL}", "Can't Save!",
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex) when (!App.IsDebug)
            {
                MessageBox.Show($"Error saving {pcc.FilePath}:\n{ex.FlattenException()}");
            }

            if (originalLength > 0)
            {
                string relativePath = Path.GetFullPath(pcc.FilePath).Substring(Path.GetFullPath(ME3Directory.gamePath).Length);
                var bin = new MemoryStream();
                bin.WriteInt32(originalLength);
                bin.WriteStringASCIINull(relativePath);
                File.WriteAllBytes(Path.Combine(ME3Directory.BinariesPath, "tocupdate"), bin.ToArray());
                GameController.SendTOCUpdateMessage();
            }
        }

        private static void Save(UDKPackage pcc, string path)
        {
            bool isSaveAs = path != pcc.FilePath;
            try
            {
                UDKSaveDelegate(pcc, path, isSaveAs);
            }
            catch (Exception ex) when (!App.IsDebug)
            {
                MessageBox.Show($"Error saving {pcc.FilePath}:\n{ex.FlattenException()}");
            }
        }
    }
}
