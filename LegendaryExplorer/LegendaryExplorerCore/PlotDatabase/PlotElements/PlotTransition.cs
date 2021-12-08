using System.Collections.Generic;
using Newtonsoft.Json;

namespace LegendaryExplorerCore.PlotDatabase.PlotElements
{
    public class PlotTransition : PlotElement
    {
        /// <summary>An argument to this transition</summary>
        [JsonProperty("argument")]
        public string Argument { get; set; }

        /// <inheritdoc />
        PlotTransition()
        {

        }

        /// <inheritdoc />
        public PlotTransition(int plotid, int elementid, string label, PlotElementType type, PlotElement parent,
            List<PlotElement> children = null)
            : base(plotid, elementid, label, type, parent, children)
        {
            Type = PlotElementType.Transition;
        }
    }
}