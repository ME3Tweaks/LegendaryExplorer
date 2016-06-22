using System;

namespace ME3Explorer.Packages
{
    public class ME3ExportEntry : IExportEntry
    {
        public byte[] header { get; set; }
        public ME3Package fileRef;
        public IMEPackage FileRef { get { return fileRef; } }
        public uint headerOffset { get; set; }

        public int idxClass { get { return BitConverter.ToInt32(header, 0); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 0, sizeof(int)); } }
        public int idxClassParent { get { return BitConverter.ToInt32(header, 4); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 4, sizeof(int)); } }
        public int idxLink { get { return BitConverter.ToInt32(header, 8); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 8, sizeof(int)); } }
        public int idxObjectName { get { return BitConverter.ToInt32(header, 12); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 12, sizeof(int)); } }
        public int indexValue { get { return BitConverter.ToInt32(header, 16); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 16, sizeof(int)); } }
        public int idxArchtype { get { return BitConverter.ToInt32(header, 20); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 20, sizeof(int)); } }
        public ulong ObjectFlags { get { return BitConverter.ToUInt64(header, 24); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 24, sizeof(long)); } }

        public string ObjectName { get { return fileRef.Names[idxObjectName]; } }
        public string ClassName { get { int val = idxClass; if (val != 0) return fileRef.Names[fileRef.getEntry(val).idxObjectName]; else return "Class"; } }
        public string ClassParent { get { int val = idxClassParent; if (val != 0) return fileRef.Names[fileRef.getEntry(val).idxObjectName]; else return "Class"; } }
        public string ArchtypeName { get { int val = idxArchtype; if (val != 0) return fileRef.getNameEntry(fileRef.getEntry(val).idxObjectName); else return "None"; } }

        public string PackageName
        {
            get
            {
                int val = idxLink;
                if (val != 0)
                {
                    IEntry entry = fileRef.getEntry(val);
                    return fileRef.Names[entry.idxObjectName];
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
                    string newPackageName = fileRef.getEntry(idxNewPackName).PackageName;
                    if (newPackageName != "Package")
                        result = newPackageName + "." + result;
                    idxNewPackName = fileRef.getEntry(idxNewPackName).idxLink;
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
        private byte[] _data = null;

        public byte[] Data
        {
            get
            {
                // if data isn't loaded then fill it from pcc file (load-on-demand)
                if (_data == null)
                {
                    fileRef.getData(DataOffset, this);
                }
                return _data;
            }

            set { _data = value; hasChanged = true; DataSize = value.Length; }
        }

        public bool hasChanged { get; internal set; }

        public ME3ExportEntry(ME3Package pccFile, byte[] importData, uint exportOffset)
        {
            fileRef = pccFile;
            header = (byte[])importData.Clone();
            headerOffset = exportOffset;
            hasChanged = false;
        }

        public ME3ExportEntry(ME3Package pccFile)
        {
            fileRef = pccFile;
        }

        public IExportEntry Clone()
        {
            ME3ExportEntry newExport = (ME3ExportEntry)this.MemberwiseClone(); // copy all reference-types vars
                                                                               // now creates new copies of referenced objects
            newExport.header = (byte[])this.header.Clone();
            newExport.Data = (byte[])this.Data.Clone();
            int index = 0;
            string name = ObjectName;
            foreach (ME3ExportEntry ent in fileRef.Exports)
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

    public class ME2ExportEntry : IExportEntry
    {
        public byte[] header { get; set; }
        public ME2Package fileRef;
        public IMEPackage FileRef { get { return fileRef; } }

        public int idxClass { get { return BitConverter.ToInt32(header, 0); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 0, sizeof(int)); } }
        public int idxClassParent { get { return BitConverter.ToInt32(header, 4); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 4, sizeof(int)); } }
        public int idxLink { get { return BitConverter.ToInt32(header, 8); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 8, sizeof(int)); } }
        public int idxObjectName { get { return BitConverter.ToInt32(header, 12); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 12, sizeof(int)); } }
        public int indexValue { get { return BitConverter.ToInt32(header, 16); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 16, sizeof(int)); } }
        public int idxArchtype { get { return BitConverter.ToInt32(header, 20); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 20, sizeof(int)); } }
        public ulong ObjectFlags { get { return BitConverter.ToUInt64(header, 24); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 64, sizeof(ulong)); } }

        public string ObjectName { get { return fileRef.Names[idxObjectName]; } }
        public string ClassParent { get { int val = idxClassParent; if (val < 0) return fileRef.Names[BitConverter.ToInt32(fileRef.Imports[val * -1 - 1].header, 20)]; else if (val > 0) return fileRef.Names[fileRef.Exports[val - 1].idxObjectName]; else return "Class"; } }
        public string ClassName { get { int val = idxClass; if (val < 0) return fileRef.Names[fileRef.Imports[val * -1 - 1].idxObjectName]; else if (val > 0) return fileRef.Names[fileRef.Exports[val].idxObjectName]; else return "Class"; } }
        public string ArchtypeName { get { int val = idxArchtype; if (val < 0) return fileRef.Names[fileRef.Imports[val * -1 - 1].idxObjectName]; else if (val > 0) return fileRef.Names[fileRef.Exports[val - 1].idxObjectName]; else return "None"; } }

        public string PackageName
        {
            get
            {
                int val = idxLink;
                if (val != 0)
                {
                    IEntry entry = fileRef.getEntry(val);
                    return fileRef.Names[entry.idxObjectName];
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
                    string newPackageName = fileRef.getEntry(idxNewPackName).PackageName;
                    if (newPackageName != "Package")
                        result = newPackageName + "." + result;
                    idxNewPackName = fileRef.getEntry(idxNewPackName).idxLink;
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

        public int DataSize { get { return BitConverter.ToInt32(header, 32); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 32, sizeof(int)); } }
        public int DataOffset { get { return BitConverter.ToInt32(header, 36); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 36, sizeof(int)); } }
        private byte[] _data = null;

        public byte[] Data
        {
            get { return _data; }

            set { _data = value; hasChanged = true; DataSize = value.Length; }
        }

        public bool hasChanged { get; internal set; }
        public uint headerOffset { get; set; }

        public ME2ExportEntry(ME2Package pccFile)
        {
            fileRef = pccFile;
        }

        public IExportEntry Clone()
        {
            ME2ExportEntry newExport = (ME2ExportEntry)this.MemberwiseClone(); // copy all reference-types vars
                                                                               // now creates new copies of referenced objects
            newExport.header = (byte[])this.header.Clone();
            newExport.Data = (byte[])this.Data.Clone();
            int index = 0;
            string name = ObjectName;
            foreach (ME2ExportEntry ent in fileRef.Exports)
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

    public class ME1ExportEntry : IExportEntry
    {
        public ME1Package fileRef;
        public IMEPackage FileRef { get { return fileRef; } }

        public byte[] header { get; set; }
        public uint headerOffset { get; set; }
        public int idxClass { get { return BitConverter.ToInt32(header, 0); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 0, sizeof(int)); } }
        public int idxClassParent { get { return BitConverter.ToInt32(header, 4); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 4, sizeof(int)); } }
        public int idxLink { get { return BitConverter.ToInt32(header, 8); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 8, sizeof(int)); } }
        public int idxObjectName { get { return BitConverter.ToInt32(header, 12); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 12, sizeof(int)); } }
        public int indexValue { get { return BitConverter.ToInt32(header, 16); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 16, sizeof(int)); } }
        public int idxArchtype { get { return BitConverter.ToInt32(header, 20); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 20, sizeof(int)); } }
        public ulong ObjectFlags { get { return BitConverter.ToUInt64(header, 24); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 64, sizeof(ulong)); } }

        public string ObjectName { get { return fileRef.Names[idxObjectName]; } }
        public string ClassName { get { int val = idxClass; if (val != 0) return fileRef.Names[fileRef.getEntry(val).idxObjectName]; else return "Class"; } }
        public string ClassParent { get { int val = idxClassParent; if (val != 0) return fileRef.Names[fileRef.getEntry(val).idxObjectName]; else return "Class"; } }
        public string ArchtypeName { get { int val = idxArchtype; if (val < 0) return fileRef.Names[fileRef.Imports[val * -1 - 1].idxObjectName]; else if (val > 0) return fileRef.Names[fileRef.Exports[val].idxObjectName]; else return "None"; } }

        public string PackageName
        {
            get
            {
                int val = idxLink;
                if (val != 0)
                {
                    IEntry entry = fileRef.getEntry(val);
                    return fileRef.Names[entry.idxObjectName];
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
                    string newPackageName = fileRef.getEntry(idxNewPackName).PackageName;
                    if (newPackageName != "Package")
                        result = newPackageName + "." + result;
                    idxNewPackName = fileRef.getEntry(idxNewPackName).idxLink;
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

        public int DataSize { get { return BitConverter.ToInt32(header, 32); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 32, sizeof(int)); } }
        public int DataOffset { get { return BitConverter.ToInt32(header, 36); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 36, sizeof(int)); } }
        private byte[] _data = null;

        public byte[] Data
        {
            get { return _data; }

            set { _data = value; hasChanged = true; DataSize = value.Length; }
        }

        public bool hasChanged { get; internal set; }

        public ME1ExportEntry(ME1Package file)
        {
            fileRef = file;
        }

        public IExportEntry Clone()
        {
            ME1ExportEntry newExport = (ME1ExportEntry)this.MemberwiseClone(); // copy all reference-types vars
                                                                               // now creates new copies of referenced objects
            newExport.header = (byte[])this.header.Clone();
            newExport.Data = (byte[])this.Data.Clone();
            int index = 0;
            string name = ObjectName;
            foreach (ME1ExportEntry ent in fileRef.Exports)
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
