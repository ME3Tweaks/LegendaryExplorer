using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorer.SharedUI;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Textures.Studio;
using PropertyChanged;

namespace LegendaryExplorer.Tools.TextureStudio
{
    /// <summary>
    /// WPF extension for TextureMapMemoryEntry
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public class TextureMapMemoryEntryWPF : TextureMapMemoryEntry
    {
        public TextureMapMemoryEntryWPF(IEntry entry) : base(entry) { }

        public bool IsProgramaticallySelecting;

        public bool IsSelected { get; set; }

        public bool IsExpanded { get; set; }

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
        public override ObservableCollectionExtendedWPF<TextureMapMemoryEntry> Children { get; } = new ObservableCollectionExtendedWPF<TextureMapMemoryEntry>();

    }
}
