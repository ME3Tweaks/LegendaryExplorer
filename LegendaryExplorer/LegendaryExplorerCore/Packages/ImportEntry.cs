using System;
using System.Buffers.Binary;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using PropertyChanged;

namespace LegendaryExplorerCore.Packages
{
    [DebuggerDisplay("ImportEntry | {UIndex} = {InstancedFullPath}")]
    [DoNotNotify] //disable Fody/PropertyChanged for this class. Do notification manually
    public sealed class ImportEntry : IEntry
    {
        public MEGame Game => FileRef.Game;

        /// <summary>
        /// Generates an import by serializing it from the specified package file and data stream.
        /// </summary>
        /// <param name="pccFile"></param>
        /// <param name="importData"></param>
        public ImportEntry(IMEPackage pccFile, EndianReader importData)
        {
            HeaderOffset = importData.Position;
            FileRef = pccFile;
            importData.Read(MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref _header, 1)));
            if (!pccFile.Endian.IsNative)
            {
                _header.ReverseEndianness();
            }
        }

        /// <summary>
        /// Creates an import that would represent the specified export if it was to be placed in the specified fakeDestPackage.
        /// </summary>
        /// <param name="sourceExport">Export to convert. The link in the dest package must exist or be the root.</param>
        /// <param name="fakeDestPackage">Package to associate this object with. The import is not installed to the import table.</param>
        public ImportEntry(ExportEntry sourceExport, int parentIdx, IMEPackage fakeDestPackage)
        {
            FileRef = fakeDestPackage;
            idxLink = parentIdx;
            ClassName = sourceExport.ClassName;
            ObjectName = sourceExport.ObjectName;
            PackageFile = GetPackageFile(sourceExport); // may want to use sourceExport as this may not have yet been attached to package 
        }

        /// <summary>
        /// Looks up the class info for the given class and returns which package file should contain it. Use this for the PackageFile attribute on ImportEntries.
        /// </summary>
        /// <param name="game"></param>
        /// <param name="className"></param>
        /// <returns></returns>
        public static string GetPackageFile(MEGame game, string className)
        {
            var classInfo = GlobalUnrealObjectInfo.GetClassOrStructInfo(game, className);
            if (classInfo != null)
            {
                return Path.GetFileNameWithoutExtension(classInfo.pccPath).UpperFirst();
            }
            else
            {
                return @"Core"; // ?? This could be engine, sfxgame...
            }
        }

        /// <summary>
        /// Looks up the class info for the given class and returns which package file should contain it. Use this for the PackageFile attribute on ImportEntries.
        /// </summary>
        /// <param name="game"></param>
        /// <param name="className"></param>
        /// <returns></returns>
        public static string GetPackageFile(ExportEntry entry)
        {
            var entryClass = entry.Class;
            if (entryClass == null)
                return @"Core"; // Class is defined in Core
            if (entryClass.HasParent)
            {
                // ForcedExport parent? Take the first forced export?
                return entryClass.InstancedFullPath.Split(".").First();
            }
            
            return GetPackageFile(entry.Game, entry.ClassName);
        }

        /// <summary>
        /// Creates an empty import associated with the specified package file.
        /// </summary>
        /// <param name="pccFile"></param>
        public ImportEntry(IMEPackage pccFile)
        {
            FileRef = pccFile;
        }

        /// <summary>
        /// Generates a new ImportEntry based on the provided import entry, but associated with the listed package. This is useful for testing imports in other packages.
        /// </summary>
        /// <param name="pccFile">Package to associate with</param>
        /// <param name="clone">Import to read data out of</param>
        public ImportEntry(IMEPackage pccFile, ImportEntry clone)
        {
            FileRef = pccFile;
            if (clone.idxLink != 0)
            {
                var link = pccFile.FindEntry(clone.ParentInstancedFullPath);
                //if (link == null)
                //    Debugger.Break(); // We are generating an import that is not the same as the original!!

                idxLink = link?.UIndex ?? 0;
            }
            else
            {
                idxLink = 0; // root level like SFXGame
            }

            ObjectName = clone.ObjectName;
            PackageFile = clone.PackageFile;
            ClassName = clone.ClassName;
        }

        public ImportEntry(IMEPackage pccFile, IEntry parent, NameReference name) : this(pccFile, parent?.UIndex ?? 0, name) { }
        public ImportEntry(IMEPackage pccFile, int parentUindex, NameReference name)
        {
            FileRef = pccFile;
            _header.Link = parentUindex;
            _header.ObjectNameIndex = FileRef.FindNameOrAdd(name.Name);
            _header.ObjectNameNumber = name.Number;
        }

        public long HeaderOffset { get; set; }

        public int Index { private get; set; }
        public int UIndex => -Index - 1;

        public IMEPackage FileRef { get; }

        public const int HeaderLength = 28;

        [StructLayout(LayoutKind.Sequential)]
        private struct ImportHeader
        {
            public int PackageFileNameIndex;
            public int PackageFileNameNumber;
            public int ClassNameIndex;
            public int ClassNameNumber;
            public int Link;
            public int ObjectNameIndex;
            public int ObjectNameNumber;

            public void ReverseEndianness()
            {
                PackageFileNameIndex = BinaryPrimitives.ReverseEndianness(PackageFileNameIndex);
                PackageFileNameNumber = BinaryPrimitives.ReverseEndianness(PackageFileNameNumber);
                ClassNameIndex = BinaryPrimitives.ReverseEndianness(ClassNameIndex);
                ClassNameNumber = BinaryPrimitives.ReverseEndianness(ClassNameNumber);
                Link = BinaryPrimitives.ReverseEndianness(Link);
                ObjectNameIndex = BinaryPrimitives.ReverseEndianness(ObjectNameIndex);
                ObjectNameNumber = BinaryPrimitives.ReverseEndianness(ObjectNameNumber);
            }
        }

        private ImportHeader _header;

        /// <summary>
        /// Get generates the header, Set deserializes all the header values from the provided byte array
        /// </summary>
        public byte[] Header
        {
            get => GenerateHeader();
            set => SetHeaderValuesFromByteArray(value);
        }

        public void SetHeaderValuesFromByteArray(byte[] value)
        {
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value), "Cannot set Header to a null value");
            }
            if (value.Length != HeaderLength)
            {
                throw new ArgumentException(nameof(value), $"Import header must be exactly {HeaderLength} bytes");
            }
            var existingHeader = GenerateHeader();
            if (existingHeader.AsSpan().SequenceEqual(value))
            {
                return; //if the data is the same don't write it and trigger the side effects
            }

            _header = MemoryMarshal.Read<ImportHeader>(value);
            if (!FileRef.Endian.IsNative)
            {
                _header.ReverseEndianness();
            }
            //new header may have changed link or name
            FileRef.InvalidateLookupTable();

            FileRef.IsModified = true; // mark package as modified if the existing header is changing.
            HeaderChanged = true;
        }

        public const int OFFSET_idxPackageFile = 0;
        public const int OFFSET_idxClassName = 8;
        public const int OFFSET_idxLink = 16;
        public const int OFFSET_idxObjectName = 20;
        public const int OFFSET_indexValue = 24;

        /// <summary>
        /// Generates the header byte array
        /// </summary>
        /// <returns></returns>
        public byte[] GenerateHeader()
        {
            var buff = new byte[HeaderLength];
            if (FileRef.Endian.IsNative)
            {
                MemoryMarshal.Write(buff, ref _header);
            }
            else
            {
                var reversedHeader = _header;
                reversedHeader.ReverseEndianness();
                MemoryMarshal.Write(buff, ref _header);
            }
            return buff;
        }

        public void SerializeHeader(Stream stream)
        {
            stream.Write(MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref _header, 1)));
        }

        public bool HasParent => FileRef.IsEntry(idxLink);

        public IEntry Parent
        {
            get => FileRef.GetEntry(idxLink);
            set => idxLink = value?.UIndex ?? 0;
        }

        private int idxPackageFile
        {
            get => _header.PackageFileNameIndex;
            set
            {
                if (_header.PackageFileNameIndex != value)
                {
                    if (HeaderOffset != 0 && value == FileRef.findName("None"))
                    {
                        throw new Exception("Cannot set PackageFile to none");
                    }

                    _header.PackageFileNameIndex = value;
                    HeaderChanged = true;
                }
            }
        }
        //int PackageNameNumber
        private int idxClassName
        {
            get => _header.ClassNameIndex;
            set
            {
                if (_header.ClassNameIndex != value)
                {
                    _header.ClassNameIndex = value;
                    HeaderChanged = true;
                }
            }
        }
        //int ClassNameNumber
        public int idxLink
        {
            get => _header.Link;
            set
            {
                if (_header.Link != value)
                {
                    // HeaderOffset = 0 means this was instantiated and not read in from a stream
                    if (value == UIndex && HeaderOffset != 0)
                    {
                        throw new Exception("Cannot set import link to itself, this will cause infinite recursion");
                    }
                    _header.Link = value;
                    HeaderChanged = true;
                    FileRef.InvalidateLookupTable();
                }
            }
        }
        private int idxObjectName
        {
            get => _header.ObjectNameIndex;
            set
            {
                if (_header.ObjectNameIndex != value)
                {
                    _header.ObjectNameIndex = value;
                    HeaderChanged = true;
                    FileRef.InvalidateLookupTable();
                }
            }
        }
        public int indexValue
        {
            get => _header.ObjectNameNumber;
            set
            {
                if (_header.ObjectNameNumber != value)
                {
                    _header.ObjectNameNumber = value;
                    HeaderChanged = true;
                    FileRef.InvalidateLookupTable();
                }
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

        public string FullPath => FileRef.IsEntry(idxLink) ? $"{ParentFullPath}.{ObjectNameString}" : ObjectNameString;

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
            if (newIndex >= 0)
            {
                _header.ObjectNameIndex = newIndex;
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

        /// <summary>
        /// Gets the top level object by following the idxLink up the chain. Typically this is the file that will contain the export (unless it is a ForcedExport) if it's an import, or the original package before forcing the export into the file.
        /// </summary>
        /// <returns></returns>
        public string GetRootName()
        {
            IEntry current = this;
            while (current.Parent != null)
            {
                current = current.Parent;
            }
            return current.InstancedFullPath;
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
