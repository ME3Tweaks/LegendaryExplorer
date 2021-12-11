using System.Collections.Generic;
using LegendaryExplorerCore.Packages;
using Newtonsoft.Json;

namespace LegendaryExplorerCore.PlotDatabase.PlotElements
{
    /// <summary>
    /// Element representing a mod in a Plot Database
    /// </summary>
    public class PlotModElement : PlotElement
    {
        /// <summary>The name of the mod this element represents</summary>
        [JsonProperty("modtitle")]
        public string ModTitle { get; set; }

        /// <summary>The author of the mod this element represents</summary>
        [JsonProperty("modauthor")]
        public string ModAuthor { get; set; }

        /// <summary>A description of the mod this element represents</summary>
        [JsonProperty("description")]
        public string Description { get; set; }

        /// <summary>The version of the mod that this database is built for</summary>
        [JsonProperty("version")]
        public string Version { get; set; }

        /// <summary>The game that the mod this element represents is for</summary>
        [JsonProperty("game")]
        public MEGame Game { get; set; }

        /// <summary>
        /// Initializes a new <see cref="PlotMod"/>
        /// </summary>
        PlotModElement()
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
        public PlotModElement(int plotid, int elementid, string label, PlotElementType type, PlotElement parent,
            List<PlotElement> children = null)
            : base(plotid, elementid, label, type, parent, children)
        {
            Type = PlotElementType.Mod;
        }
    }
}