using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Memory;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using PropertyChanged;
using static LegendaryExplorerCore.Unreal.UnrealFlags;

namespace LegendaryExplorerCore.Packages
{
    [DebuggerDisplay("{Game} ExportEntry | {UIndex} {ObjectName.Instanced}({ClassName}) in {System.IO.Path.GetFileName(FileRef.FilePath)}")]
    [DoNotNotify]//disable Fody/PropertyChanged for this class. Do notification manually
    public class ExportEntry : INotifyPropertyChanged, IEntry
    {
        public IMEPackage FileRef { get; }

        public MEGame Game => FileRef.Game;

        public int Index { private get; set; } = -1;
        public int UIndex => Index + 1;

        /// <summary>
        /// Constructor for generating a new export entry
        /// </summary>
        /// <param name="file"></param>
        /// <param name="parent"></param>
        /// <param name="name"></param>
        /// <param name="prePropBinary"></param>
        /// <param name="properties"></param>
        /// <param name="binary"></param>
        /// <param name="isClass"></param>
        public ExportEntry(IMEPackage file, IEntry parent, NameReference name, byte[] prePropBinary = null, PropertyCollection properties = null, ObjectBinary binary = null, bool isClass = false) :
            this(file, parent?.UIndex ?? 0, name, prePropBinary, properties, binary, isClass) { }

        /// <summary>
        /// Constructor for generating a new export entry
        /// </summary>
        /// <param name="file"></param>
        /// <param name="parentUIndex"></param>
        /// <param name="name"></param>
        /// <param name="prePropBinary"></param>
        /// <param name="properties"></param>
        /// <param name="binary"></param>
        /// <param name="isClass"></param>
        public ExportEntry(IMEPackage file, int parentUIndex, NameReference name, byte[] prePropBinary = null, PropertyCollection properties = null, ObjectBinary binary = null, bool isClass = false)
        {
            FileRef = file;
            OriginalDataSize = 0;
            _header = new byte[HasComponentMap ? 72 : 68];
            EndianBitConverter.WriteAsBytes(parentUIndex, _header.AsSpan(OFFSET_idxLink), FileRef.Endian);
            EndianBitConverter.WriteAsBytes(FileRef.FindNameOrAdd(name.Name), _header.AsSpan(OFFSET_idxObjectName), FileRef.Endian);
            EndianBitConverter.WriteAsBytes(name.Number, _header.AsSpan(OFFSET_indexValue), FileRef.Endian);
            DataOffset = 0;
            ObjectFlags = EObjectFlags.LoadForClient | EObjectFlags.LoadForServer | EObjectFlags.LoadForEdit; //sensible defaults?

            var ms = new EndianReader { Endian = file.Endian };
            prePropBinary ??= new byte[4];
            ms.Writer.WriteFromBuffer(prePropBinary);
            if (!isClass)
            {
                properties ??= new PropertyCollection();
                properties.WriteTo(ms.Writer, file);
            }

            binary?.WriteTo(ms.Writer, file);

            _data = ms.ToArray();
            DataSize = _data.Length;
        }

        /// <summary>
        /// Constructor for generating a new export entry
        /// </summary>
        /// <param name="file"></param>
        /// <param name="header"></param>
        /// <param name="prePropBinary"></param>
        /// <param name="properties"></param>
        /// <param name="binary"></param>
        /// <param name="isClass"></param>
        public ExportEntry(IMEPackage file, byte[] header, byte[] prePropBinary = null, PropertyCollection properties = null, ObjectBinary binary = null, bool isClass = false)
        {
            FileRef = file;
            OriginalDataSize = 0;
            _header = header;
            DataOffset = 0;

            var ms = new EndianReader { Endian = file.Endian };
            prePropBinary ??= new byte[4];
            ms.Writer.WriteFromBuffer(prePropBinary);
            if (!isClass)
            {
                properties ??= new PropertyCollection();
                properties.WriteTo(ms.Writer, file);
            }

            binary?.WriteTo(ms.Writer, file);

            _data = ms.ToArray();
            DataSize = _data.Length;
        }

        /// <summary>
        /// Constructor for reading an export from the package
        /// </summary>
        /// <param name="file"></param>
        /// <param name="stream"></param>
        /// <param name="readData">should export data be read from the stream</param>
        public ExportEntry(IMEPackage file, EndianReader stream, bool readData = true)
        {
            FileRef = file;
            OriginalDataSize = 0;
            HeaderOffset = (int)stream.Position;
            switch (file.Game)
            {
                case MEGame.ME1 when file.Platform == MEPackage.GamePlatform.Xenon:
                    {

                        long start = stream.Position;
                        //Debug.WriteLine($"Export header pos {start:X8}");
                        stream.Seek(0x28, SeekOrigin.Current);
                        int componentMapCount = stream.ReadInt32();
                        //Debug.WriteLine("Component count: " + componentMapCount);
                        stream.Seek(4 + componentMapCount * 12, SeekOrigin.Current);
                        int generationsNetObjectsCount = stream.ReadInt32();
                        //Debug.WriteLine("GenObjs count: " + generationsNetObjectsCount);

                        stream.Seek(16, SeekOrigin.Current); // skip guid size
                        stream.Seek(generationsNetObjectsCount * 4, SeekOrigin.Current);
                        long end = stream.Position;
                        stream.Seek(start, SeekOrigin.Begin);
                        var len = (end - start);
                        //Debug.WriteLine($"Len: {len:X2}");
                        //read header
                        _header = stream.ReadBytes((int)len);
                        break;
                    }
                case MEGame.ME1 when file.Platform == MEPackage.GamePlatform.PC:
                case MEGame.ME2 when file.Platform != MEPackage.GamePlatform.PS3:
                    {

                        long start = stream.Position;
                        stream.Seek(0x28, SeekOrigin.Current);
                        int componentMapCount = stream.ReadInt32();
                        stream.Seek(4 + componentMapCount * 12, SeekOrigin.Current);
                        int generationsNetObjectsCount = stream.ReadInt32();
                        stream.Seek(16, SeekOrigin.Current);
                        stream.Seek(4 + generationsNetObjectsCount * 4, SeekOrigin.Current);
                        long end = stream.Position;
                        stream.Seek(start, SeekOrigin.Begin);
                        //read header
                        _header = stream.ReadBytes((int)(end - start));
                        break;
                    }
                case MEGame.ME1 when file.Platform == MEPackage.GamePlatform.PS3:
                case MEGame.ME2 when file.Platform == MEPackage.GamePlatform.PS3:
                case MEGame.ME3:
                case MEGame.LE1:
                case MEGame.LE2:
                case MEGame.LE3:
                case MEGame.UDK:
                {
                        stream.Seek(44, SeekOrigin.Current);
                        int count = stream.ReadInt32();
                        stream.Seek(-48, SeekOrigin.Current);

                        int expInfoSize = 68 + (count * 4);
                        _header = stream.ReadBytes(expInfoSize);
                        break;
                    }
                default:
                    throw new ArgumentOutOfRangeException();
            }
            OriginalDataSize = DataSize;
            if (readData)
            {
                long headerEnd = stream.Position;

                stream.Seek(DataOffset, SeekOrigin.Begin);
                _data = stream.ReadBytes(DataSize);
                stream.Seek(headerEnd, SeekOrigin.Begin);
            }
        }

        public bool HasStack => ObjectFlags.Has(EObjectFlags.HasStack);

        public byte[] GetPrePropBinary()
        {
            return _data.Slice(0, GetPropertyStart());
        }

        public void SetPrePropBinary(byte[] bytes)
        {
            if (Game >= MEGame.ME3 && ClassName == "DominantDirectionalLightComponent" || ClassName == "DominantSpotLightComponent")
            {
                int minLen = (IsDefaultObject ? 8 : 12);
                if (bytes.Length < minLen)
                {
                    throw new ArgumentException($"Expected pre-property binary to be at least {minLen} bytes, not {bytes.Length}!", nameof(bytes));
                }
                int oldLen = GetPropertyStart();
                int count = EndianReader.ToInt32(bytes, 0, FileRef.Endian);
                minLen += count * 2;
                if (bytes.Length < minLen)
                {
                    throw new ArgumentException($"Expected pre-property binary to be {minLen} bytes, not {bytes.Length}!", nameof(bytes));
                }

                using var ms = MemoryManager.GetMemoryStream();
                ms.WriteFromBuffer(bytes);
                ms.Write(_data, oldLen, _data.Length - oldLen);
                Data = ms.ToArray();
            }
            else
            {
                if (bytes.Length != GetPropertyStart())
                {
                    throw new ArgumentException($"Expected pre-property binary to be {GetPropertyStart()} bytes, not {bytes.Length}!", nameof(bytes));
                }
                byte[] data = Data;
                Buffer.BlockCopy(bytes, 0, data, 0, bytes.Length);
                Data = data;
            }
        }

        //should only have to check the flag, but custom mod classes might not have set it properly
        public bool IsDefaultObject => ObjectFlags.Has(EObjectFlags.ClassDefaultObject);

        private byte[] _header;

        /// <summary>
        /// The underlying header is directly returned by this getter. If you want to write a new header back, use the copy provided by getHeader()!
        /// Otherwise some events may not trigger
        /// </summary>
        public byte[] Header
        {
            get => _header;
            set
            {
                if (_header != null && value != null && _header.AsSpan().SequenceEqual(value))
                {
                    return; //if the data is the same don't write it and trigger the side effects
                }

                int dataSize = _header != null ? DataSize : (_data?.Length ?? 0);
                _header = value;
                DataSize = dataSize; //should never be altered by Header overwrite

                //new header may have changed link or name
                FileRef.InvalidateLookupTable();

                EntryHasPendingChanges = true;
                HeaderChanged = true;
                // This is a hack cause FileRef is IMEPackage
                FileRef.IsModified = true;
            }
        }

        public const int OFFSET_idxClass = 0;
        public const int OFFSET_idxSuperClass = 4;
        public const int OFFSET_idxLink = 8;
        public const int OFFSET_idxObjectName = 12;
        public const int OFFSET_indexValue = 16;
        public const int OFFSET_idxArchetype = 20;
        public const int OFFSET_ObjectFlags = 24;
        public const int OFFSET_DataSize = 32;
        public const int OFFSET_DataOffset = 36;

        /// <summary>
        /// Returns a clone of the header for modifying
        /// </summary>
        /// <returns></returns>
        public byte[] GetHeader()
        {
            return _header.ArrayClone();
        }

        public byte[] GenerateHeader(MEGame game, bool clearComponentMap = false) => GenerateHeader(null, null, game == MEGame.ME1 || game == MEGame.ME2, clearComponentMap);

        public void RegenerateHeader(MEGame game, bool clearComponentMap = false) => Header = GenerateHeader(game, clearComponentMap);

        private byte[] GenerateHeader(OrderedMultiValueDictionary<NameReference, int> componentMap, int[] generationNetObjectCount, bool? hasComponentMap = null, bool clearComponentMap = false)
        {
            using var bin = MemoryManager.GetMemoryStream(36 * 4); // This should hopefully ensure we don't have extra allocations, assuming 5 component items
            bin.WriteInt32(idxClass);
            bin.WriteInt32(idxSuperClass);
            bin.WriteInt32(idxLink);
            bin.WriteInt32(idxObjectName);
            bin.WriteInt32(indexValue);
            bin.WriteInt32(idxArchetype);
            bin.WriteUInt64((ulong)ObjectFlags);
            bin.WriteInt32(DataSize);
            bin.WriteInt32(DataOffset);
            if (hasComponentMap ?? HasComponentMap)
            {
                if (clearComponentMap)
                {
                    bin.WriteInt32(0);
                }
                else
                {
                    OrderedMultiValueDictionary<NameReference, int> cmpMap = componentMap ?? ComponentMap;
                    bin.WriteInt32(cmpMap.Count);
                    foreach ((NameReference name, int uIndex) in cmpMap)
                    {
                        bin.WriteInt32(FileRef.FindNameOrAdd(name.Name));
                        bin.WriteInt32(name.Number);
                        bin.WriteInt32(uIndex); // 0-based index
                    }
                }
            }
            bin.WriteUInt32((uint)ExportFlags);
            int[] genobjCounts = generationNetObjectCount ?? GenerationNetObjectCount;
            bin.WriteInt32(genobjCounts.Length);
            foreach (int count in genobjCounts)
            {
                bin.WriteInt32(count);
            }
            bin.WriteGuid(PackageGUID);
            bin.WriteUInt32((uint)PackageFlags);
            return bin.ToArray();
        }

        private void RegenerateHeader(OrderedMultiValueDictionary<NameReference, int> componentMap, int[] generationNetObjectCount, bool? hasComponentMap = null)
        {
            Header = GenerateHeader(componentMap, generationNetObjectCount, hasComponentMap, componentMap is null);
        }

        public int HeaderOffset { get; set; }

        public int idxClass
        {
            get => EndianReader.ToInt32(_header.AsSpan(OFFSET_idxClass), FileRef.Endian);
            private set
            {
                if (value != idxClass)
                {
                    EndianBitConverter.WriteAsBytes(value, _header.AsSpan(OFFSET_idxClass), FileRef.Endian);
                    HeaderChanged = true;
                }
            }
        }

        public int idxSuperClass
        {
            get => EndianReader.ToInt32(_header.AsSpan(OFFSET_idxSuperClass), FileRef.Endian);
            private set
            {
                if (value != idxSuperClass)
                {
                    // 0 check for setup
                    if (UIndex != 0 && value == UIndex)
                    {
                        throw new Exception("Cannot set export superclass to itself, this will cause infinite recursion");
                    }
                    EndianBitConverter.WriteAsBytes(value, _header.AsSpan(OFFSET_idxSuperClass), FileRef.Endian);
                    HeaderChanged = true;
                }
            }
        }

        public int idxLink
        {
            get => EndianReader.ToInt32(_header.AsSpan(OFFSET_idxLink), FileRef.Endian);
            set
            {
                if (value != idxLink)
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
        }

        private int idxObjectName
        {
            get => EndianReader.ToInt32(_header.AsSpan(OFFSET_idxObjectName), FileRef.Endian);
            set
            {
                if (value != idxObjectName)
                {
                    EndianBitConverter.WriteAsBytes(value, _header.AsSpan(OFFSET_idxObjectName), FileRef.Endian);
                    HeaderChanged = true;
                    FileRef.InvalidateLookupTable();
                }
            }
        }

        public int indexValue
        {
            get => EndianReader.ToInt32(_header.AsSpan(OFFSET_indexValue), FileRef.Endian);
            set
            {
                if (value != indexValue)
                {
                    EndianBitConverter.WriteAsBytes(value, _header.AsSpan(OFFSET_indexValue), FileRef.Endian);
                    HeaderChanged = true;
                    FileRef.InvalidateLookupTable();
                }
            }
        }

        public int idxArchetype
        {
            get => EndianReader.ToInt32(_header.AsSpan(OFFSET_idxArchetype), FileRef.Endian);
            private set
            {
                if (value != idxArchetype)
                {
                    EndianBitConverter.WriteAsBytes(value, _header.AsSpan(OFFSET_idxArchetype), FileRef.Endian);
                    HeaderChanged = true;
                }
            }
        }

        public EObjectFlags ObjectFlags
        {
            get => (EObjectFlags)EndianReader.ToUInt64(_header.AsSpan(OFFSET_ObjectFlags), FileRef.Endian);
            set
            {
                if (value != ObjectFlags)
                {
                    EndianBitConverter.WriteAsBytes((ulong)value, _header.AsSpan(OFFSET_ObjectFlags), FileRef.Endian);
                    HeaderChanged = true;
                }
            }
        }

        public int DataSize
        {
            get => EndianReader.ToInt32(_header.AsSpan(OFFSET_DataSize), FileRef.Endian);
            private set => EndianBitConverter.WriteAsBytes(value, _header.AsSpan(OFFSET_DataSize), FileRef.Endian);
        }

        public int DataOffset
        {
            get => EndianReader.ToInt32(_header.AsSpan(OFFSET_DataOffset), FileRef.Endian);
            set => EndianBitConverter.WriteAsBytes(value, _header.AsSpan(OFFSET_DataOffset), FileRef.Endian);
        }

        public bool HasComponentMap => FileRef.Game <= MEGame.ME2 && FileRef.Platform != MEPackage.GamePlatform.PS3;

        //me1 and me2 only
        public OrderedMultiValueDictionary<NameReference, int> ComponentMap
        {
            get
            {
                var componentMap = new OrderedMultiValueDictionary<NameReference, int>();
                if (!HasComponentMap) return componentMap;
                int count = EndianReader.ToInt32(_header, 40, FileRef.Endian);
                for (int i = 0; i < count; i++)
                {
                    int pairIndex = 44 + i * 12;
                    string name = FileRef.GetNameEntry(EndianReader.ToInt32(_header, pairIndex, FileRef.Endian));
                    componentMap.Add(new NameReference(name, EndianReader.ToInt32(_header, pairIndex + 4, FileRef.Endian)),
                        EndianReader.ToInt32(_header, pairIndex + 8, FileRef.Endian));
                }
                return componentMap;
            }
            set
            {
                if (!HasComponentMap) return;
                RegenerateHeader(value, null);
            }
        }

        public int ExportFlagsOffset => HasComponentMap ? 44 + EndianReader.ToInt32(_header, 40, FileRef.Endian) * 12 : 40;

        public EExportFlags ExportFlags
        {
            get => (EExportFlags)EndianReader.ToUInt32(_header.AsSpan(ExportFlagsOffset), FileRef.Endian);
            set
            {
                if (value != ExportFlags)
                {
                    EndianBitConverter.WriteAsBytes((uint)value, _header.AsSpan(ExportFlagsOffset), FileRef.Endian);
                    HeaderChanged = true;
                }
            }
        }

        public int[] GenerationNetObjectCount
        {
            get
            {
                int count = EndianReader.ToInt32(_header.AsSpan(ExportFlagsOffset + 4), FileRef.Endian);
                var result = new int[count];
                for (int i = 0; i < count; i++)
                {
                    result[i] = EndianReader.ToInt32(_header.AsSpan(ExportFlagsOffset + 8 + i * 4), FileRef.Endian);
                }
                return result;
            }
            set => RegenerateHeader(null, value);
        }

        public int PackageGuidOffset => ExportFlagsOffset + 8 + EndianReader.ToInt32(_header.AsSpan(ExportFlagsOffset + 4), FileRef.Endian) * 4;

        public Guid PackageGUID
        {
            get => EndianReader.ToGuid(_header.AsSpan(PackageGuidOffset, 16), FileRef.Endian);
            set
            {
                if (value != PackageGUID)
                {
                    EndianBitConverter.WriteAsBytes(value, _header.AsSpan(PackageGuidOffset), FileRef.Endian);
                    HeaderChanged = true;
                }
            }
        }

        public EPackageFlags PackageFlags
        {
            get => (EPackageFlags)EndianReader.ToUInt32(_header.AsSpan(PackageGuidOffset + 16), FileRef.Endian);
            set
            {
                if (value != PackageFlags)
                {
                    EndianBitConverter.WriteAsBytes((uint)value, _header.AsSpan(PackageGuidOffset + 16), FileRef.Endian);
                    HeaderChanged = true;
                }
            }
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

        public string ClassName => Class?.ObjectName.Name ?? "Class";

        public string SuperClassName => SuperClass?.ObjectName.Name ?? "Class";

        public string ParentName => FileRef.GetEntry(idxLink)?.ObjectName ?? "";

        public string ParentFullPath => FileRef.GetEntry(idxLink)?.FullPath ?? "";

        public string FullPath => FileRef.IsEntry(idxLink) ? $"{ParentFullPath}.{ObjectName.Name}" : ObjectName.Name;

        public string ParentInstancedFullPath => FileRef.GetEntry(idxLink)?.InstancedFullPath ?? "";
        public string InstancedFullPath => FileRef.IsEntry(idxLink) ? ObjectName.AddToPath(ParentInstancedFullPath) : ObjectName.Instanced;

        public bool HasParent => FileRef.IsEntry(idxLink);

        public IEntry Parent
        {
            get => FileRef.GetEntry(idxLink);
            set => idxLink = value?.UIndex ?? 0;
        }

        public bool HasArchetype => FileRef.IsEntry(idxArchetype);

        public IEntry Archetype
        {
            get => FileRef.GetEntry(idxArchetype);
            set => idxArchetype = value?.UIndex ?? 0;
        }

        public bool HasSuperClass => FileRef.IsEntry(idxSuperClass);

        public IEntry SuperClass
        {
            get => FileRef.GetEntry(idxSuperClass);
            set => idxSuperClass = value?.UIndex ?? 0;
        }

        public bool IsClass => idxClass == 0;

        public IEntry Class
        {
            get => FileRef.GetEntry(idxClass);
            set => idxClass = value?.UIndex ?? 0;
        }

        //NEVER DIRECTLY SET THIS OUTSIDE OF CONSTRUCTOR!
        private byte[] _data;

        /// <summary>
        /// Returns a ReadOnlySpan of Data. This is much more efficient than cloning with Data.
        /// </summary>
        public ReadOnlySpan<byte> DataReadOnly => _data.AsSpan();

        /// <summary>
        /// RETURNS A CLONE
        /// </summary>
        public byte[] Data
        {
            //TODO: remove get accessor, and replace with GetDataCopy() to make behavior more obvious
            get => _data.ArrayClone();

            set
            {
                if (_data is null)
                {
                    //first time initialization
                    _data = value;
                    return;
                }
                if (value is null)
                {
                    throw new Exception("Cannot set an ExportEntry's Data to null!");
                }
                if (_data.AsSpan().SequenceEqual(value))
                {
                    return; //if the data is the same don't write it and trigger the side effects
                }

                _data = value;
                DataSize = value.Length;
                DataChanged = true;
                properties = null;
                propsEndOffset = null;
                EntryHasPendingChanges = true;
                FileRef.IsModified = true; // mark package as modified if the existing header is changing.
            }
        }

        public int OriginalDataSize { get; protected set; }

        bool dataChanged;

        public bool DataChanged
        {
            get => dataChanged;

            set
            {
                //This cannot be optimized as we cannot subscribe to array change events unfortunately

                //if (dataChanged != value)
                //{
                dataChanged = value;
                //    if (value)
                //    {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DataChanged)));
                //    }
            }
        }

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
                if (value != _entryHasPendingChanges)
                {
                    _entryHasPendingChanges = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(""));

                    EntryModifiedChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public event EventHandler EntryModifiedChanged;

        PropertyCollection properties;

        /// <summary>
        /// Gets properties of an export. You can force it to reload which is useful when debugging the property engine.
        /// </summary>
        /// <param name="forceReload">Forces full property release rather than using the property collection cache</param>
        /// <param name="includeNoneProperties">Include NoneProperties in the resulting property collection</param>
        /// <returns></returns>
        public PropertyCollection GetProperties(bool forceReload = false, bool includeNoneProperties = false, int propStartPos = 0, PackageCache packageCache = null)
        {
            if (properties != null && !forceReload && !includeNoneProperties)
            {
                return properties;
            }

            if (IsClass)
            {
                return new PropertyCollection { endOffset = 4, IsImmutable = true };
            } //no properties

            IEntry parsingClass = this;
            if (IsDefaultObject)
            {
                parsingClass = Class; //class we are defaults of
            }

            //if (!includeNoneProperties)
            //{
            //    int start = GetPropertyStart();
            //    MemoryStream stream = new MemoryStream(_data, false);
            //    stream.Seek(start, SeekOrigin.Current);
            //    return properties = PropertyCollection.ReadProps(this, stream, ClassName, false, false, this); //do not set properties as this may interfere with some other code. may change later.
            //}
            //else
            //{
            if (propStartPos == 0)
                propStartPos = GetPropertyStart();
            var stream = new MemoryStream(_data, false);
            stream.Seek(propStartPos, SeekOrigin.Current);
            // Do not cache
            return PropertyCollection.ReadProps(this, stream, ClassName, includeNoneProperties, true, parsingClass, packageCache); //do not set properties as this may interfere with some other code. may change later.
            //}
        }

        public void WriteProperties(PropertyCollection props)
        {
            var m = new EndianReader { Endian = FileRef.Endian };
            m.Writer.Write(_data, 0, GetPropertyStart());
            props.WriteTo(m.Writer, FileRef);
            int binStart = propsEnd();
            m.Writer.Write(_data, binStart, _data.Length - binStart);
            Data = m.ToArray();
        }

        public void WritePrePropsAndProperties(byte[] prePropBytes, PropertyCollection props, int binStart = -1)
        {
            // This does not properly work when porting assets across games
            var m = new EndianReader { Endian = FileRef.Endian };
            m.Writer.WriteBytes(prePropBytes);
            props.WriteTo(m.Writer, FileRef);
            binStart = binStart == -1 ? propsEnd() : binStart; // this allows us to precompute the starting position, which can avoid issues during relink as props may not have resolved yet
            m.Writer.Write(_data, binStart, _data.Length - binStart);
            Data = m.ToArray();
        }

        public int GetPropertyStart()
        {
            if (HasStack)
            {
                return stackLength;
            }

            int start = 0;

            
            if (!IsDefaultObject && this.IsA("Component") || (Game == MEGame.UDK && ClassName.EndsWith("Component")))
            {
                if (Game >= MEGame.ME3 && ClassName == "DominantDirectionalLightComponent" || ClassName == "DominantSpotLightComponent")
                {
                    //DominantLightShadowMap, which goes before everything for some reason
                    int count = EndianReader.ToInt32(_data, 0, FileRef.Endian);
                    start += count * 2 + 4;
                }
                start += 4; //TemplateOwnerClass
                if (ParentFullPath.Contains("Default__"))
                {
                    start += 8; //TemplateName
                }
            }

            start += 4; //NetIndex

            return start;
        }

        public int TemplateOwnerClassIdx
        {
            get
            {
                if (!IsDefaultObject && this.IsA("Component") || (Game == MEGame.UDK && ClassName.EndsWith("Component")))
                {
                    if (Game >= MEGame.ME3 && ClassName == "DominantDirectionalLightComponent" || ClassName == "DominantSpotLightComponent")
                    {
                        //DominantLightShadowMap, which goes before everything for some reason
                        int count = EndianReader.ToInt32(_data, 0, FileRef.Endian);
                        return count * 2 + 4;
                    }
                    return 0;
                }

                return -1;
            }
        }

        private int stackLength =>
            Game switch
            {
                MEGame.UDK => 26,
                MEGame.ME3 => 30,
                MEGame.LE1 => 30,
                MEGame.LE2 => 30, 
                MEGame.LE3 => 30, 
                MEGame.ME1 when FileRef.Platform == MEPackage.GamePlatform.PS3 => 30,
                MEGame.ME2 when FileRef.Platform == MEPackage.GamePlatform.PS3 => 30,
                _ => 32
            };

        public int NetIndex
        {
            get => EndianReader.ToInt32(_data, GetPropertyStart() - 4, FileRef.Endian);
            set
            {
                if (value != NetIndex)
                {
                    var data = Data;
                    data.OverwriteRange(GetPropertyStart() - 4, EndianBitConverter.GetBytes(value, FileRef.Endian));
                    Data = data;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(""));
                }
            }
        }

        private int? propsEndOffset;
        /// <summary>
        /// Gets the ending offset of the properties for this export, where binary data begins (if any). This call caches the position, the cached value is invalidated when the .Data attribute of this export is updated.
        /// </summary>
        /// <returns></returns>
        public int propsEnd()
        {
            propsEndOffset ??= GetProperties(true, true).endOffset;
            if (propsEndOffset.Value < 4) throw new Exception("Props end is less than 4!");
            return propsEndOffset.Value;
        }

        public byte[] GetBinaryData()
        {
            int start = propsEnd();
            return _data.Slice(start, _data.Length - start);
        }

        public MemoryStream GetReadOnlyBinaryStream()
        {
            int start = propsEnd();
            return new MemoryStream(_data, start, _data.Length - start, false);
        }

        public void WriteBinary(byte[] binaryData)
        {
            var m = new EndianReader { Endian = FileRef.Endian };
            m.Writer.Write(_data, 0, propsEnd());
            m.Writer.WriteBytes(binaryData);
            Data = m.ToArray();
        }

        public void WriteBinary(ObjectBinary bin)
        {
            var m = new EndianReader { Endian = FileRef.Endian };
            m.Writer.Write(_data, 0, propsEnd());
            bin.WriteTo(m.Writer, FileRef, DataOffset);
            Data = m.ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="props"></param>
        /// <param name="binary"></param>
        public void WritePropertiesAndBinary(PropertyCollection props, ObjectBinary binary)
        {
            var m = new EndianReader { Endian = FileRef.Endian };
            m.Writer.Write(_data, 0, GetPropertyStart());
            props?.WriteTo(m.Writer, FileRef); //props could be null if this is a class
            binary.WriteTo(m.Writer, FileRef, DataOffset);
            Data = m.ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="preProps"></param>
        /// <param name="props"></param>
        public void WritePrePropsAndPropertiesAndBinary(byte[] preProps, PropertyCollection props, ObjectBinary binary)
        {
            var m = new EndianReader { Endian = FileRef.Endian };
            m.Writer.WriteBytes(preProps);
            props?.WriteTo(m.Writer, FileRef); //props could be null if this is a class
            binary.WriteTo(m.Writer, FileRef, DataOffset);
            Data = m.ToArray();
        }


        private ExportEntry(IMEPackage file)
        {
            FileRef = file;
            OriginalDataSize = 0;
        }
        public ExportEntry Clone(int newIndex = -1)
        {
            var clone = new ExportEntry(FileRef)
            {
                _header = _header.ArrayClone(),
                HeaderOffset = 0,
                Data = this.Data,
                DataOffset = 0
            };
            if (newIndex >= 0)
            {
                EndianBitConverter.WriteAsBytes(newIndex, clone._header.AsSpan(OFFSET_indexValue), FileRef.Endian);
            }
            return clone;
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

        /// <summary>
        /// Gets the property flags for this export. Returns null if this export does not have stack.
        /// </summary>
        /// <returns></returns>
        public EPropertyFlags? GetPropertyFlags()
        {
            if (HasStack)
            {
                return (EPropertyFlags)EndianReader.ToUInt64(_data, 0x18, FileRef.Endian);
            }
            return null;
        }

        /// <summary>
        /// Gets the top level container export by following the idxLink up the chain. Typically this is the file that will contain the export (unless it is a ForcedExport) if it's an import, or the original package before forcing the export into the file.
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
    }
}
