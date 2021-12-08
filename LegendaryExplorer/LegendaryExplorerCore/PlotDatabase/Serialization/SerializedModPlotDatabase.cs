using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.PlotDatabase.Databases;
using LegendaryExplorerCore.PlotDatabase.PlotElements;
using Newtonsoft.Json;

namespace LegendaryExplorerCore.PlotDatabase.Serialization
{
    /// <summary>
    /// Represents a JSON serialized plot database for a single mod
    /// </summary>
    public class SerializedModPlotDatabase : SerializedPlotDatabase
    {
        /// <summary>The ModPlotElement root of this database</summary>
        [JsonProperty("modroot")]
        public PlotModElement ModRoot { get; set; }

        /// <inheritdoc/>
        public SerializedModPlotDatabase() { }

        /// <inheritdoc/>
        public SerializedModPlotDatabase(ModPlotDatabase plotDatabase) : base(plotDatabase)
        {
            ModRoot = plotDatabase.ModRoot;
        }

        /// <inheritdoc/>
        protected override Dictionary<int, PlotElement> GetMasterPlotDictionary()
        {
            return Bools.Concat(Ints)
                .Concat(Floats).Concat(Conditionals)
                .Concat(Transitions).Concat(Organizational).Append(ModRoot)
                .ToDictionary((e) => e.ElementId);
        }
    }
}