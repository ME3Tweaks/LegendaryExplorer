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

        public ImportEntry(IMEPackage pccFile, IEntry parent, NameReference name) : this(pccFile, parent?.UIndex ?? 0, name) { }
        public ImportEntry(IMEPackage pccFile, int parentUindex, NameReference name)
        {
            FileRef = pccFile;
            Header = new byte[headerSize];
            EndianBitConverter.WriteAsBytes(parentUindex, _header.AsSpan(OFFSET_idxLink), FileRef.Endian);
            EndianBitConverter.WriteAsBytes(FileRef.FindNameOrAdd(name.Name), _header.AsSpan(OFFSET_idxObjectName), FileRef.Endian);
            EndianBitConverter.WriteAsBytes(name.Number, _header.AsSpan(OFFSET_indexValue), FileRef.Endian);
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
                if (_header != null && value != null && _header.AsSpan().SequenceEqual(value))
                {
                    return; //if the data is the same don't write it and trigger the side effects
                }
                _header = value;
                if (!isFirstLoad)
                {
                    //new header may have changed link or name
                    FileRef.InvalidateLookupTable();

                    FileRef.IsModified = true; // mark package as modified if the existing header is changing.
                    HeaderChanged = true;
                }
            }
        }

        public const int OFFSET_idxPackageFile = 0;
        public const int OFFSET_idxClassName = 8;
        public const int OFFSET_idxLink = 16;
        public const int OFFSET_idxObjectName = 20;
        public const int OFFSET_indexValue = 24;

        /// <summary>
        /// Returns a clone of the header for modifying
        /// </summary>
        /// <returns></returns>
        public byte[] GetHeader()
        {
            return _header.ArrayClone();
        }

        public bool HasParent => FileRef.IsEntry(idxLink);

        public IEntry Parent
        {
            get => FileRef.GetEntry(idxLink);
            set => idxLink = value?.UIndex ?? 0;
        }

        private int idxPackageFile
        {
            get => EndianReader.ToInt32(_header.AsSpan(OFFSET_idxPackageFile), FileRef.Endian);
            set
            {
                EndianBitConverter.WriteAsBytes(value, _header.AsSpan(OFFSET_idxPackageFile), FileRef.Endian);
                HeaderChanged = true;
            }
        }
        //int PackageNameNumber
        private int idxClassName
        {
            get => EndianReader.ToInt32(_header.AsSpan(OFFSET_idxClassName), FileRef.Endian);
            set
            {
                EndianBitConverter.WriteAsBytes(value, _header.AsSpan(OFFSET_idxClassName), FileRef.Endian);
                HeaderChanged = true;
            }
        }
        //int ClassNameNumber
        public int idxLink
        {
            get => EndianReader.ToInt32(_header.AsSpan(OFFSET_idxLink), FileRef.Endian);
            set
            {
                // HeaderOffset = 0 means this was instantiated and not read in from a stream
                if (value == UIndex && HeaderOffset != 0)
                {
                    throw new Exception("Cannot set import link to itself, this will cause infinite recursion");
                }
                EndianBitConverter.WriteAsBytes(value, _header.AsSpan(OFFSET_idxLink), FileRef.Endian);
                HeaderChanged = true;
                FileRef.InvalidateLookupTable();
            }
        }
        private int idxObjectName
        {
            get => EndianReader.ToInt32(_header.AsSpan(OFFSET_idxObjectName), FileRef.Endian);
            set
            {
                EndianBitConverter.WriteAsBytes(value, _header.AsSpan(OFFSET_idxObjectName), FileRef.Endian);
                HeaderChanged = true;
                FileRef.InvalidateLookupTable();
            }
        }
        public int indexValue
        {
            get => EndianReader.ToInt32(_header.AsSpan(OFFSET_indexValue), FileRef.Endian);
            set
            {
                EndianBitConverter.WriteAsBytes(value, _header.AsSpan(OFFSET_indexValue), FileRef.Endian);
                HeaderChanged = true;
                FileRef.InvalidateLookupTable();
            }
        }


        public string ClassName
        {
            get => FileRef.Names[idxClassName];
            set => idxClassName = FileRef.FindNameOrAdd(value);
        }

        public string ObjectNameString
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
        public string InstancedFullPath => FileRef.IsEntry(idxLink) ? ObjectName.AddToPath(ParentInstancedFullPath) : ObjectName.Instanced;

        bool headerChanged;
        public bool HeaderChanged
        {
            get => headerChanged;

            set
            {
                headerChanged = value;
                EntryHasPendingChanges |= value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HeaderChanged)));
            }
        }


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

        public ImportEntry Clone(int newIndex = -1)
        {
            ImportEntry newImport = (ImportEntry)MemberwiseClone();
            newImport.Header = Header.ArrayClone();
            if (newIndex >= 0)
            {
                EndianBitConverter.WriteAsBytes(newIndex, _header.AsSpan(OFFSET_indexValue), FileRef.Endian);
            }
            return newImport;
        }

        IEntry IEntry.Clone(bool incrementIndex)
        {
            if (incrementIndex)
            {
                return Clone(FileRef.GetNextIndexForInstancedName(this));
            }

            return Clone();
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
