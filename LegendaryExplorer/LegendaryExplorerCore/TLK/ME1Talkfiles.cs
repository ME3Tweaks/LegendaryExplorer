using System.Collections.Generic;
using System.IO;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.TLK.ME1;

namespace LegendaryExplorerCore.TLK
{
    /// <summary>
    /// Manages stringref lookup for LE1 tlks
    /// </summary>
    public static class ME1TalkFiles
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

            //Look in package local first
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

            //Look in loaded list
            foreach (ME1TalkFile tlk in LoadedTlks)
            {
                s = tlk.FindDataById(strRefID, withFileName);
                if (s != "No Data")
                {
                    return s;
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
