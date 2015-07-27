using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace KFreonLib.PCCObjects
{
    public class ME1ImportEntry : IImportEntry
    {
        public string Package;
        public int link { get; set; }
        public string Name { get; set; }
        public byte[] raw;

        #region Unused inherited properties
        public string ObjectName
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

        public string ClassName
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


        public string PackageFullName
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

        public int idxObjectName
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

        public byte[] data
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

    public class ME2ImportEntry : IImportEntry
    {
        public string Package;
        public int link { get; set; }
        public string Name { get; set; }
        public byte[] raw;

        #region Unused inherited properties
        public string ObjectName
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

        public string ClassName
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


        public string PackageFullName
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

        public int idxObjectName
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

        public byte[] data
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

    public class ME3ImportEntry : IImportEntry
    {
        public static int byteSize = 28;
        public byte[] data = new byte[byteSize];
        internal ME3PCCObject pccRef;
        public int link { get; set; }
        public string Name { 
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public int idxPackageFile { get { return BitConverter.ToInt32(data, 0); } private set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, data, 0, sizeof(int)); } }
        public int idxClassName { get { return BitConverter.ToInt32(data, 8); } private set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, data, 8, sizeof(int)); } }
        public int idxPackageName { get { return BitConverter.ToInt32(data, 16) - 1; } private set { Buffer.BlockCopy(BitConverter.GetBytes(value + 1), 0, data, 16, sizeof(int)); } }
        public int idxObjectName { get { return BitConverter.ToInt32(data, 20); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, data, 20, sizeof(int)); } }
        public int idxLink { get { return BitConverter.ToInt32(data, 16); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, data, 16, sizeof(int)); } }
        public long ObjectFlags { get { return BitConverter.ToInt32(data, 24); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, data, 24, sizeof(int)); }}

        public string ClassName
        {
            get { return pccRef.Names[idxClassName]; }
            set
            {
                throw new NotImplementedException();
            }
        }
        public string PackageFile { get { return pccRef.Names[idxPackageFile] + ".pcc"; } }
        public string ObjectName
        {
            get { return pccRef.Names[idxObjectName]; }
            set
            {
                throw new NotImplementedException();
            }
        }
        public string PackageName { get { int val = idxPackageName; if (val >= 0) return pccRef.Names[pccRef.Exports[val].idxObjectName]; else return "Package"; } }
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

        public ME3ImportEntry(ME3PCCObject pccFile, byte[] importData)
        {
            pccRef = pccFile;
            data = (byte[])importData.Clone();
        }

        public ME3ImportEntry(ME3PCCObject pccFile, Stream importData)
        {
            pccRef = pccFile;
            data = new byte[ME3ImportEntry.byteSize];
            importData.Read(data, 0, data.Length);
        }

        byte[] IImportEntry.data
        {
            get
            {
                return data;
            }
            set
            {
                data = value;
            }
        }

    }
}
