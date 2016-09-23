using System.ComponentModel;
using ME3Explorer.Unreal;

namespace ME3Explorer.Packages
{
    public interface IEntry
    {
        bool HeaderChanged { get; }
        int Index { get; set; }
        int UIndex { get; }
        byte[] header { get; }
        IMEPackage FileRef { get; }
        int idxLink { get; set; }
        int idxObjectName { get; set; }
        string ClassName { get; }
        string GetFullPath { get; }
        string ObjectName { get; }
        string PackageFullName { get; }
        string PackageName { get; }
    }

    public interface IImportEntry : IEntry
    {
        int idxClassName { get; set; }
        int idxPackageFile { get; set; }
        string PackageFile { get; }

        IImportEntry Clone();
    }

    public interface IExportEntry : IEntry
    {
        bool DataChanged { get; }
        /// <summary>
        /// RETURNS A CLONE
        /// </summary>
        byte[] Data { get; set; }
        int DataOffset { get; }
        int DataSize { get; }
        int idxArchtype { get; set; }
        int idxClass { get; set; }
        int idxClassParent { get; set; }
        int indexValue { get; set; }
        string ArchtypeName { get; }
        string ClassParent { get; }
        uint headerOffset { get; set; }
        ulong ObjectFlags { get; set; }

        IExportEntry Clone();
        void setHeader(byte[] v);

        event PropertyChangedEventHandler PropertyChanged;

        PropertyCollection GetProperties();
        void WriteProperties(PropertyCollection props);
        int propsEnd();
        int detectStart();
        byte[] getBinaryData();
        void setBinaryData(byte[] binaryData);
        T GetProperty<T>(string name) where T : UProperty;
        void WriteProperty(UProperty prop);
    }
}
