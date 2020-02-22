using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Gammtek.Conduit.IO;
using ME3Explorer.Unreal;

namespace ME3Explorer.Packages
{
    [DebuggerDisplay("ImportEntry | {UIndex} = {InstancedFullPath}")]
    public class ImportEntry : NotifyPropertyChangedBase, IEntry
    {
        public MEGame Game => FileRef.Game;

        public ImportEntry(IMEPackage pccFile, EndianReader importData)
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
        public int UIndex => -Index - 1;

        public IMEPackage FileRef { get; protected set; }

        public const int byteSize = 28;

        protected byte[] _header;
        public byte[] Header
        {
            get => _header;
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

        public bool HasParent => FileRef.IsEntry(idxLink);

        public IEntry Parent
        {
            get => FileRef.GetEntry(idxLink);
            set => idxLink = value?.UIndex ?? 0;
        }

        private int idxPackageFile { get => EndianReader.ToInt32(_header, 0,FileRef.Endian);
            set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, _header, 0, sizeof(int)); HeaderChanged = true; } }
        //int PackageNameNumber
        private int idxClassName { get => EndianReader.ToInt32(_header, 8, FileRef.Endian);
            set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, _header, 8, sizeof(int)); HeaderChanged = true; } }
        //int ClassNameNumber
        public int idxLink { get => EndianReader.ToInt32(_header, 16, FileRef.Endian);
            set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, _header, 16, sizeof(int)); HeaderChanged = true; } }
        private int idxObjectName { get => EndianReader.ToInt32(_header, 20, FileRef.Endian);
            set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, _header, 20, sizeof(int)); HeaderChanged = true; } }
        public int indexValue { get => EndianReader.ToInt32(_header, 24, FileRef.Endian);
            set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, _header, 24, sizeof(int)); HeaderChanged = true; } }




        public string ClassName
        {
            get => FileRef.Names[idxClassName];
            set => idxClassName = FileRef.FindNameOrAdd(value);
        }

        private string ObjectNameString
        {
            get => FileRef.Names[idxObjectName];
            set => idxObjectName = FileRef.FindNameOrAdd(value);
        }

        public NameReference ObjectName
        {
            get => new NameReference(ObjectNameString, indexValue);
            set => (ObjectNameString, indexValue) = value;
        }

        public string PackageFile
        {
            get => FileRef.Names[idxPackageFile];
            set => idxPackageFile = FileRef.FindNameOrAdd(value);
        }

        public string ParentName => FileRef.GetEntry(idxLink)?.ObjectName ?? "";

        public string ParentFullPath => FileRef.GetEntry(idxLink)?.FullPath ?? "";

        public string FullPath => FileRef.IsEntry(idxLink) ? $"{ParentFullPath}.{ObjectName.Name}" : ObjectName.Name;

        public string ParentInstancedFullPath => FileRef.GetEntry(idxLink)?.InstancedFullPath ?? "";

        public string InstancedFullPath => FileRef.IsEntry(idxLink) ? $"{ParentInstancedFullPath}.{ObjectName.Instanced}" : ObjectName.Instanced;

        bool headerChanged;
        public bool HeaderChanged
        {
            get => headerChanged;

            set
            {
                headerChanged = value;
                OnPropertyChanged();
            }
        }


        private bool _entryHasPendingChanges;

        public bool EntryHasPendingChanges
        {
            get => _entryHasPendingChanges;
            set
            {
                if (value != _entryHasPendingChanges)
                {
                    _entryHasPendingChanges = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsClass => ClassName == "Class";

        public ImportEntry Clone()
        {
            ImportEntry newImport = (ImportEntry)MemberwiseClone();
            newImport.Header = Header.TypedClone();
            return newImport;
        }

        IEntry IEntry.Clone() => Clone();
    }
}
