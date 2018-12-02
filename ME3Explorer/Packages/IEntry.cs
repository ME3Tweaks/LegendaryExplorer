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
        int UIndex { get; }
        byte[] Header { get; set; }
        IMEPackage FileRef { get; }
        int idxLink { get; set; }
        int idxObjectName { get; set; }
        string ClassName { get; }
        string GetFullPath { get; }
        string ObjectName { get; }
        string PackageFullName { get; }
        string PackageName { get; }
        byte[] GetHeader(); //returns clone
    }

    public interface IExportEntry : IEntry
    {
        bool DataChanged { get; set; }
        /// <summary>
        /// RETURNS A CLONE
        /// </summary>
        byte[] Data { get; set; }
        int DataOffset { get; set; }
        int DataSize { get; set; }
        int idxArchtype { get; set; }
        int idxClass { get; set; }
        int idxClassParent { get; set; }
        int indexValue { get; set; }
        string ArchtypeName { get; }
        string ClassParent { get; }
        uint HeaderOffset { get; set; }
        ulong ObjectFlags { get; set; }
        int OriginalDataSize { get; }
        bool ReadsFromConfig { get; }

        IExportEntry Clone();


        PropertyCollection GetProperties(bool forceReload = false, bool includeNoneProperties = false);
        void WriteProperties(PropertyCollection props);
        int propsEnd();
        int GetPropertyStart();
        byte[] getBinaryData();
        void setBinaryData(byte[] binaryData);
        T GetProperty<T>(string name) where T : UProperty;
        void WriteProperty(UProperty prop);
    }
}
