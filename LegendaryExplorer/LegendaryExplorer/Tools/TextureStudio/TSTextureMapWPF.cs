using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorer.SharedUI;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Textures.Studio;

namespace LegendaryExplorer.Tools.TextureStudio
{
    /// <summary>
    /// WPF extension for TextureMapMemoryEntry
    /// </summary>
    public class TextureMapMemoryEntryWPF : TextureMapMemoryEntry, INotifyPropertyChanged
    {
        public TextureMapMemoryEntryWPF(IEntry entry) : base(entry) { }

        public TextureMapMemoryEntryWPF ParentWPF => (TextureMapMemoryEntryWPF) Parent;

        public bool IsProgramaticallySelecting;

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        private bool _isFilteredOut;
        /// <summary>
        /// If this node is hidden by the tree search filter
        /// </summary>
        public bool IsFilteredOut
        {
            get => _isFilteredOut;
            set => SetProperty(ref _isFilteredOut, value);
        }

        /// <summary>
        /// Filters this node and its children by object name. A node stays visible if it matches, one of its parents matches, or one of its children matches.
        /// </summary>
        /// <param name="searchText">Text to search for. Null or empty shows everything.</param>
        /// <param name="parentMatched">If a parent of this node matched the search</param>
        /// <returns>True if this node is visible</returns>
        public bool ApplyFilter(string searchText, bool parentMatched = false)
        {
            bool showAll = parentMatched || string.IsNullOrEmpty(searchText);
            bool selfMatched = showAll || ObjectName.Contains(searchText, StringComparison.OrdinalIgnoreCase);

            bool childMatched = false;
            foreach (var child in Children.OfType<TextureMapMemoryEntryWPF>())
            {
                // Must visit every child so each one updates its own visibility
                childMatched |= child.ApplyFilter(searchText, selfMatched);
            }

            if (!selfMatched && childMatched)
            {
                // Only a descendant matched, so open this node to reveal it
                IsExpanded = true;
            }

            IsFilteredOut = !(selfMatched || childMatched);
            return !IsFilteredOut;
        }

        public void OnIsExpandedChanged()
        {
            Debug.WriteLine($"IsExpanded: {IsExpanded} {MemoryFullPath}");
        }

        public void OnIsSelectedChanged()
        {
            Debug.WriteLine($"IsSelected: {IsSelected} {MemoryFullPath}");
        }

        // Todo: Add cubemap icon
        public string IconSource => IsTexture ? @"/PackageEditor/EntryIcons/icon_package.png" : @"/PackageEditor/EntryIcons/icon_texture2d.png";

        public void ExpandParents()
        {
            if (Parent is TextureMapMemoryEntryWPF tmmew)
            {
                tmmew.ExpandParents();
                tmmew.IsExpanded = true;
            }
        }

        public override ObservableCollectionExtended<TextureMapPackageEntry> Instances { get; } = new();

        /// <summary>
        /// List of direct children to this memory entry. The collection type WPF allows adding from other threads
        /// </summary>
        public override ObservableCollectionExtendedWPF<TextureMapMemoryEntry> Children { get; } = new();

        private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
