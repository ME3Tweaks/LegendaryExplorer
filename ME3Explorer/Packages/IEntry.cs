using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Explorer.Packages
{
    public interface IEntry
    {
        string ClassName { get; }
        string GetFullPath { get; }
        int idxLink { get; }
        int idxObjectName { get; }
        string ObjectName { get; }
        string PackageFullName { get; }
        string PackageName { get; }
        IMEPackage FileRef { get; }
    }
    
    public interface IImportEntry : IEntry
    {
        int idxClassName { get; set; }
        int idxPackageFile { get; set; }
        string PackageFile { get; }
    }

    public interface IExportEntry : IEntry
    {
        string ArchtypeName { get; }
        string ClassParent { get; }
        byte[] Data { get; set; }
        int DataOffset { get; }
        int DataSize { get; }
        bool hasChanged { get; }
        int idxArchtype { get; set; }
        int idxClass { get; set; }
        int idxClassParent { get; set; }
        int indexValue { get; set; }
        ulong ObjectFlags { get; set; }
        uint headerOffset { get; set; }
    }
}
