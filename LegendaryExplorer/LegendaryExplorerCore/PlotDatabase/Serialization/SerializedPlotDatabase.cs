using System;
using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.PlotDatabase.Databases;
using LegendaryExplorerCore.PlotDatabase.PlotElements;
using Newtonsoft.Json;

namespace LegendaryExplorerCore.PlotDatabase.Serialization
{
    /// <summary>
    /// Represents a JSON serialized plot database file
    /// </summary>
    public class SerializedPlotDatabase
    {
        // TODO: Change the JSON serialization to dictionary
        /// <summary>A list of plot bool elements in this database</summary>
        [JsonProperty("bools")] public List<PlotBool> Bools = new();

        /// <summary>A list of plot int elements in this database</summary>
        [JsonProperty("ints")] public List<PlotElement> Ints = new();

        /// <summary>A list of plot float elements in this database</summary>
        [JsonProperty("floats")] public List<PlotElement> Floats = new();

        /// <summary>A list of plot conditional elements in this database</summary>
        [JsonProperty("conditionals")] public List<PlotConditional> Conditionals = new();

        /// <summary>A list of plot transition elements in this database</summary>
        [JsonProperty("transitions")] public List<PlotTransition> Transitions = new();

        /// <summary>A list of all non-game-state and journal elements in this database</summary>
        [JsonProperty("organizational")] public List<PlotElement> Organizational = new();

        /// <summary>
        /// Initializes a new serializable database
        /// </summary>
        public SerializedPlotDatabase()
        {
        }

        /// <summary>
        /// Initializes a new serializable database, importing all elements from an input <see cref="PlotDataBaseBase"/>
        /// </summary>
        /// <param name="plotDatabase">Database to import plot elements from</param>
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

        /// <summary>
        /// Creates a dictionary of all PlotElements in this database by ElementId
        /// </summary>
        /// <returns>Dictionary of PlotElements, with ElementId as keys</returns>
        protected virtual Dictionary<int, PlotElement> GetMasterPlotDictionary()
        {
            return Bools.Concat(Ints)
                .Concat(Floats).Concat(Conditionals)
                .Concat(Transitions).Concat(Organizational)
                .ToDictionary((e) => e.ElementId);
        }
    }
}