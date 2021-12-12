using System;
using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.PlotDatabase.PlotElements;
using LegendaryExplorerCore.PlotDatabase.Serialization;
using Newtonsoft.Json;

namespace LegendaryExplorerCore.PlotDatabase.Databases
{
    /// <summary>
    /// Abstract base class representing a database of game plot information
    /// </summary>
    public abstract class PlotDatabaseBase
    {
        /// <summary>Gets the root element of the plot tree</summary>
        public virtual PlotElement Root { get; protected set; }
        /// <summary>A table of boolean plot variables, ordered by in-game variable id</summary>
        public Dictionary<int, PlotBool> Bools { get; set; } = new Dictionary<int, PlotBool>();
        /// <summary>A table of integer plot variables, ordered by in-game variable id</summary>
        public Dictionary<int, PlotElement> Ints { get; set; } = new Dictionary<int, PlotElement>();
        /// <summary>A table of float plot variables, ordered by in-game variable id</summary>
        public Dictionary<int, PlotElement> Floats { get; set; } = new Dictionary<int, PlotElement>();
        /// <summary>A table of conditional functions, ordered by in-game conditional id</summary>
        public Dictionary<int, PlotConditional> Conditionals { get; set; } = new Dictionary<int, PlotConditional>();
        /// <summary>A table of plot transitions, ordered by in-game transition id</summary>
        public Dictionary<int, PlotTransition> Transitions { get; set; } = new Dictionary<int, PlotTransition>();
        /// <summary>A table of organizational plot elements, ordered by plot element id</summary>
        public Dictionary<int, PlotElement> Organizational { get; set; } = new Dictionary<int, PlotElement>();

        /// <summary>Gets or sets the MEGame associated with this database</summary>
        public MEGame Game { get; set; }

        /// <summary>Gets a value indicating whether this database was created from the BioWare plot databases</summary>
        public abstract bool IsBioware { get; }

        /// <summary>Indicates whether this database should be allowed to be saved to disk</summary>
        /// <returns><c>true</c> if database can save</returns>
        public bool CanSave() => !IsBioware;

        internal static JsonSerializerSettings _jsonSerializerSettings = new JsonSerializerSettings
            {NullValueHandling = NullValueHandling.Ignore};

        /// <summary>
        /// Copies all tables from the input database to this one, overwriting existing tables
        /// </summary>
        /// <param name="pdb">Database to import from</param>
        /// <exception cref="ArgumentNullException"><paramref name="pdb"/> is null</exception>
        internal void ImportPlots(SerializedPlotDatabase pdb)
        {
            if (pdb == null) throw new ArgumentNullException(nameof(pdb), "Plot Database was null");

            Bools = pdb.Bools.ToDictionary((b) => b.PlotId);
            Ints = pdb.Ints.ToDictionary((b) => b.PlotId);
            Floats = pdb.Floats.ToDictionary((b) => b.PlotId);
            Conditionals = pdb.Conditionals.ToDictionary((b) => b.PlotId);
            Transitions = pdb.Transitions.ToDictionary((b) => b.PlotId);
            Organizational = pdb.Organizational.ToDictionary((b) => b.ElementId);
        }

        /// <summary>
        /// Creates a dictionary of all plot elements in the database, with the key being the element ID
        /// </summary>
        /// <returns>A dictionary of all plot elements</returns>
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

        /// <summary>
        /// Inserts a plot element into the appropriate lookup table for it's type, and assigns it the given parent
        /// </summary>
        /// <param name="element">Element to add</param>
        /// <param name="parent">Parent to assign element to, can be null if element already has parent</param>
        public void AddElement(PlotElement element, PlotElement parent)
        {
            // This method does not check if the parent is already in the database!
            if (parent != null)
            {
                element.AssignParent(parent);
            }
            else if (element.Parent is null)
                throw new Exception("Element must already have parent, or parent element must be supplied!");

            switch (element.Type)
            {
                case PlotElementType.Integer:
                    Ints.Add(element.PlotId, element);
                    return;
                case PlotElementType.Float:
                    Floats.Add(element.PlotId, element);
                    return;
                case PlotElementType.Conditional when element is PlotConditional pc:
                    Conditionals.Add(pc.PlotId, pc);
                    return;
                case PlotElementType.Transition when element is PlotTransition pe:
                    Transitions.Add(pe.PlotId, pe);
                    return;
            }

            if (element is PlotBool pb)
            {
                Bools.Add(pb.PlotId, pb);
            }
            else
            {
                Organizational.Add(element.ElementId, element);
            }
        }

        /// <summary>
        /// Removes an element from the database, including it's parent and all lookup tables
        /// </summary>
        /// <param name="element">Element to remove</param>
        /// <param name="removeAllChildren">Recursively remove the element's children from the database?</param>
        /// <exception cref="ArgumentException"></exception>
        public void RemoveElement(PlotElement element, bool removeAllChildren = false)
        {
            if (element == Root) throw new ArgumentException("Cannot remove root element from database!");
            if (element.Children.Any())
            {
                if (removeAllChildren)
                {
                    var children = element.Children.ToList();
                    foreach (var child in children)
                    {
                        RemoveElement(child, true);
                    }
                }
                else
                {
                    throw new ArgumentException("Cannot remove an element with children.");
                }
            }

            element.RemoveFromParent();
            switch (element.Type)
            {
                case PlotElementType.Integer:
                    TryRemoveFromDictionary(element, Ints);
                    return;
                case PlotElementType.Float:
                    TryRemoveFromDictionary(element, Floats);
                    return;
                case PlotElementType.Conditional when element is PlotConditional pc:
                    TryRemoveFromDictionary(pc, Conditionals);
                    return;
                case PlotElementType.Transition when element is PlotTransition pe:
                    TryRemoveFromDictionary(pe, Transitions);
                    return;
            }

            if (element is PlotBool pb)
            {
                TryRemoveFromDictionary(pb, Bools);
            }
            else
            {
                TryRemoveFromDictionary(element, Organizational);
            }

            void TryRemoveFromDictionary<T>(T el, Dictionary<int, T> dict) where T : PlotElement
            {
                if (dict.TryGetValue(el.RelevantId, out var value) && el == value)
                {
                    dict.Remove(el.RelevantId);
                }
            }
        }

        /// <summary>
        /// Calculates the highest element id used in the database + 1
        /// </summary>
        /// <returns>The next available element id</returns>
        public int GetNextElementId()
        {
            var keys = GetMasterDictionary().Keys;
            var maxElement = keys.Count == 0 ? Root.ElementId : keys.Max();
            return maxElement + 1;
        }
    }
}