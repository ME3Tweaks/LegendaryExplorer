using System.ComponentModel;
using System.Diagnostics;
using LegendaryExplorerCore.Unreal;

namespace LegendaryExplorerCore.Packages
{
    public interface IEntry : INotifyPropertyChanged
    {
        bool EntryHasPendingChanges { get; set; } //used to signal that this entry has uncommited changes
        bool HeaderChanged { get; set; }
        int indexValue { get; set; }
        int UIndex { get; }
        /// <summary>
        /// Get generates the header, Set deserializes all the header values from the provided byte array
        /// </summary>
        byte[] Header { get; set; }
        byte[] GenerateHeader();
        void SetHeaderValuesFromByteArray(byte[] value);
        IMEPackage FileRef { get; }
        MEGame Game { get; }
        int idxLink { get; set; }
        string ClassName { get; }
        string FullPath { get; }

        /// <summary>
        /// InstancedFullPath - Dot separated object hierarchy, within the local package file. Does not take exports lacking ForcedExport into account.
        /// </summary>
        string InstancedFullPath { get; }

        /// <summary>
        /// MemoryFullPath - The path as the object will appear in game-memory, taking ForceExport into account.
        /// </summary>
        string MemoryFullPath { get; }
        string ObjectNameString { get; set; }
        NameReference ObjectName { get; set; }
        string ParentFullPath { get; }
        string ParentInstancedFullPath { get; }
        string ParentName { get; }
        bool HasParent { get; }
        bool IsClass { get; }
        IEntry Parent { get; set; }
        IEntry Clone(bool incrementIndex, int newParentUIndex);

        /// <summary>
        /// Gets the name of the top level object by following the idxLink up the chain. Typically this is the file that will contain the export (unless it is a ForcedExport) if it's an import, or the original package before forcing the export into the file.
        /// </summary>
        /// <returns></returns>
        public string GetRootName();

        /// <summary>
        /// Gets the top level object by following the idxLink up the chain. Typically this is the file that will contain the export (unless it is a ForcedExport) if it's an import, or the original package before forcing the export into the file.
        /// </summary>
        /// <returns></returns>
        public IEntry GetRoot();

        /// <summary>
        /// Get package file this entry will be nested under in memory. Parent chain should be preferably exports
        /// </summary>
        /// <returns></returns>
        public string GetLinker();

        /// <summary>
        /// Internal method takes argument that we don't want exposed to external API
        /// </summary>
        /// <param name="entry">Entry to get linker for; will go up the chain.</param>
        /// <returns>Package this entry will be nested under in memory</returns>
        internal static string GetLinker(IEntry entry)
        {
            if (entry.Parent == null)
            {
                if (entry.ClassName == "Package")
                {
                    if (entry is ExportEntry exp && exp.IsForcedExport)
                        return entry.InstancedFullPath; // SFXGameContent
                    if (entry is ImportEntry imp)
                        return entry.InstancedFullPath; // Edge case; this should not occur after resynthesis of a package
                }

#if DEBUG
                if (entry is ImportEntry)
                {
                    Debug.WriteLine("THIS IS AN INVALID IMPORT - ROOT IMPORTS MUST BE FOR PACKAGES!");
                    Debugger.Break();
                }
#endif
                // It's part of the package file itself.
                return entry.FileRef.FileNameNoExtension;
            }

            return GetLinker(entry.Parent);
        }
    }
}
