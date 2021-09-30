using System.ComponentModel;
using System.Text;
using LegendaryExplorerCore.Unreal;

namespace LegendaryExplorerCore.Packages
{
    public interface IEntry
    {
        bool EntryHasPendingChanges { get; set; } //used to signal that this entry has uncommited changes
        bool HeaderChanged { get; set; }
        int Index { set; }
        int indexValue { get; set; }
        int UIndex { get; }
        byte[] Header { get; set; }
        IMEPackage FileRef { get; }
        MEGame Game { get; }
        int idxLink { get; set; }
        string ClassName { get; }
        string FullPath { get; }
        string InstancedFullPath { get; }
        NameReference ObjectName { get; set; }
        string ParentFullPath { get; }
        string ParentInstancedFullPath { get; }
        string ParentName { get; }
        byte[] GetHeader(); //returns clone
        bool HasParent { get; }
        IEntry Parent { get; set; }
        IEntry Clone(bool incrementIndex);

        /// <summary>
        /// Gets the top level object by following the idxLink up the chain. Typically this is the file that will contain the export (unless it is a ForcedExport) if it's an import, or the original package before forcing the export into the file.
        /// </summary>
        /// <returns></returns>
        public string GetRootName();
    }
}
