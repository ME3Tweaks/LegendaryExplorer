using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.TLK
{
    /// <summary>
    /// Interface for talk files (ME1 and ME2/3 style). This lets you interchange TLKs without worrying about the class types.
    /// </summary>
    public interface ITalkFile
    {
        /// <summary>
        /// The localization of this TLK
        /// </summary>
        public MELocalization Localization { get; set; }

        /// <summary>
        /// List of TLK String references contained in this TLK
        /// </summary>
        public List<TLKStringRef> StringRefs { get; set; }

        /// <summary>
        /// If the TLK has been modified
        /// </summary>
        public bool IsModified { get; set; }

        /// <summary>
        /// Converts this TalkFile to an XML representation of it that can be edited in external tools
        /// </summary>
        /// <param name="filePath"></param>
        public void SaveToXML(string filePath);

        /// <summary>
        /// Replaces a string in the list of StringRefs.
        /// </summary>
        /// <param name="stringID">The ID of the string to replace.</param>
        /// <param name="newText">The new text of the string.</param>
        /// <param name="addIfNotFound">If the string should be added as new stringref if it is not found. Default is false.</param>
        /// <returns>True if the string was found, false otherwise.</returns>
        public bool ReplaceString(int stringID, string newText, bool addIfNotFound = false);

        /// <summary>
        /// Fetches a string by its ID. If the TLK is from ME1, the 'male' flag is ignored, as it uses entirely separate TLKs.
        /// </summary>
        /// <param name="strRefID"></param>
        /// <param name="withFileName"></param>
        /// <param name="returnNullIfNotFound"></param>
        /// <param name="noQuotes"></param>
        /// <param name="male"></param>
        /// <returns></returns>
        public string FindDataById(int strRefID, bool withFileName = false, bool returnNullIfNotFound = false, bool noQuotes = false, bool male = true);

        /// <summary>
        /// Finds the first instance of a string and returns its string index. If none is found, it returns -1. The male value is not used for ME1 talk files.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public int FindIdByData(string value, bool male = true);
    }
}
