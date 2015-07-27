using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Gibbed.IO;

namespace KFreonLib.PCCObjects
{
    public class ME1ExportEntry : IExportEntry
    {
        public byte[] info { get; set; } //Properties, not raw data
        public int ClassNameID { get { return BitConverter.ToInt32(info, 0); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, info, 0, sizeof(int)); } }
        public int LinkID { get { return BitConverter.ToInt32(info, 8); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, info, 8, sizeof(int)); } }
        public int PackageNameID;
        public int ObjectNameID { get { return BitConverter.ToInt32(info, 12); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, info, 12, sizeof(int)); } }
        public string ObjectName { get; set; }
        public string PackageName
        {
            get
            {
                string temppack = PackageFullName;
                if (temppack == "." || String.IsNullOrEmpty(PackageFullName))
                    return "";
                temppack = temppack.Remove(temppack.Length - 1);
                if (temppack.Split('.').Length > 1)
                    return temppack.Split('.')[temppack.Split('.').Length - 1];
                else
                    return temppack.Split('.')[0];
            }
            set
            {
                throw new NotImplementedException();
            }
        }
        public string PackageFullName { get; set; }
        public string ClassName { get; set; }
        public byte[] flag
        {
            get
            {
                byte[] val = new byte[4];
                Buffer.BlockCopy(info, 28, val, 0, 4);
                return val;
            }
        }
        public long flagint
        {
            get
            {
                byte[] val = new byte[4];
                Buffer.BlockCopy(info, 28, val, 0, 4);
                return BitConverter.ToInt32(val, 0);
            }
            set
            {
                throw new NotImplementedException();
            }
        }
        public IPCCObject pccRef { get; set; }
        public int DataSize { get { return BitConverter.ToInt32(info, 32); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, info, 32, sizeof(int)); } }
        public int DataOffset { get { return BitConverter.ToInt32(info, 36); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, info, 36, sizeof(int)); } }
        public byte[] Data
        {
            get { byte[] val = new byte[DataSize]; pccRef.listsStream.Seek(DataOffset, SeekOrigin.Begin); val = pccRef.listsStream.ReadBytes(DataSize); return val; }
            set
            {
                if (value.Length > DataSize)
                {
                    pccRef.listsStream.Seek(0, SeekOrigin.End);
                    DataOffset = (int)pccRef.listsStream.Position;
                    pccRef.listsStream.WriteBytes(value);
                    pccRef.LastExport = this;
                    MoveNames();
                }
                else
                {
                    pccRef.listsStream.Seek(DataOffset, SeekOrigin.Begin);
                    pccRef.listsStream.WriteBytes(value);
                }
                if (value.Length != DataSize)
                {
                    DataSize = value.Length;
                    pccRef.listsStream.Seek(infoOffset, SeekOrigin.Begin);
                    pccRef.listsStream.WriteBytes(info);
                }
            }
        }
        public bool hasChanged { get; set; }
        public int infoOffset;

        private void MoveNames()
        {
            pccRef.NameOffset = (int)pccRef.listsStream.Position;
            foreach (string name in pccRef.Names)
            {
                pccRef.listsStream.WriteValueS32(name.Length + 1);
                pccRef.listsStream.WriteString(name);
                pccRef.listsStream.WriteByte(0);
                pccRef.listsStream.WriteValueS32(0);
                pccRef.listsStream.WriteValueS32(458768);
            }
        }

        public void SetData(byte[] p)
        {
            Data = p;
        }

        uint IExportEntry.DataOffset
        {
            get
            {
                return (uint)DataOffset;
            }
            set
            {
                DataOffset = (int)value;
            }
        }

        public string Package
        {
            get
            {
                return PackageName;
            }
            set
            {
                throw new NotImplementedException();
            }
        }


        public bool ValidTextureClass()
        {
            return Misc.ValidTexClass(ClassName);
        }

        public int idxClassName
        {
            get
            {
                return ClassNameID;
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public int idxObjectName
        {
            get
            {
                return ObjectNameID;
            }
            set
            {
                ObjectNameID = value;
            }
        }

        #region Unused Inherited Properties
        public void LegacySetData(byte[] newData)
        {
            throw new NotImplementedException();
        }

        public int idxLink
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public uint offset
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public int DataOffsetTmp
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public int idxPackageName
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }


        public string GetFullPath
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }


        public int indexValue
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public string ArchtypeName
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public int idxArchtypeName
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public long ObjectFlags
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }
        #endregion
    }

    public class ME2ExportEntry : IExportEntry
    {
        public byte[] info { get; set; } //Properties, not raw data
        public int ClassNameID { get { return BitConverter.ToInt32(info, 0); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, info, 0, sizeof(int)); } }
        public int LinkID { get { return BitConverter.ToInt32(info, 8); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, info, 8, sizeof(int)); } }
        public int PackageNameID;
        public int ObjectNameID { get { return BitConverter.ToInt32(info, 12); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, info, 12, sizeof(int)); } }
        public string ObjectName { get; set; }
        public string PackageFullName { get; set; }
        public string ClassName { get; set; }
        public byte[] flag
        {
            get
            {
                byte[] val = new byte[4];
                Buffer.BlockCopy(info, 28, val, 0, 4);
                return val;
            }
        }

        public long flagint
        {
            get
            {
                byte[] val = new byte[4];
                Buffer.BlockCopy(info, 28, val, 0, 4);
                return BitConverter.ToInt32(val, 0);
            }
            set
            {
                throw new NotImplementedException();
            }
        }
        public IPCCObject pccRef { get; set; }
        public int DataSize { get { return BitConverter.ToInt32(info, 32); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, info, 32, sizeof(int)); } }
        public int DataOffset { get { return BitConverter.ToInt32(info, 36); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, info, 36, sizeof(int)); } }
        public byte[] Data
        {
            get { byte[] val = new byte[DataSize]; pccRef.listsStream.Seek(DataOffset, SeekOrigin.Begin); val = pccRef.listsStream.ReadBytes(DataSize); return val; }
            set
            {
                if (value.Length > DataSize)
                {
                    pccRef.listsStream.Seek(0, SeekOrigin.End);
                    DataOffset = (int)pccRef.listsStream.Position;
                    pccRef.listsStream.WriteBytes(value);
                    pccRef.LastExport = this;
                }
                else
                {
                    pccRef.listsStream.Seek(DataOffset, SeekOrigin.Begin);
                    pccRef.listsStream.WriteBytes(value);
                }
                if (value.Length != DataSize)
                {
                    DataSize = value.Length;
                    pccRef.listsStream.Seek(infoOffset, SeekOrigin.Begin);
                    pccRef.listsStream.WriteBytes(info);
                }
            }
        }
        public bool hasChanged { get; set; }
        public int infoOffset;

        public void SetData(byte[] p)
        {
            Data = p;
        }

        uint IExportEntry.DataOffset
        {
            get
            {
                return (uint)DataOffset;
            }
            set
            {
                DataOffset = (int)value;
            }
        }


        public bool ValidTextureClass()
        {
            return Misc.ValidTexClass(ClassName);
        }


        public int idxLink
        {
            get
            {
                return LinkID;
            }
            set
            {
                LinkID = value;
            }
        }

        public int idxClassName
        {
            get
            {
                return ClassNameID;
            }
            set
            {
                ClassNameID = value;
            }
        }

        public int idxObjectName
        {
            get
            {
                return ObjectNameID;
            }
            set
            {
                ObjectNameID = value;
            }
        }

        #region Unused Inherited Properties
        public void LegacySetData(byte[] newData)
        {
            throw new NotImplementedException();
        }

        public string Package
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public uint offset
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public int DataOffsetTmp
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }


        public int idxPackageName
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public string PackageName
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }


        public string GetFullPath
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }


        public int indexValue
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public string ArchtypeName
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public int idxArchtypeName
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public long ObjectFlags
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }
        #endregion
    }

    public class ME3ExportEntry : IExportEntry, ICloneable
    {
        public byte[] info { get; set; } // holds data about export header, not the export data.
        public IPCCObject pccRef { get; set; }
        public uint InfoOffset { get; private set; }
        public uint offset { get; set; }
        public int Link
        {
            get
            {
                return idxLink;
            }
            set 
            {
                throw new NotImplementedException();
            }
        } 

        public int ClassNameID
        {
            get
            {
                return idxClassName;
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public int idxClassName { get { return BitConverter.ToInt32(info, 0); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, info, 0, sizeof(int)); } }
        public int idxClassParent { get { return BitConverter.ToInt32(info, 4); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, info, 4, sizeof(int)); } }
        public int idxLink { get { return BitConverter.ToInt32(info, 8); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, info, 8, sizeof(int)); } }
        public int idxPackageName { get { return BitConverter.ToInt32(info, 8) - 1; } set { Buffer.BlockCopy(BitConverter.GetBytes(value + 1), 0, info, 8, sizeof(int)); } }
        public int idxObjectName { get { return BitConverter.ToInt32(info, 12); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, info, 12, sizeof(int)); } }
        public int indexValue { get { return BitConverter.ToInt32(info, 16); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, info, 16, sizeof(int)); } }
        public int idxArchtypeName { get { return BitConverter.ToInt32(info, 20); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, info, 20, sizeof(int)); } }
        public string ArchtypeName
        {
            get { int val = idxArchtypeName; if (val < 0)  return pccRef.Names[pccRef.Imports[val * -1 - 1].idxObjectName]; else if (val > 0) return pccRef.Names[pccRef.Exports[val].idxObjectName]; else return "None"; }
            set
            {
                throw new NotImplementedException();
            }
        }
        public long ObjectFlags { get { return BitConverter.ToInt64(info, 24); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, info, 64, sizeof(long)); } }

        public string ObjectName { get { return pccRef.Names[idxObjectName]; } set { throw new NotImplementedException(); } }
        public string ClassName { get { int val = idxClassName; if (val < 0)  return pccRef.Names[pccRef.Imports[val * -1 - 1].idxObjectName]; else if (val > 0) return pccRef.Names[pccRef.Exports[val].idxObjectName]; else return "Class"; } set { throw new NotImplementedException(); } }
        public string ClassParent { get { int val = idxClassParent; if (val < 0)  return pccRef.Names[pccRef.Imports[val * -1 - 1].idxObjectName]; else if (val > 0) return pccRef.Names[pccRef.Exports[val].idxObjectName]; else return "Class"; } }
        public string PackageName
        {
            get { int val = idxPackageName; if (val >= 0) return pccRef.Names[pccRef.Exports[val].idxObjectName]; else return "Package"; }
            set
            {
                throw new NotImplementedException();
            }
        }
        public string PackageFullName
        {
            get
            {
                string result = PackageName;
                int idxNewPackName = idxPackageName;

                while (idxNewPackName >= 0)
                {
                    string newPackageName = pccRef.Exports[idxNewPackName].PackageName;
                    if (newPackageName != "Package")
                        result = newPackageName + "." + result;
                    idxNewPackName = pccRef.Exports[idxNewPackName].idxPackageName;
                }
                return result;
            }
            set
            {
                throw new NotImplementedException();
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
            set
            {
                throw new NotImplementedException();
            }
        }

        public int DataSize { get { return BitConverter.ToInt32(info, 32); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, info, 32, sizeof(int)); } }
        internal uint dataoff = 0;
        public int DataOffsetTmp { get; set; }
        public uint DataOffset
        {
            get
            {
                if (KFreonLib.Misc.Methods.FindInStack("CloneDialog"))
                    return dataoff;
                else
                    return BitConverter.ToUInt32(info, 36);
            }
            set
            {
                if (KFreonLib.Misc.Methods.FindInStack("CloneDialog"))
                    dataoff = value;
                else
                    Buffer.BlockCopy(BitConverter.GetBytes(value), 0, info, 36, sizeof(uint));
            }
        }
        byte[] _data = null;
        //public byte[] Data { get { return GetData(); } set { SetData(value); } }
        public byte[] Data { get { return GetData(); } set { SetData(value); hasChanged = true; } }

        public bool hasChanged { get; set; }

        private byte[] GetData()
        {
            if (_data == null)
            {
                pccRef.listsStream.Seek((long)DataOffset, SeekOrigin.Begin);
                return pccRef.listsStream.ReadBytes(DataSize);
            }
            else
                return _data;
        }

        public void SetData(byte[] newData)
        {
            pccRef.listsStream.Seek(pccRef.expDataEndOffset, SeekOrigin.Begin);
            DataOffset = (uint)pccRef.listsStream.Position;
            DataSize = newData.Length;
            pccRef.listsStream.WriteBytes(newData);

            RefreshInfo();
            GC.Collect();
        }

        public void LegacySetData(byte[] newData)
        {
            _data = newData;
            hasChanged = true;
        }

        private void RefreshInfo()
        {
            pccRef.listsStream.Seek(InfoOffset, SeekOrigin.Begin);
            pccRef.listsStream.WriteBytes(info);
        }

        public ME3ExportEntry(ME3PCCObject pccFile, byte[] importData, uint exportOffset)
        {
            pccRef = pccFile;
            info = (byte[])importData.Clone();
            InfoOffset = exportOffset;
            hasChanged = false;
        }

        public ME3ExportEntry()
        {

        }

        object ICloneable.Clone()
        {
            return this.Clone();
        }

        public ME3ExportEntry Clone()
        {
            ME3ExportEntry newExport = (ME3ExportEntry)this.MemberwiseClone(); // copy all reference-types vars
            // now creates new copies of referenced objects
            newExport.info = (byte[])this.info.Clone();
            newExport.Data = (byte[])this.Data.Clone();
            return newExport;
        }

        #region Unused Inherited Properties
        public long flagint
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }
        #endregion

        public string Package
        {
            get
            {
                return PackageName;
            }
            set
            {
                throw new NotImplementedException();
            }
        }


        public bool ValidTextureClass()
        {
            return Misc.ValidTexClass(ClassName);
        }
    }
}
