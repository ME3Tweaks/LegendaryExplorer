using System;
using System.IO;
using UsefulThings.WPF;

namespace ME3Explorer.Packages
{
    public abstract class ImportEntry : ViewModelBase
    {
        public int Index { get; set; }
        public int UIndex { get { return -Index - 1; } }


        public IMEPackage FileRef { get; protected set; }

        public const int byteSize = 28;
        public byte[] header { get; protected set; }

        public int idxPackageFile { get { return BitConverter.ToInt32(header, 0); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 0, sizeof(int)); HeaderChanged = true; } }
        public int idxClassName { get { return BitConverter.ToInt32(header, 8); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 8, sizeof(int)); HeaderChanged = true; } }
        public int idxLink { get { return BitConverter.ToInt32(header, 16); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 16, sizeof(int)); HeaderChanged = true; } }
        public int idxObjectName { get { return BitConverter.ToInt32(header, 20); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 20, sizeof(int)); HeaderChanged = true; } }
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

        bool headerChanged;
        public bool HeaderChanged
        {
            get
            {
                return headerChanged;
            }

            set
            {
                headerChanged = value;
                if (value)
                {
                    OnPropertyChanged(); 
                }
            }
        }
    }

    public class ME3ImportEntry : ImportEntry, IImportEntry
    {

        public ME3ImportEntry(ME3Package pccFile, byte[] importData)
        {
            FileRef = pccFile;
            header = (byte[])importData.Clone();
        }

        public ME3ImportEntry(ME3Package pccFile, Stream importData)
        {
            FileRef = pccFile;
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
            FileRef = pccFile;
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
            FileRef = pccFile;
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
