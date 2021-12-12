using System.Collections.Generic;
using Newtonsoft.Json;

namespace LegendaryExplorerCore.PlotDatabase.PlotElements
{
    /// <summary>
    /// Represents a plot transition element in a Plot Database
    /// </summary>
    public class PlotTransition : PlotElement
    {
        /// <summary>An argument to this transition</summary>
        [JsonProperty("argument")]
        public string Argument { get; set; }

        /// <summary>
        /// Initializes a new <see cref="PlotTransition"/>
        /// </summary>
        PlotTransition()
        { }

        /// <summary>
        /// Initializes a new PlotElement
        /// </summary>
        /// <param name="plotid">PlotID of new element</param>
        /// <param name="elementid">ElementID of new element</param>
        /// <param name="label">Label of new element</param>
        /// <param name="type">Type of new element</param>
        /// <param name="parent">Parent PlotElement of new element</param>
        /// <param name="children">Children of new element</param>
        public PlotTransition(int plotid, int elementid, string label, PlotElementType type, PlotElement parent,
            List<PlotElement> children = null)
            : base(plotid, elementid, label, type, parent, children)
        {
            Type = PlotElementType.Transition;
        }
    }
}