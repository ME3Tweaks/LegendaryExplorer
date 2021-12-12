using System.IO;
using System.Collections.Generic;
using LegendaryExplorerCore.TLK.ME2ME3;

namespace LegendaryExplorerCore.TLK
{
    /// <summary>
    /// Manages stringref lookup for LE3 tlks
    /// </summary>
    public static class LE3TalkFiles
    {
        /// <summary>
        /// The tlks that will be searched by <see cref="FindDataById"/>
        /// </summary>
        public static readonly List<ME2ME3LazyTLK> LoadedTlks = new();

        /// <summary>
        /// Adds the .tlk file at <paramref name="filePath"/> to the loaded tlks.
        /// </summary>
        /// <param name="filePath">Path of the .tlk file to load</param>
        public static void LoadTlkData(string filePath)
        {
            if (File.Exists(filePath))
            {
                var tlk = new ME2ME3LazyTLK();
                tlk.LoadTlkData(filePath);
                LoadedTlks.Add(tlk);
            }
        }

        /// <summary>
        /// Gets the string corresponding to the <paramref name="strRefID"/> (wrapped in quotes), if it exists in the loaded tlks. If it does not, returns <c>"No Data"</c>
        /// </summary>
        /// <param name="strRefID"></param>
        /// <param name="withFileName">Optional: Should the filename be appended to the returned string</param>
        public static string FindDataById(int strRefID, bool withFileName = false)
        {
            string s = "No Data";
            foreach (ME2ME3LazyTLK tlk in LoadedTlks)
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
