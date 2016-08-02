using System;

namespace ME3Explorer.Packages
{
    public abstract class ExportEntry
    {
        public IMEPackage FileRef { get; protected set; }

        protected ExportEntry(byte[] headerData)
        {
            header = (byte[])headerData.Clone();
            OriginalDataSize = DataSize;
        }

        protected ExportEntry()
        {
            OriginalDataSize = 0;
        }

        public byte[] header { get; protected set; }

        public void setHeader(byte[] newHead)
        {
            header = newHead;
            hasChanged = true;
        }

        public uint headerOffset { get; set; }

        public int idxClass { get { return BitConverter.ToInt32(header, 0); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 0, sizeof(int)); hasChanged = true; } }
        public int idxClassParent { get { return BitConverter.ToInt32(header, 4); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 4, sizeof(int)); hasChanged = true; } }
        public int idxLink { get { return BitConverter.ToInt32(header, 8); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 8, sizeof(int)); hasChanged = true; } }
        public int idxObjectName { get { return BitConverter.ToInt32(header, 12); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 12, sizeof(int)); hasChanged = true; } }
        public int indexValue { get { return BitConverter.ToInt32(header, 16); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 16, sizeof(int)); hasChanged = true; } }
        public int idxArchtype { get { return BitConverter.ToInt32(header, 20); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 20, sizeof(int)); hasChanged = true; } }
        public ulong ObjectFlags { get { return BitConverter.ToUInt64(header, 24); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 24, sizeof(long)); hasChanged = true; } }

        public string ObjectName { get { return FileRef.Names[idxObjectName]; } }
        public string ClassName { get { int val = idxClass; if (val != 0) return FileRef.Names[FileRef.getEntry(val).idxObjectName]; else return "Class"; } }
        public string ClassParent { get { int val = idxClassParent; if (val != 0) return FileRef.Names[FileRef.getEntry(val).idxObjectName]; else return "Class"; } }
        public string ArchtypeName { get { int val = idxArchtype; if (val != 0) return FileRef.getNameEntry(FileRef.getEntry(val).idxObjectName); else return "None"; } }

        public string PackageName
        {
            get
            {
                int val = idxLink;
                if (val != 0)
                {
                    IEntry entry = FileRef.getEntry(val);
                    return FileRef.Names[entry.idxObjectName];
                }
                else return "Package";
            }
        }

        public string PackageFullName
        {
            get
            {
                string result = PackageName;
                int idxNewPackName = idxLink;

                while (idxNewPackName != 0)
                {
                    string newPackageName = FileRef.getEntry(idxNewPackName).PackageName;
                    if (newPackageName != "Package")
                        result = newPackageName + "." + result;
                    idxNewPackName = FileRef.getEntry(idxNewPackName).idxLink;
                }
                return result;
            }
        }

        public string GetFullPath
        {
            get
            {
                string s = "";
                if (PackageFullName != "Class" && PackageFullName != "Package")
                    s += PackageFullName + ".";
                s += ObjectName;
                return s;
            }
        }
        
        public byte[] Data
        {
            get { return _data; }

            set { _data = value; hasChanged = true; DataSize = value.Length; }
        }

        public int DataSize { get { return BitConverter.ToInt32(header, 32); } internal set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 32, sizeof(int)); } }
        public int DataOffset { get { return BitConverter.ToInt32(header, 36); } internal set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 36, sizeof(int)); } }
        public readonly int OriginalDataSize;
        protected byte[] _data = null;
        
        public bool hasChanged { get; internal set; }
    }

    public class ME3ExportEntry : ExportEntry, IExportEntry
    {
        public ME3ExportEntry(ME3Package pccFile, byte[] headerData, uint exportOffset) :
            base(headerData)
        {
            FileRef = pccFile;
            headerOffset = exportOffset;
        }

        public ME3ExportEntry(ME3Package pccFile)
        {
            FileRef = pccFile;
        }

        public IExportEntry Clone()
        {
            ME3ExportEntry newExport = new ME3ExportEntry(FileRef as ME3Package);
            newExport.header = (byte[])this.header.Clone();
            newExport.headerOffset = 0;
            newExport.Data = (byte[])this.Data.Clone();
            int index = 0;
            string name = ObjectName;
            foreach (IExportEntry ent in FileRef.Exports)
            {
                if (name == ent.ObjectName && ent.indexValue > index)
                {
                    index = ent.indexValue;
                }
            }
            index++;
            newExport.indexValue = index;
            return newExport;
        }
    }

    public class ME2ExportEntry : ExportEntry, IExportEntry
    {
        public ME2ExportEntry(ME2Package pccFile, byte[] headerData, uint exportOffset) :
            base(headerData)
        {
            FileRef = pccFile;
            headerOffset = exportOffset;
        }

        public ME2ExportEntry(ME2Package pccFile)
        {
            FileRef = pccFile;
        }

        public IExportEntry Clone()
        {
            ME2ExportEntry newExport = new ME2ExportEntry(FileRef as ME2Package);
            newExport.header = (byte[])this.header.Clone();
            newExport.headerOffset = 0;
            newExport.Data = (byte[])this.Data.Clone();
            int index = 0;
            string name = ObjectName;
            foreach (IExportEntry ent in FileRef.Exports)
            {
                if (name == ent.ObjectName && ent.indexValue > index)
                {
                    index = ent.indexValue;
                }
            }
            index++;
            newExport.indexValue = index;
            return newExport;
        }
    }

    public class ME1ExportEntry : ExportEntry, IExportEntry
    {
        public ME1ExportEntry(ME1Package pccFile, byte[] headerData, uint exportOffset) :
            base(headerData)
        {
            FileRef = pccFile;
            headerOffset = exportOffset;
        }

        public ME1ExportEntry(ME1Package file)
        {
            FileRef = file;
        }

        public IExportEntry Clone()
        {
            ME1ExportEntry newExport = new ME1ExportEntry(FileRef as ME1Package);
            newExport.header = (byte[])this.header.Clone();
            newExport.headerOffset = 0;
            newExport.Data = (byte[])this.Data.Clone();
            int index = 0;
            string name = ObjectName;
            foreach (IExportEntry ent in FileRef.Exports)
            {
                if (name == ent.ObjectName && ent.indexValue > index)
                {
                    index = ent.indexValue;
                }
            }
            index++;
            newExport.indexValue = index;
            return newExport;
        }
    }
}
