using System.Collections.Generic;
using Newtonsoft.Json;

namespace LegendaryExplorerCore.PlotDatabase.PlotElements
{
    /// <summary>
    /// Represents a boolean plot variable element in a Plot Database
    /// </summary>
    public class PlotBool : PlotElement
    {
        /// <summary>The bool subtype of this element</summary>
        [JsonProperty("subtype")]
        public PlotElementType? SubType { get; set; }

        /// <summary>Unknown</summary>
        [JsonProperty("gamervariable")]
        public int? GamerVariable { get; set; }

        /// <summary>The achievement id that is unlocked with this bool, if any</summary>
        [JsonProperty("achievementid")]
        public int? AchievementID { get; set; }

        /// <summary>The GalaxyAtWar id that is associated with this bool, if any</summary>
        [JsonProperty("galaxyatwar")]
        public int? GalaxyAtWar { get; set; }

        /// <inheritdoc />
        PlotBool()
        {

        }

        /// <inheritdoc />
        public PlotBool(int plotid, int elementid, string label, PlotElementType type, PlotElement parent, List<PlotElement> children = null)
            : base(plotid, elementid, label, type, parent, children) { }
    }
}