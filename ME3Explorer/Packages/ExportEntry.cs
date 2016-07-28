using System;

namespace ME3Explorer.Packages
{
    public abstract class ExportEntry
    {
        public ME1Package fileRef1;
        public ME2Package fileRef2;
        public ME3Package fileRef3;
        public IMEPackage FileRef
        {
            get
            {
                if (fileRef1 != null)
                {
                    return fileRef1;
                }
                else if (fileRef2 != null)
                {
                    return fileRef2;
                }
                else
                {
                    return fileRef3;
                }
            }
        }

        public byte[] header { get; set; }

        public uint headerOffset { get; set; }

        public int idxClass { get { return BitConverter.ToInt32(header, 0); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 0, sizeof(int)); } }
        public int idxClassParent { get { return BitConverter.ToInt32(header, 4); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 4, sizeof(int)); } }
        public int idxLink { get { return BitConverter.ToInt32(header, 8); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 8, sizeof(int)); } }
        public int idxObjectName { get { return BitConverter.ToInt32(header, 12); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 12, sizeof(int)); } }
        public int indexValue { get { return BitConverter.ToInt32(header, 16); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 16, sizeof(int)); } }
        public int idxArchtype { get { return BitConverter.ToInt32(header, 20); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 20, sizeof(int)); } }
        public ulong ObjectFlags { get { return BitConverter.ToUInt64(header, 24); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 24, sizeof(long)); } }

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

        public int DataSize { get { return BitConverter.ToInt32(header, 32); } internal set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 32, sizeof(int)); } }
        public int DataOffset { get { return BitConverter.ToInt32(header, 36); } internal set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 36, sizeof(int)); } }
        public int DataOffsetTmp;
        protected byte[] _data = null;
        
        public bool hasChanged { get; internal set; }
    }

    public class ME3ExportEntry : ExportEntry, IExportEntry
    {
        public byte[] Data
        {
            get
            {
                // if data isn't loaded then fill it from pcc file (load-on-demand)
                if (_data == null)
                {
                    _data = fileRef3.getData(DataOffset, this);
                }
                return _data;
            }

            set { _data = value; hasChanged = true; DataSize = value.Length; }
        }

        public ME3ExportEntry(ME3Package pccFile, byte[] headerData, uint exportOffset)
        {
            fileRef3 = pccFile;
            header = (byte[])headerData.Clone();
            headerOffset = exportOffset;
            hasChanged = false;
        }

        public ME3ExportEntry(ME3Package pccFile)
        {
            fileRef3 = pccFile;
        }

        public IExportEntry Clone()
        {
            ME3ExportEntry newExport = (ME3ExportEntry)this.MemberwiseClone(); // copy all reference-types vars
                                                                               // now creates new copies of referenced objects
            newExport.header = (byte[])this.header.Clone();
            newExport.Data = (byte[])this.Data.Clone();
            int index = 0;
            string name = ObjectName;
            foreach (IExportEntry ent in fileRef3.Exports)
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
        public byte[] Data
        {
            get { return _data; }

            set { _data = value; hasChanged = true; DataSize = value.Length; }
        }

        public ME2ExportEntry(ME2Package pccFile)
        {
            fileRef2 = pccFile;
        }

        public IExportEntry Clone()
        {
            ME2ExportEntry newExport = (ME2ExportEntry)this.MemberwiseClone(); // copy all reference-types vars
                                                                               // now creates new copies of referenced objects
            newExport.header = (byte[])this.header.Clone();
            newExport.Data = (byte[])this.Data.Clone();
            int index = 0;
            string name = ObjectName;
            foreach (IExportEntry ent in fileRef2.Exports)
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
        public byte[] Data
        {
            get { return _data; }

            set { _data = value; hasChanged = true; DataSize = value.Length; }
        }

        public ME1ExportEntry(ME1Package file)
        {
            fileRef1 = file;
        }

        public IExportEntry Clone()
        {
            ME1ExportEntry newExport = (ME1ExportEntry)this.MemberwiseClone(); // copy all reference-types vars
                                                                               // now creates new copies of referenced objects
            newExport.header = (byte[])this.header.Clone();
            newExport.Data = (byte[])this.Data.Clone();
            int index = 0;
            string name = ObjectName;
            foreach (IExportEntry ent in fileRef1.Exports)
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
