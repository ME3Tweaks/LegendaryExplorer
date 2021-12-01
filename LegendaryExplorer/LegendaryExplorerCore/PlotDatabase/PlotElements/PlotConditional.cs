using System.Collections.Generic;
using Newtonsoft.Json;

namespace LegendaryExplorerCore.PlotDatabase.PlotElements
{
    public class PlotConditional : PlotElement
    {
        [JsonProperty("code")]
        public string Code { get; set; }

        PlotConditional()
        {

        }

        public PlotConditional(int plotid, int elementid, string label, PlotElementType type, PlotElement parent,
            List<PlotElement> children = null)
            : base(plotid, elementid, label, type, parent, children)
        {
            Type = PlotElementType.Conditional;
        }
    }
}