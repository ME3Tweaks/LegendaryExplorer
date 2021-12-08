using System.Collections.Generic;
using Newtonsoft.Json;

namespace LegendaryExplorerCore.PlotDatabase.PlotElements
{
    public class PlotConditional : PlotElement
    {
        /// <summary>The code of this conditional</summary>
        [JsonProperty("code")]
        public string Code { get; set; }

        /// <inheritdoc />
        PlotConditional()
        {

        }

        /// <inheritdoc />
        public PlotConditional(int plotid, int elementid, string label, PlotElementType type, PlotElement parent,
            List<PlotElement> children = null)
            : base(plotid, elementid, label, type, parent, children)
        {
            Type = PlotElementType.Conditional;
        }
    }
}