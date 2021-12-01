using System;
using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.PlotDatabase.PlotElements;
using LegendaryExplorerCore.PlotDatabase.Serialization;
using Newtonsoft.Json;

namespace LegendaryExplorerCore.PlotDatabase.Databases
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

        /// <summary>
        /// Assigns a plot element to the given parent, and inserts it into the appropriate lookup table for it's type
        /// </summary>
        /// <param name="element"></param>
        /// <param name="parent"></param>
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

        public int GetNextElementId()
        {
            var keys = GetMasterDictionary().Keys;
            var maxElement = keys.Count == 0 ? Root.ElementId : keys.Max();
            return maxElement + 1;
        }
    }
}