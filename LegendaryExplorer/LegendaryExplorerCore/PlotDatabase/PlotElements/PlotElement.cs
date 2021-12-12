using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using Newtonsoft.Json;

namespace LegendaryExplorerCore.PlotDatabase.PlotElements
{
    /// <summary>
    /// Base class representing an element in a plot database. Can be serialized to JSON
    /// </summary>
    /// <remarks>
    /// This class does not affect or represent anything in-game. Instances of this class are intended as supplementary reference material
    /// for in-game plot variables. The existence of a plot element in a database does not ensure the existence of anything in-game.
    /// </remarks>
    [DebuggerDisplay("{Type} {PlotId}: {Path}")]
    public class PlotElement : INotifyPropertyChanged
    {
        /// <summary>The in-game id for this element, -1 if not applicable</summary>
        [JsonProperty("plotid")]
        public int PlotId { get; set; }

        /// <summary>The database id for this element</summary>
        /// <remarks>ElementIds should be unique within a database</remarks>
        [JsonProperty("elementid")]
        public int ElementId { get; set; }

        /// <summary>The element id of this element's parent</summary>
        /// <remarks>This property is used during deserialization to determine the hierarchical structure of plot elements.
        /// It should always match the id of the <see cref="Parent"/> element. <see cref="AssignParent"/> should always be used to modify hierarchical plot relationships. </remarks>
        [JsonProperty("parentelementid")]
        public int ParentElementId { get; set; }

        /// <summary>The name or label associated with this element</summary>
        [JsonProperty("label")]
        public string Label { get; set; }

        /// <summary>A value that likely associates this element to something in an internal BioWare system</summary>
        [JsonProperty("sequence")]
        public float Sequence { get; set; }

        /// <summary>The type of data this element represents</summary>
        [JsonProperty("type")]
        public PlotElementType Type { get; set; }

        /// <summary>Gets the <see cref="PlotElement"/> that is this element's parent</summary>
        /// <remarks>Use <see cref="AssignParent"/> to set parent. This property is not used during serialization.</remarks>
        [JsonIgnore]
        public PlotElement Parent { get; private set; }

        /// <summary>A bindable collection of this element's children</summary>
        /// <remarks>Use <see cref="AssignParent"/> on children elements to set children. This property is not used during serialization.</remarks>
        [JsonIgnore] public ObservableCollectionExtended<PlotElement> Children { get; } = new ();

        /// <summary>Gets a string displaying the full path through the tree to this element</summary>
        /// <example><c>LE3.Global.Game_Progress.Gth002_Completed</c></example>
        [JsonIgnore]
        public string Path
        {
            get
            {
                var path = new StringBuilder();
                PlotElement el = this;
                path.Insert(0, el.Label);
                while (el.ParentElementId > 0 && el.Parent != null)
                {
                    el = el.Parent;
                    path.Insert(0, ".");
                    path.Insert(0, el.Label);
                }
                return path.ToString();
            }
        }

        /// <summary>Gets the most relevant id based on type, either <see cref="PlotId"/> for in-game elements or <see cref="ElementId"/></summary>
        [JsonIgnore]
        public int RelevantId
        {
            get
            {
                switch (Type)
                {
                    case PlotElementType.Conditional:
                    case PlotElementType.Consequence:
                    case PlotElementType.Flag:
                    case PlotElementType.Float:
                    case PlotElementType.Integer:
                    case PlotElementType.State:
                    case PlotElementType.SubState:
                    case PlotElementType.Transition:
                        return PlotId;
                    default:
                        return ElementId;
                }
            }
        }

        /// <summary>Gets whether this element represents something in-game, based on element type</summary>
        /// <remarks>No plot element in the database ever affects anything in game. This property merely reflects if there
        /// is something in the game files that this element type should represent. An element existing in the database does not ensure
        /// a corresponding in-game representation.</remarks>
        [JsonIgnore]
        public bool IsAGameState
        {
            get
            {
                switch (Type)
                {
                    case PlotElementType.Conditional:
                    case PlotElementType.Consequence:
                    case PlotElementType.Flag:
                    case PlotElementType.Float:
                    case PlotElementType.Integer:
                    case PlotElementType.JournalGoal:
                    case PlotElementType.JournalItem:
                    case PlotElementType.JournalTask:
                    case PlotElementType.State:
                    case PlotElementType.SubState:
                    case PlotElementType.Transition:
                        return true;
                    default:
                        return false;
                }
            }
        }

        /// <summary>
        /// Initializes a new <see cref="PlotElement"/>
        /// </summary>
        public PlotElement()
        { }

        /// <summary>
        /// Initializes a new PlotElement
        /// </summary>
        /// <param name="plotid">PlotID of new element</param>
        /// <param name="elementid">ElementID of new element</param>
        /// <param name="label">Label of new element</param>
        /// <param name="type">Type of new element</param>
        /// <param name="parentelementId">ParentElementID of new element</param>
        /// <param name="children">Children of new element</param>
        public PlotElement(int plotid, int elementid, string label, PlotElementType type, int parentelementId, List<PlotElement> children = null)
        {
            PlotId = plotid;
            ElementId = elementid;
            Label = label;
            Type = type;
            ParentElementId = parentelementId;
            Children.AddRange(children ?? new List<PlotElement>());
        }

        /// <summary>
        /// Initializes a new PlotElement
        /// </summary>
        /// <param name="plotid">PlotID of new element</param>
        /// <param name="elementid">ElementID of new element</param>
        /// <param name="label">Label of new element</param>
        /// <param name="type">Type of new element</param>
        /// <param name="parent">Parent PlotElement of new element</param>
        /// <param name="children">Children of new element</param>
        public PlotElement(int plotid, int elementid, string label, PlotElementType type, PlotElement parent, List<PlotElement> children = null)
            : this(plotid, elementid, label, type, -1, children)
        {
            AssignParent(parent);
        }

        /// <summary>
        /// Assigns this element to a new parent.
        /// Removes it from an existing parent and handles <see cref="ParentElementId"/> and <see cref="Children"/> arrays as needed
        /// </summary>
        /// <param name="parent">New parent plot element</param>
        public void AssignParent(PlotElement parent)
        {
            if (Parent != parent && Parent != null)
            {
                RemoveFromParent();
            }
            Parent = parent;
            ParentElementId = parent?.ElementId ?? -1;
            if (Parent != null && !Parent.Children.Contains(this))
            {
                parent?.Children.Add(this);
            }
        }

        /// <summary>
        /// Removes the parent for this element
        /// </summary>
        /// <returns><c>true</c> if a parent was removed, <c>false</c> otherwise</returns>
        public bool RemoveFromParent()
        {
            if (Parent != null)
            {
                Parent.Children.RemoveAll((i) => i == this);
                Parent = null;
                ParentElementId = -1;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Sets the <see cref="ElementId"/>, handling <see cref="ParentElementId"/> of any children
        /// </summary>
        /// <param name="id">New element id</param>
        public void SetElementId(int id)
        {
            this.ElementId = id;
            foreach (var c in Children)
            {
                c.ParentElementId = id;
            }
        }

#pragma warning disable
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore

    }
}
