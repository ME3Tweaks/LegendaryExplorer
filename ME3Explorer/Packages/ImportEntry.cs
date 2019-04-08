using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UsefulThings.WPF;

namespace ME3Explorer.Packages
{
    [DebuggerDisplay("ImportEntry | {UIndex} = {GetFullPath}")]
    public class ImportEntry : ViewModelBase, IEntry
    {
        public ImportEntry(IMEPackage pccFile, Stream importData)
        {
            HeaderOffset = importData.Position;
            FileRef = pccFile;
            Header = new byte[byteSize];
            importData.Read(Header, 0, Header.Length);
        }

        public ImportEntry(IMEPackage pccFile)
        {
            FileRef = pccFile;
            Header = new byte[byteSize];
        }

        public long HeaderOffset { get; set; }

        public int Index { get; set; }
        public int UIndex { get { return -Index - 1; } }

        public IMEPackage FileRef { get; protected set; }

        public const int byteSize = 28;

        protected byte[] _header;
        public byte[] Header
        {
            get { return _header; }
            set
            {
                bool isFirstLoad = _header == null;
                if (_header != null && value != null && _header.SequenceEqual(value))
                {
                    return; //if the data is the same don't write it and trigger the side effects
                }
                _header = value;
                if (!isFirstLoad)
                {
                    HeaderChanged = true;
                    EntryHasPendingChanges = true;
                }
            }
        }

        /// <summary>
        /// Returns a clone of the header for modifying
        /// </summary>
        /// <returns></returns>
        public byte[] GetHeader()
        {
            return _header.TypedClone();
        }

        public int idxPackageFile { get { return BitConverter.ToInt32(Header, 0); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, Header, 0, sizeof(int)); HeaderChanged = true; } }
        //int PackageNameNumber
        public int idxClassName { get { return BitConverter.ToInt32(Header, 8); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, Header, 8, sizeof(int)); HeaderChanged = true; } }
        //int ClassNameNumber
        public int idxLink { get { return BitConverter.ToInt32(Header, 16); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, Header, 16, sizeof(int)); HeaderChanged = true; } }
        public int idxObjectName { get { return BitConverter.ToInt32(Header, 20); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, Header, 20, sizeof(int)); HeaderChanged = true; } }
        public int indexValue { get { return BitConverter.ToInt32(Header, 24); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, Header, 24, sizeof(int)); HeaderChanged = true; } }

        //int ObjectNameNumber

        public string ClassName { get { return FileRef.Names[idxClassName]; } }
        public string PackageFile { get { return FileRef.Names[idxPackageFile] + ".pcc"; } } //Is this valid for ME1?
        public string PackageFileNoExtension { get { return FileRef.Names[idxPackageFile]; } }
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
                OnPropertyChanged();
            }
        }


        private bool _entryHasPendingChanges = false;
        public bool EntryHasPendingChanges
        {
            get { return _entryHasPendingChanges; }
            set
            {
                if (value != _entryHasPendingChanges)
                {
                    _entryHasPendingChanges = value;
                    OnPropertyChanged();
                }
            }
        }

        public ImportEntry Clone()
        {
            ImportEntry newImport = (ImportEntry)MemberwiseClone();
            newImport.Header = (byte[])Header.Clone();
            return newImport;
        }
    }
}
