using System.Collections.Generic;
using LegendaryExplorerCore.Packages;
using Newtonsoft.Json;

namespace LegendaryExplorerCore.PlotDatabase.PlotElements
{
    public class PlotModElement : PlotElement
    {
        [JsonProperty("modtitle")]
        public string ModTitle { get; set; }

        [JsonProperty("modauthor")]
        public string ModAuthor { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("game")]
        public MEGame Game { get; set; }

        PlotModElement()
        {

        }

        public PlotModElement(int plotid, int elementid, string label, PlotElementType type, PlotElement parent, List<PlotElement> children = null)
            : base(plotid, elementid, label, type, parent, children) { }
    }
}