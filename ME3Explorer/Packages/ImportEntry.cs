using System;
using System.IO;

namespace ME3Explorer.Packages
{
    public abstract class ImportEntry
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

        public const int byteSize = 28;
        public byte[] header { get; set; }

        public int idxPackageFile { get { return BitConverter.ToInt32(header, 0); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 0, sizeof(int)); } }
        public int idxClassName { get { return BitConverter.ToInt32(header, 8); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 8, sizeof(int)); } }
        public int idxLink { get { return BitConverter.ToInt32(header, 16); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 16, sizeof(int)); } }
        public int idxObjectName { get { return BitConverter.ToInt32(header, 20); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 20, sizeof(int)); } }
        public int ObjectFlags { get { return BitConverter.ToInt32(header, 24); } }

        public string ClassName { get { return FileRef.Names[idxClassName]; } }
        public string PackageFile { get { return FileRef.Names[idxPackageFile] + ".pcc"; } }
        public string ObjectName { get { return FileRef.Names[idxObjectName]; } }

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
    }

    public class ME3ImportEntry : ImportEntry, IImportEntry
    {

        public ME3ImportEntry(ME3Package pccFile, byte[] importData)
        {
            fileRef3 = pccFile;
            header = (byte[])importData.Clone();
        }

        public ME3ImportEntry(ME3Package pccFile, Stream importData)
        {
            fileRef3 = pccFile;
            header = new byte[byteSize];
            importData.Read(header, 0, header.Length);
        }

        public IImportEntry Clone()
        {
            ME3ImportEntry newImport = (ME3ImportEntry)MemberwiseClone();
            newImport.header = (byte[])header.Clone();
            return newImport;
        }
    }

    public class ME2ImportEntry : ImportEntry, IImportEntry
    {
        public ME2ImportEntry(ME2Package pccFile, byte[] importData)
        {
            fileRef2 = pccFile;
            header = (byte[])importData.Clone();
        }

        public IImportEntry Clone()
        {
            ME2ImportEntry newImport = (ME2ImportEntry)MemberwiseClone();
            newImport.header = (byte[])header.Clone();
            return newImport;
        }
    }

    public class ME1ImportEntry : ImportEntry, IImportEntry
    {
        public ME1ImportEntry(ME1Package pccFile, byte[] importData)
        {
            fileRef1 = pccFile;
            header = (byte[])importData.Clone();
        }

        public IImportEntry Clone()
        {
            ME1ImportEntry newImport = (ME1ImportEntry)MemberwiseClone();
            newImport.header = (byte[])header.Clone();
            return newImport;
        }
    }
}
