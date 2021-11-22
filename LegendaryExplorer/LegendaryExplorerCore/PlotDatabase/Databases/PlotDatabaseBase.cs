using System;
using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.Packages;
using Newtonsoft.Json;

namespace LegendaryExplorerCore.PlotDatabase
{
    public abstract class PlotDatabaseBase
    {
        public virtual PlotElement Root { get; protected set; }
        public Dictionary<int, PlotBool> Bools { get; set; } = new Dictionary<int, PlotBool>();
        public Dictionary<int, PlotElement> Ints { get; set; } = new Dictionary<int, PlotElement>();
        public Dictionary<int, PlotElement> Floats { get; set; } = new Dictionary<int, PlotElement>();
        public Dictionary<int, PlotConditional> Conditionals { get; set; } = new Dictionary<int, PlotConditional>();
        public Dictionary<int, PlotTransition> Transitions { get; set; } = new Dictionary<int, PlotTransition>();
        public Dictionary<int, PlotElement> Organizational { get; set; } = new Dictionary<int, PlotElement>();

        public MEGame Game { get; set; }

        public abstract bool IsBioware { get; }

        public bool CanSave() => !IsBioware;

        internal static JsonSerializerSettings _jsonSerializerSettings = new JsonSerializerSettings
            {NullValueHandling = NullValueHandling.Ignore};

        public PlotDatabaseBase(MEGame refgame, bool isbioware)
        {
            this.Game = refgame;
        }

        public PlotDatabaseBase()
        {
        }

        internal void ImportPlots(SerializedPlotDatabase pdb)
        {
            if (pdb == null) throw new Exception("Plot Database was null.");

            Bools = pdb.Bools.ToDictionary((b) => b.PlotId);
            Ints = pdb.Ints.ToDictionary((b) => b.PlotId);
            Floats = pdb.Floats.ToDictionary((b) => b.PlotId);
            Conditionals = pdb.Conditionals.ToDictionary((b) => b.PlotId);
            Transitions = pdb.Transitions.ToDictionary((b) => b.PlotId);
            Organizational = pdb.Organizational.ToDictionary((b) => b.ElementId);
        }

        /// <summary>
        /// Turns the plot database into a single dictionary, with the key being ElementID
        /// </summary>
        /// <returns></returns>
        public SortedDictionary<int, PlotElement> GetMasterDictionary()
        {
            try
            {
                var elements = Bools.Values.ToList()
                    .Concat(Ints.Values.ToList())
                    .Concat(Floats.Values.ToList())
                    .Concat(Conditionals.Values.ToList())
                    .Concat(Transitions.Values.ToList())
                    .Concat(Organizational.Values.ToList())
                    .ToDictionary(e => e.ElementId);

                return new SortedDictionary<int, PlotElement>(elements);
            }
            catch //fallback in case saved dictionary has duplicate element ids
            {
                return new SortedDictionary<int, PlotElement>();
            }
        }

        /// <summary>
        /// Get an element from by it's Element ID
        /// </summary>
        /// <param name="elementId"></param>
        /// <returns></returns>
        public PlotElement GetElementById(int elementId)
        {
            var sorted = GetMasterDictionary();
            if (sorted.ContainsKey(elementId)) return sorted[elementId];

            return null;
        }

        public int GetNextElementId()
        {
            var maxElement = GetMasterDictionary().Keys.Max();
            return maxElement + 1;
        }
    }
}