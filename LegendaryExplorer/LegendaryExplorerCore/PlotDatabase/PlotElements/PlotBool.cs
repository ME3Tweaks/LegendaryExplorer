using System.Collections.Generic;
using Newtonsoft.Json;

namespace LegendaryExplorerCore.PlotDatabase.PlotElements
{
    public class PlotBool : PlotElement
    {
        [JsonProperty("subtype")]
        public PlotElementType? SubType { get; set; }

        [JsonProperty("gamervariable")]
        public int? GamerVariable { get; set; }

        [JsonProperty("achievementid")]
        public int? AchievementID { get; set; }

        [JsonProperty("galaxyatwar")]
        public int? GalaxyAtWar { get; set; }

        PlotBool()
        {

        }

        public PlotBool(int plotid, int elementid, string label, PlotElementType type, PlotElement parent, List<PlotElement> children = null)
            : base(plotid, elementid, label, type, parent, children) { }
    }
}