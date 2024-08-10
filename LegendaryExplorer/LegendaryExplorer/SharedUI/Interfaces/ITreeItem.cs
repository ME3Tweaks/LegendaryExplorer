using System.IO;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorer.SharedUI.Interfaces
{
    public interface ITreeItem
    {
        bool IsSelected { get; set; }
        bool IsExpanded { get; set; }
        public ITreeItem Parent { get; set; }
        void PrintPretty(string indent, TextWriter str, bool last, ExportEntry associatedExport);
    }
}
