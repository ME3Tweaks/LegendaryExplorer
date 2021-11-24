using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.PlotDatabase.Databases;
using LegendaryExplorerCore.PlotDatabase.PlotElements;
using Newtonsoft.Json;

namespace LegendaryExplorerCore.PlotDatabase.Serialization
{
    public class SerializedModPlotDatabase : SerializedPlotDatabase
    {
        [JsonProperty("modroot")] public PlotModElement ModRoot { get; set; }

        public SerializedModPlotDatabase() { }

        public SerializedModPlotDatabase(ModPlotDatabase plotDatabase) : base(plotDatabase)
        {
            ModRoot = plotDatabase.ModRoot;
        }

        protected override Dictionary<int, PlotElement> GetMasterPlotDictionary()
        {
            return Bools.Concat(Ints)
                .Concat(Floats).Concat(Conditionals)
                .Concat(Transitions).Concat(Organizational).Append(ModRoot)
                .ToDictionary((e) => e.ElementId);
        }
    }
}