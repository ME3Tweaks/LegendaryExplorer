using System;
using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.PlotDatabase.Databases;
using LegendaryExplorerCore.PlotDatabase.PlotElements;
using Newtonsoft.Json;

namespace LegendaryExplorerCore.PlotDatabase.Serialization
{
    /// <summary>
    /// A class representing the JSON serialized plot database file
    /// </summary>
    public class SerializedPlotDatabase
    {
        // TODO: Change the JSON serialization to dictionary
        [JsonProperty("bools")] public List<PlotBool> Bools = new();

        [JsonProperty("ints")] public List<PlotElement> Ints = new();

        [JsonProperty("floats")] public List<PlotElement> Floats = new();

        [JsonProperty("conditionals")] public List<PlotConditional> Conditionals = new();

        [JsonProperty("transitions")] public List<PlotTransition> Transitions = new();

        [JsonProperty("organizational")] public List<PlotElement> Organizational = new();

        public SerializedPlotDatabase()
        {
        }

        public SerializedPlotDatabase(PlotDatabaseBase plotDatabase)
        {
            Bools = plotDatabase.Bools.Values.ToList();
            Ints = plotDatabase.Ints.Values.ToList();
            Floats = plotDatabase.Floats.Values.ToList();
            Conditionals = plotDatabase.Conditionals.Values.ToList();
            Transitions = plotDatabase.Transitions.Values.ToList();
            Organizational = plotDatabase.Organizational.Values.ToList();
        }

        /// <summary>
        /// Builds the Parent and Child List relationships between all plot elements.
        /// Needs to be run when database gets initialized.
        /// </summary>
        public void BuildTree()
        {
            Dictionary<int, PlotElement> table = GetMasterPlotDictionary();

            foreach (var element in table)
            {
                var plot = element.Value;
                var parentId = plot.ParentElementId;
                if (parentId > 0)
                {
                    if (table.TryGetValue(parentId, out var parent))
                    {
                        plot.AssignParent(parent);
                    }
                    else if (element.Value is not PlotModElement)
                    {
                        throw new Exception("Cannot assign parent");
                    }
                }
            }
        }

        protected virtual Dictionary<int, PlotElement> GetMasterPlotDictionary()
        {
            return Bools.Concat<PlotElement>(Ints)
                .Concat(Floats).Concat(Conditionals)
                .Concat(Transitions).Concat(Organizational)
                .ToDictionary((e) => e.ElementId);
        }
    }
}