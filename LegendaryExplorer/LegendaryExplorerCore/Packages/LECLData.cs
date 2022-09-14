using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace LegendaryExplorerCore.Packages
{
    /// <summary>
    /// Data that is appended to a saved package file
    /// </summary>
    public class LECLData
    {
        /// <summary>
        /// If this package was saved with LEC
        /// </summary>
        [JsonIgnore]
        public bool WasSavedWithLEC { get; set; }

        /// <summary>
        /// If this package had the ThisIsMemEndOfFile tag on it - it was used in a texture modded package
        /// </summary>
        [JsonIgnore]
        public bool WasSavedWithMEM { get; set; }

        /// <summary>
        /// Filenames that this package should be able to safely import from
        /// </summary>
        [JsonProperty("importhintfiles")] // DO NOT CHANGE
        public List<string> ImportHintFiles { get; } = new(0);

        /// <summary>
        /// If this file is marked as only being used post save file load (e.g. after BIO_COMMON for example). This allows it to access more files for importing.
        /// </summary>
        [JsonProperty("ispostload")] // DO NOT CHANGE
        public bool IsPostLoadFile { get; set; }

        /// <summary>
        /// Data that is not known by this LECLData class - it may mean that this package was saved with
        /// a newer version of LEC that had more fields.
        /// </summary>
        [JsonExtensionData]
        public IDictionary<string, JToken> UnknownData;

        /// <summary>
        /// If this object has any data that should be serialized.
        /// </summary>
        /// <returns>True if something could be written, false otherwise</returns>
        internal bool HasAnyData()
        {
            if (UnknownData is { Count: > 0 }) return true;

            // IF ADDING DATA TO THIS CLASS ENSURE YOU ADD IT BELOW OR IT WILL NOT SERIALIZE IN SOME CASES
            if (ImportHintFiles.Count > 0) return true;
            if (IsPostLoadFile) return true;

            return false;
        }
    }
}
