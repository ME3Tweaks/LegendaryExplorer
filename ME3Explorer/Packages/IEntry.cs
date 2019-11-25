using System.ComponentModel;
using System.Diagnostics;
using ME3Explorer.Unreal;

namespace ME3Explorer.Packages
{
    public interface IEntry
    {
        event PropertyChangedEventHandler PropertyChanged;
        bool EntryHasPendingChanges { get; set; } //used to signal that this entry has uncommited changes
        bool HeaderChanged { get; set; }
        int Index { get; set; }
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
        IEntry Clone();
    }
}
