using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Unreal;
using PropertyChanged;

namespace LegendaryExplorerCore.Packages
{
    [DebuggerDisplay("ImportEntry | {UIndex} = {InstancedFullPath}")]
    [DoNotNotify] //disable Fody/PropertyChanged for this class. Do notification manually
    public class ImportEntry : INotifyPropertyChanged, IEntry
    {
        public MEGame Game => FileRef.Game;

        public ImportEntry(IMEPackage pccFile, EndianReader importData)
        {
            HeaderOffset = importData.Position;
            FileRef = pccFile;
            Header = new byte[headerSize];
            importData.Read(Header, 0, Header.Length);
        }

        public ImportEntry(IMEPackage pccFile)
        {
            FileRef = pccFile;
            Header = new byte[headerSize];
        }

        /// <summary>
        /// Copy constructor. You should only use this if you know what you're doing. This is only used for duplicating objects in memory - do not attach to a package!
        /// </summary>
        /// <param name="imp"></param>
        public ImportEntry(ImportEntry imp)
        {
            FileRef = imp.FileRef;
            Index = imp.Index;
            _header = imp.Header;
        }

        public long HeaderOffset { get; set; }

        public int Index { private get; set; }
        public int UIndex => -Index - 1;

        public IMEPackage FileRef { get; protected set; }

        public const int headerSize = 28;

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
                    FileRef.IsModified = true; // mark package as modified if the existing header is changing.
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

        private int idxPackageFile
        {
            get => EndianReader.ToInt32(_header, 0, FileRef.Endian);
            set { Buffer.BlockCopy(EndianBitConverter.GetBytes(value, FileRef.Endian), 0, _header, 0, sizeof(int)); HeaderChanged = true; }
        }
        //int PackageNameNumber
        private int idxClassName
        {
            get => EndianReader.ToInt32(_header, 8, FileRef.Endian);
            set { Buffer.BlockCopy(EndianBitConverter.GetBytes(value, FileRef.Endian), 0, _header, 8, sizeof(int)); HeaderChanged = true; }
        }
        //int ClassNameNumber
        public int idxLink
        {
            get => EndianReader.ToInt32(_header, 16, FileRef.Endian);
            set
            {
                // HeaderOffset = 0 means this was instantiated and not read in from a stream
                if (value == UIndex && HeaderOffset != 0)
                {
                    throw new Exception("Cannot set import link to itself, this will cause infinite recursion");
                }
                Buffer.BlockCopy(EndianBitConverter.GetBytes(value, FileRef.Endian), 0, _header, 16, sizeof(int));
                HeaderChanged = true;
            }
        }
        private int idxObjectName
        {
            get => EndianReader.ToInt32(_header, 20, FileRef.Endian);
            set { Buffer.BlockCopy(EndianBitConverter.GetBytes(value, FileRef.Endian), 0, _header, 20, sizeof(int)); HeaderChanged = true; }
        }
        public int indexValue
        {
            get => EndianReader.ToInt32(_header, 24, FileRef.Endian);
            set { Buffer.BlockCopy(EndianBitConverter.GetBytes(value, FileRef.Endian), 0, _header, 24, sizeof(int)); HeaderChanged = true; }
        }


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

        public bool HeaderChanged { get; set; }


        private bool _entryHasPendingChanges;
        public bool EntryHasPendingChanges
        {
            get => _entryHasPendingChanges;
            set
            {
                if (_entryHasPendingChanges != value)
                {
                    _entryHasPendingChanges = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EntryHasPendingChanges)));
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
        public event PropertyChangedEventHandler PropertyChanged;
    }
}
