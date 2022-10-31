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

        public void OnIsExpandedChanged()
        {
            Debug.WriteLine($"IsExpanded: {IsExpanded} {InstancedFullPath}");
        }

        public void OnIsSelectedChanged()
        {
            Debug.WriteLine($"IsSelected: {IsSelected} {InstancedFullPath}");
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
