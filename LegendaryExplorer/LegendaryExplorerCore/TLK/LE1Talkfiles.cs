using System.Collections.Generic;
using System.IO;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.TLK.ME1;

namespace LegendaryExplorerCore.TLK
{
    /// <summary>
    /// Manages stringref lookup for LE1 tlks
    /// </summary>
    public static class LE1TalkFiles
    {
        /// <summary>
        /// The tlks that will be searched by <see cref="FindDataById"/>
        /// </summary>
        public static readonly List<ME1TalkFile> LoadedTlks = new();
        
        /// <summary>
        /// Adds the BioTalkFile at <paramref name="uIndex"/> in the LE1 package file at <paramref name="filePath"/> to the loaded tlks.
        /// </summary>
        /// <param name="filePath">Path of the LE1 package file to look in</param>
        /// <param name="uIndex">Uindex of the BioTalkFile export to load</param>
        public static void LoadTlkData(string filePath, int uIndex)
        {
            if (File.Exists(filePath))
            {
                using IMEPackage pcc = MEPackageHandler.UnsafePartialLoad(filePath, exp => exp.UIndex == uIndex);
                LoadedTlks.Add(new ME1TalkFile(pcc, uIndex));
            }
        }

        /// <summary>
        /// Gets the string corresponding to the <paramref name="strRefID"/> (wrapped in quotes), if it exists in the tlks in <paramref name="package"/> or in the loaded tlks. If not found, returns <c>"No Data"</c>
        /// </summary>
        /// <param name="strRefID"></param>
        /// <param name="package">If not null, looks through the tlks in this package first</param>
        /// <param name="withFileName">Optional: Should the filename be appended to the returned string</param>
        public static string FindDataById(int strRefID, IMEPackage package, bool withFileName = false)
        {
            string s = "No Data";

            if (package.Game == MEGame.LE1)
            {
                // LE1 - First check override TLKs in priority order (highest to lowest priority)
                // LoadedTlks is in mount priority order (lowest to highest), so iterate in reverse
                for (int i = LoadedTlks.Count - 1; i >= 0; i--)
                {
                    ME1TalkFile tlk = LoadedTlks[i];
                    if (tlk.IsLE1OverrideTLK)
                    {
                        s = tlk.FindDataById(strRefID, withFileName);
                        if (s != "No Data")
                        {
                            return s;
                        }
                    }
                }
            }

            // If not found in override TLKs, check package local tlks
            if (package != null)
            {
                foreach (ME1TalkFile tlk in package.LocalTalkFiles)
                {
                    s = tlk.FindDataById(strRefID, withFileName);
                    if (s != "No Data")
                    {
                        return s;
                    }
                }
            }

            // Finally check non-override TLKs in reverse order (highest to lowest priority)
            for (int i = LoadedTlks.Count - 1; i >= 0; i--)
            {
                ME1TalkFile tlk = LoadedTlks[i];
                if (!tlk.IsLE1OverrideTLK) // This is always be false in ME1
                {
                    s = tlk.FindDataById(strRefID, withFileName);
                    if (s != "No Data")
                    {
                        return s;
                    }
                }
            }

            return s;
        }

        /// <summary>
        /// Clears the loaded Tlks.
        /// </summary>
        public static void ClearLoadedTlks()
        {
            LoadedTlks.Clear();
        }
    }
}
