using System.IO;
using ME3ExplorerCore.Packages;

namespace LegendaryExplorer.SharedUI.Interfaces
{
    public interface ITreeItem
    {
        bool IsSelected { get; set; }
        bool IsExpanded { get; set; }
        void PrintPretty(string indent, TextWriter str, bool last, ExportEntry associatedExport);
    }
}
