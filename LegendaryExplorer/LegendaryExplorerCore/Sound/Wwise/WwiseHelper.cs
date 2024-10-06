using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;

namespace LegendaryExplorerCore.Sound.Wwise
{
    /// <summary>
    /// Contains useful utility methods for Wwise.
    /// </summary>
    public class WwiseHelper
    {
        /// <summary>
        /// Update the DurationMilliseconds property on all WwiseEvents that reference the given WwiseStream
        /// </summary>
        /// <param name="wwiseStreamExport"></param>
        /// <param name="streamLengthInMs">Value to update DurationMilliseconds to</param>
        public static void UpdateReferencedWwiseEventLengths(ExportEntry wwiseStreamExport, float streamLengthInMs)
        {
            // LE2 has the DurationSeconds property but does not appear to be on any events, so we do nothing. I think.
            // We cannot modify ME2 Wwisestreams so we don't include them here

            if (wwiseStreamExport.Game is MEGame.ME3)
            {
                var durationProperty = new FloatProperty(streamLengthInMs, "DurationMilliseconds");

                // Find referenced WwiseEvent exports and update the property
                var referencedExports = wwiseStreamExport.GetEntriesThatReferenceThisOne();
                foreach (var re in referencedExports.Select(e => e.Key)
                                                            .Where(e => e.ClassName == "WwiseEvent")
                                                            .OfType<ExportEntry>())
                {
                    re.WriteProperty(durationProperty);
                }
            }
            // Finding all WwiseEvent references in LE games will return several WwiseExports, some incorrect
            // so we have to look up the WwiseEvent by TLK ID
            else if (wwiseStreamExport.Game is MEGame.LE3)
            {
                var durationProperty = new FloatProperty(streamLengthInMs / 1000, "DurationSeconds");

                var splits = wwiseStreamExport.ObjectName.Name.Split('_', ',');
                int tlkId = 0;
                bool specifyByGender = false;
                bool isFemaleStream = false;
                for (int i = splits.Length - 1; i > 0; i--)
                {
                    //backwards is faster
                    if (int.TryParse(splits[i], out var parsed))
                    {
                        tlkId = parsed;
                        specifyByGender = wwiseStreamExport.ObjectName.Name.Contains("player_", StringComparison.OrdinalIgnoreCase);
                        isFemaleStream = splits[i + 1] == "f";
                        break; // assume first int we find is the tlk id
                    }
                }
                if (tlkId == 0) return;

                var referencedExports = wwiseStreamExport.GetEntriesThatReferenceThisOne()
                    .Select(e => e.Key)
                    .Where(e => e.ClassName == "WwiseEvent")
                    .Where(e =>
                    {
                        if (!e.ObjectName.Name.StartsWith("VO", StringComparison.OrdinalIgnoreCase)) return false;

                        var splits = e.ObjectName.Name.Split("_");
                        if (specifyByGender)
                        {
                            return splits[1] == tlkId.ToString() && (isFemaleStream == (splits[2] == "f"));
                        }
                        else return splits[1] == tlkId.ToString();
                    });
                foreach (var re in referencedExports.OfType<ExportEntry>())
                {
                    re.WriteProperty(durationProperty);
                }
            }
        }
    }
}
