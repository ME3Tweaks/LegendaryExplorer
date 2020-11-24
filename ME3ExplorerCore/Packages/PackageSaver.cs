using System;
using System.Collections.Generic;
using System.IO;
using ME3ExplorerCore.GameFilesystem;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.Unreal.Classes;
using Newtonsoft.Json;

namespace ME3ExplorerCore.Packages
{
    public static class PackageSaver
    {
        private static List<string> _me1TextureFiles;
        internal static List<string> ME1TextureFiles => _me1TextureFiles ??= JsonConvert.DeserializeObject<List<string>>(Utilities.LoadStringFromCompressedResource("Infos.zip", "ME1TextureFiles.json"));

        /// <summary>
        /// Callback that is invoked when a package fails to save, hook this up to show a message to the user that something failed
        /// </summary>
        public static Action<string> PackageSaveFailedCallback { get; set; }

        public static bool CanReconstruct(this IMEPackage pcc) => CanReconstruct(pcc, pcc.FilePath);

        public static bool CanReconstruct(IMEPackage pckg, string path) =>
            pckg.Game == MEGame.UDK ||
            pckg.Game == MEGame.ME3 ||
            pckg.Game == MEGame.ME2 ||
            pckg.Game == MEGame.ME1 && ME1TextureFiles.TrueForAll(texFilePath => !path.EndsWith(texFilePath));

        private static Action<MEPackage, string, bool, bool, bool, bool> MESaveDelegate;
        private static Action<UDKPackage, string, bool> UDKSaveDelegate;

        public static void Initialize()
        {
            UDKSaveDelegate = UDKPackage.RegisterSaver();
            MESaveDelegate = MEPackage.RegisterSaver();
        }

        /// <summary>
        /// Saves the package to disk.
        /// </summary>
        /// <param name="package"></param>
        /// <param name="compress"></param>
        /// <param name="includeAdditionalPackagesToCook"></param>
        /// <param name="includeDependencyTable"></param>
        public static void Save(this IMEPackage package, string savePath = null, bool compress = false, bool includeAdditionalPackagesToCook = true, bool includeDependencyTable = true)
        {
            if (package == null)
            {
                return;
            }

            if (package.FilePath is null && savePath == null)
            {
                throw new InvalidOperationException("Cannot save a temporary memory-based package! You must pass a save path to save a memory package.");
            }
            switch (package)
            {
                case MEPackage mePackage:
                    MESave(mePackage, savePath, compress, includeAdditionalPackagesToCook, includeDependencyTable);
                    break;
                case UDKPackage udkPackage:
                    Save(udkPackage, savePath);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(package));
            }
        }

        ///// <summary>
        ///// Saves this package to a stream. This does not mark the package as no longer modified like the save to disk does.
        ///// </summary>
        ///// <param name="package"></param>
        ///// <param name="compress"></param>
        ///// <returns></returns>
        //public static MemoryStream SaveToStream(this IMEPackage package, bool compress = false)
        //{
        //    if (package == null)
        //    {
        //        return null;
        //    }
        //    switch (package)
        //    {
        //        case MEPackage mePackage:
        //            return mePackage.SaveToStream(compress);
        //        case UDKPackage udkPackage:
        //            return udkPackage.SaveToStream(compress);
        //        default:
        //            throw new ArgumentOutOfRangeException(nameof(package));
        //    }
        //}

        /// <summary>
        /// Used to test if ME3 is running. USed by ME3Explorer GameController class
        /// </summary>
        public static Func<bool> CheckME3Running { get; set; }
        /// <summary>
        /// Notifies that a TOC update is required for a running instance of a game (for ME3 only).
        /// </summary>
        public static Func<bool> NotifyRunningTOCUpdateRequired { get; set; }

        public static Func<Texture2D, byte[]> GetPNGForThumbnail { get; set; }

        private static void MESave(MEPackage pcc, string savePath, bool compress = false, bool includeAdditionalPackagesToCook = true, bool includeDependencyTable = true)
        {
            bool isSaveAs = savePath != null && savePath != pcc.FilePath;
            int originalLength = -1;
            if (pcc.Game == MEGame.ME3 && !isSaveAs && pcc.FilePath.StartsWith(ME3Directory.GetBioGamePath()) && CheckME3Running != null && CheckME3Running.Invoke())
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
                if (CanReconstruct(pcc, savePath))
                {
                    MESaveDelegate(pcc, savePath, isSaveAs, compress, includeAdditionalPackagesToCook, includeDependencyTable);
                }
                else
                {
                    PackageSaveFailedCallback?.Invoke($"Cannot save ME1 packages with externally referenced textures. Please make an issue on github: {CoreLib.BugReportURL}");
                }
            }
            catch (Exception ex) when (!CoreLib.IsDebug)
            {
                PackageSaveFailedCallback?.Invoke($"Error saving {pcc.FilePath}:\n{ex.FlattenException()}");
            }

            if (originalLength > 0)
            {
                string relativePath = Path.GetFullPath(pcc.FilePath).Substring(Path.GetFullPath(ME3Directory.DefaultGamePath).Length);
                var bin = new MemoryStream();
                bin.WriteInt32(originalLength);
                bin.WriteStringASCIINull(relativePath);
                File.WriteAllBytes(Path.Combine(ME3Directory.ExecutableFolder, "tocupdate"), bin.ToArray());
                // oh boy...
                NotifyRunningTOCUpdateRequired();
                // replaced:
                //GameController.SendTOCUpdateMessage();
            }
        }

        private static void UDKSave(UDKPackage pcc, string path)
        {
            bool isSaveAs = path != pcc.FilePath;
            try
            {
                UDKSaveDelegate(pcc, path, isSaveAs);
            }
            catch (Exception ex) when (!CoreLib.IsDebug)
            {
                PackageSaveFailedCallback?.Invoke($"Error saving {pcc.FilePath}:\n{ex.FlattenException()}");
            }
        }
    }
}
