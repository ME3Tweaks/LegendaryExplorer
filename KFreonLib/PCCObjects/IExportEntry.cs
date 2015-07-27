using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KFreonLib.PCCObjects
{
    public interface IExportEntry
    {
        byte[] Data { get; set; }

        long flagint { get; set; }

        string ObjectName { get; set; }

        string PackageFullName { get; set; }

        string ClassName { get; set; }

        void SetData(byte[] p);

        uint DataOffset { get; set; }

        bool hasChanged { get; set; }

        string Package { get; set; }

        bool ValidTextureClass();

        int ClassNameID { get; set; }

        int DataSize { get; set; }
        int idxLink { get; set; }
        int idxClassName { get; set; }
        byte[] info { get; set; }

        int idxObjectName { get; set; }

        uint offset { get; set; }

        int DataOffsetTmp { get; set; }

        IPCCObject pccRef { get; set; }

        int idxPackageName { get; set; }

        string PackageName { get; set; }

        string GetFullPath { get; set; }

        int indexValue { get; set; }

        string ArchtypeName { get; set; }

        int idxArchtypeName { get; set; }

        long ObjectFlags { get; set; }

        void LegacySetData(byte[] newData);
    }
}
