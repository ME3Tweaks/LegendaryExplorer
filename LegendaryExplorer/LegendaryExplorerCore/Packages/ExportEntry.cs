using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
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
    [DebuggerDisplay("{Game} ExportEntry | {UIndex} {ObjectName.Instanced}({ClassName}) in {System.IO.Path.GetFileName(_fileRef.FilePath)}")]
    [DoNotNotify]//disable Fody/PropertyChanged for this class. Do notification manually
    public sealed class ExportEntry : INotifyPropertyChanged, IEntry
    {
        private readonly IMEPackage _fileRef;
        public IMEPackage FileRef => _fileRef;

        public MEGame Game => _fileRef.Game;

        private int _uIndex;
        public int Index { set => _uIndex = value + 1; }

        public int UIndex => _uIndex;

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
        /// <param name="parent_uIndex"></param>
        /// <param name="name"></param>
        /// <param name="prePropBinary"></param>
        /// <param name="properties"></param>
        /// <param name="binary"></param>
        /// <param name="isClass"></param>
        public ExportEntry(IMEPackage file, int parent_uIndex, NameReference name, byte[] prePropBinary = null, PropertyCollection properties = null, ObjectBinary binary = null, bool isClass = false)
        {
            _fileRef = file;

            //these three must be written to the underlying values so as not to invalidate the lookuptable 
            _idxLink = parent_uIndex;
            _idxObjectName = _fileRef.FindNameOrAdd(name.Name);
            _indexValue = name.Number;
            _generationNetObjectCounts = Array.Empty<int>();
            if (HasComponentMap)
            {
                _componentMap = Array.Empty<byte>();
            }

            DataOffset = 0;
            ObjectFlags = EObjectFlags.LoadForClient | EObjectFlags.LoadForServer | EObjectFlags.LoadForEdit; //sensible defaults?

            var ms = new EndianWriter { Endian = file.Endian };
            if (prePropBinary == null)
            {
                ms.Write(stackalloc byte[4]);
            }
            else
            {
                ms.WriteFromBuffer(prePropBinary);
            }
            if (!isClass)
            {
                if (properties == null)
                {
                    ms.WriteNoneProperty(file);
                }
                else
                {
                    properties.WriteTo(ms, file);
                }
            }

            binary?.WriteTo(ms, file);

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
            _fileRef = file;
            DeserializeHeader(new EndianReader(header));
            DataOffset = 0;

            var ms = new EndianWriter { Endian = file.Endian };
            if (prePropBinary == null)
            {
                ms.Write(stackalloc byte[4]);
            }
            else
            {
                ms.WriteFromBuffer(prePropBinary);
            }
            if (!isClass)
            {
                if (properties == null)
                {
                    ms.WriteNoneProperty(file);
                }
                else
                {
                    properties.WriteTo(ms, file);
                }
            }

            binary?.WriteTo(ms, file);

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
            _fileRef = file;
            HeaderOffset = (int)stream.Position;
            DeserializeHeader(stream);
            if (readData)
            {
                long headerEnd = stream.Position;

                stream.Seek(DataOffset, SeekOrigin.Begin);
                _data = stream.ReadBytes(DataSize);
                stream.Seek(headerEnd, SeekOrigin.Begin);
            }
        }

        public bool HasStack => _objectFlags.Has(EObjectFlags.HasStack);

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
                int count = EndianReader.ToInt32(bytes, 0, _fileRef.Endian);
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
        
        public bool IsDefaultObject => _objectFlags.Has(EObjectFlags.ClassDefaultObject);



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
                throw new ArgumentNullException(nameof(value), "Cannot set Header to a null value!");
            }
            var existingHeader = GenerateHeader();
            if (existingHeader.AsSpan().SequenceEqual(value))
            {
                return; //if the data is the same don't write it and trigger the side effects
            }

            int dataSize = DataSize;
            DeserializeHeader(new EndianReader(value));
            DataSize = dataSize; //should never be altered by Header overwrite

            //new header may have changed link or name
            _fileRef.InvalidateLookupTable();

            EntryHasPendingChanges = true;
            HeaderChanged = true;
            // This is a hack cause _fileRef is IMEPackage
            _fileRef.IsModified = true;
        }

        public int HeaderLength
        {
            get
            {
                int length = OFFSET_DataOffset
                    + 4 //DataOffset
                    + 4 //ExportFlags
                    + 16 //PackageGUID
                    + 4 //GenerationNetObjectCount count
                    + 4 //PackageFlags
                    + _generationNetObjectCounts.Length * 4;
                if (HasComponentMap)
                {
                    length += 4 + _componentMap.Length;
                }
                return length;
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
        /// Generates the header byte array
        /// </summary>
        /// <returns></returns>
        public byte[] GenerateHeader()
        {
            return GenerateHeader(_fileRef);
        }
        
        public byte[] GenerateHeader(IMEPackage pcc, bool clearComponentMap = false)
        {
            using var bin = new EndianWriter(MemoryManager.GetMemoryStream(HeaderLength)) { Endian = pcc.Endian };
            bin.WriteInt32(idxClass);
            bin.WriteInt32(idxSuperClass);
            bin.WriteInt32(idxLink);
            bin.WriteInt32(idxObjectName);
            bin.WriteInt32(indexValue);
            bin.WriteInt32(idxArchetype);
            bin.WriteUInt64((ulong)_objectFlags);
            bin.WriteInt32(DataSize);
            bin.WriteInt32(DataOffset);
            if (pcc.Game <= MEGame.ME2 && pcc.Platform != MEPackage.GamePlatform.PS3)
            {
                if (clearComponentMap)
                {
                    bin.WriteInt32(0);
                }
                else
                {
                    byte[] componentMap = _componentMap ?? Array.Empty<byte>();
                    bin.WriteInt32(componentMap.Length / 12);
                    bin.Write(componentMap);
                }
            }
            bin.WriteUInt32((uint)ExportFlags);
            int[] genobjCounts = GenerationNetObjectCount;
            bin.WriteInt32(genobjCounts.Length);
            foreach (int count in genobjCounts)
            {
                bin.WriteInt32(count);
            }
            bin.WriteGuid(PackageGUID);
            if (pcc.Game != MEGame.ME1 || pcc.Platform != MEPackage.GamePlatform.Xenon)
            {
                bin.WriteUInt32((uint)PackageFlags);
            }
            return bin.ToArray();
        }

        private void DeserializeHeader(EndianReader stream)
        {
            _idxClass = stream.ReadInt32();
            _idxSuperClass = stream.ReadInt32();
            _idxLink = stream.ReadInt32();
            _idxObjectName = stream.ReadInt32();
            _indexValue = stream.ReadInt32();
            _idxArchetype = stream.ReadInt32();
            _objectFlags = (EObjectFlags)stream.ReadUInt64();
            DataSize = stream.ReadInt32();
            DataOffset = stream.ReadInt32();
            if (HasComponentMap)
            {
                _componentMap = stream.ReadBytes(stream.ReadInt32() * 12);
            }
            _exportFlags = (EExportFlags)stream.ReadUInt32();
            int count = stream.ReadInt32();
            _generationNetObjectCounts = new int[count];
            for (int i = 0; i < count; i++)
            {
                _generationNetObjectCounts[i] = stream.ReadInt32();
            }
            _packageGuid = stream.ReadGuid();
            if (Game != MEGame.ME1 || _fileRef.Platform != MEPackage.GamePlatform.Xenon)
            {
                _packageFlags = (EPackageFlags)stream.ReadUInt32();
            }
        }

        public void SerializeHeader(Stream bin)
        {
            bin.WriteInt32(_idxClass);
            bin.WriteInt32(_idxSuperClass);
            bin.WriteInt32(_idxLink);
            bin.WriteInt32(_idxObjectName);
            bin.WriteInt32(_indexValue);
            bin.WriteInt32(_idxArchetype);
            bin.WriteUInt64((ulong)_objectFlags);
            bin.WriteInt32(DataSize);
            bin.WriteInt32(DataOffset);
            if (HasComponentMap)
            {
                bin.WriteInt32(_componentMap.Length / 12);
                bin.Write(_componentMap);
            }
            bin.WriteUInt32((uint)_exportFlags);
            int[] genobjCounts = _generationNetObjectCounts;
            bin.WriteInt32(genobjCounts.Length);
            for (int i = 0; i < genobjCounts.Length; i++)
            {
                int count = genobjCounts[i];
                bin.WriteInt32(count);
            }
            bin.WriteGuid(_packageGuid);
            bin.WriteUInt32((uint)_packageFlags);
        }

        public int HeaderOffset { get; set; }

        private int _idxClass;
        public int idxClass
        {
            get => _idxClass;
            private set
            {
                if (value != _idxClass)
                {
                    _idxClass = value;
                    HeaderChanged = true;
                }
            }
        }

        private int _idxSuperClass;
        public int idxSuperClass
        {
            get => _idxSuperClass;
            private set
            {
                if (value != _idxSuperClass)
                {
                    // 0 check for setup
                    if (_uIndex != 0 && value == _uIndex)
                    {
                        throw new Exception("Cannot set export superclass to itself, this will cause infinite recursion");
                    }
                    _idxSuperClass = value;
                    HeaderChanged = true;
                }
            }
        }

        private int _idxLink;
        public int idxLink
        {
            get => _idxLink;
            set
            {
                if (value != _idxLink)
                {
                    // HeaderOffset = 0 means this was instantiated and not read in from a stream
                    if (value == _uIndex && HeaderOffset != 0)
                    {
                        throw new Exception("Cannot set import link to itself, this will cause infinite recursion");
                    }
                    _idxLink = value;
                    HeaderChanged = true;
                    _fileRef.InvalidateLookupTable();
                }
            }
        }

        private int _idxObjectName;
        private int idxObjectName
        {
            get => _idxObjectName;
            set
            {
                if (value != _idxObjectName)
                {
                    _idxObjectName = value;
                    HeaderChanged = true;
                    _fileRef.InvalidateLookupTable();
                }
            }
        }

        private int _indexValue;
        public int indexValue
        {
            get => _indexValue;
            set
            {
                if (value != _indexValue)
                {
                    _indexValue = value;
                    HeaderChanged = true;
                    _fileRef.InvalidateLookupTable();
                }
            }
        }

        private int _idxArchetype;
        public int idxArchetype
        {
            get => _idxArchetype;
            private set
            {
                if (value != _idxArchetype)
                {
                    _idxArchetype = value;
                    HeaderChanged = true;
                }
            }
        }

        private EObjectFlags _objectFlags;
        public EObjectFlags ObjectFlags
        {
            get => _objectFlags;
            set
            {
                if (value != _objectFlags)
                {
                    _objectFlags = value;
                    HeaderChanged = true;
                }
            }
        }

        public int DataSize;

        public int DataOffset;

        public bool HasComponentMap => _fileRef.Game <= MEGame.ME2 && _fileRef.Platform != MEPackage.GamePlatform.PS3;

        //me1 and me2 only
        private byte[] _componentMap;
        public OrderedMultiValueDictionary<NameReference, int> ComponentMap
        {
            get
            {
                var componentMap = new OrderedMultiValueDictionary<NameReference, int>();
                if (!HasComponentMap) return componentMap;
                for (int i = 0; i < _componentMap.Length; i += 12)
                {
                    string name = _fileRef.GetNameEntry(EndianReader.ToInt32(_componentMap, i, _fileRef.Endian));
                    componentMap.Add(new NameReference(name, EndianReader.ToInt32(_componentMap, i + 4, _fileRef.Endian)),
                        EndianReader.ToInt32(_componentMap, i + 8, _fileRef.Endian));
                }
                return componentMap;
            }
            set
            {
                if (!HasComponentMap) return;
                if (value is null)
                {
                    if (_componentMap.Length == 0)
                    {
                        return;
                    }
                    _componentMap = Array.Empty<byte>();
                    HeaderChanged = true;
                    return;
                }
                var bin = new MemoryStream(value.Count * 12);
                foreach ((NameReference name, int _uIndex) in value)
                {
                    bin.WriteInt32(_fileRef.FindNameOrAdd(name.Name));
                    bin.WriteInt32(name.Number);
                    bin.WriteInt32(_uIndex); // 0-based index
                }
                var newMap = bin.ToArray();
                if (!newMap.AsSpan().SequenceEqual(_componentMap))
                {
                    _componentMap = newMap;
                    HeaderChanged = true;
                }
            }
        }

        private EExportFlags _exportFlags;
        public EExportFlags ExportFlags
        {
            get => _exportFlags;
            set
            {
                if (value != _exportFlags)
                {
                    _exportFlags = value;
                    HeaderChanged = true;
                }
            }
        }

        private int[] _generationNetObjectCounts;
        public int[] GenerationNetObjectCount
        {
            get => _generationNetObjectCounts;
            set
            {
                if (value is null)
                {
                    throw new ArgumentNullException(nameof(value), $"{nameof(GenerationNetObjectCount)} cannot be null");
                }
                if (!value.AsSpan().SequenceEqual(_generationNetObjectCounts))
                {
                    _generationNetObjectCounts = value;
                    HeaderChanged = true;
                }
            }
        }

        private Guid _packageGuid;
        public Guid PackageGUID
        {
            get => _packageGuid;
            set
            {
                if (value != _packageGuid)
                {
                    _packageGuid = value;
                    HeaderChanged = true;
                }
            }
        }

        private EPackageFlags _packageFlags;
        public EPackageFlags PackageFlags
        {
            get => _packageFlags;
            set
            {
                if (value != _packageFlags)
                {
                    _packageFlags = value;
                    HeaderChanged = true;
                }
            }
        }

        public string ObjectNameString
        {
            get => _fileRef.Names[_idxObjectName];
            set => idxObjectName = _fileRef.FindNameOrAdd(value);
        }

        public NameReference ObjectName
        {
            get => new NameReference(ObjectNameString, _indexValue);
            set => (ObjectNameString, indexValue) = value;
        }

        public string ClassName => Class?.ObjectNameString ?? "Class";

        public string SuperClassName => SuperClass?.ObjectNameString ?? "Class";

        public string ParentName => _fileRef.GetEntry(_idxLink)?.ObjectName ?? "";

        public string ParentFullPath => _fileRef.GetEntry(_idxLink)?.FullPath ?? "";

        public string FullPath => _fileRef.IsEntry(_idxLink) ? $"{ParentFullPath}.{ObjectNameString}" : ObjectNameString;

        public string ParentInstancedFullPath => _fileRef.GetEntry(_idxLink)?.InstancedFullPath ?? "";
        public string InstancedFullPath => _fileRef.IsEntry(_idxLink) ? ObjectName.AddToPath(ParentInstancedFullPath) : ObjectName.Instanced;

        public bool HasParent => _fileRef.IsEntry(_idxLink);

        public IEntry Parent
        {
            get => _fileRef.GetEntry(_idxLink);
            set => idxLink = value?.UIndex ?? 0;
        }

        public bool HasArchetype => _fileRef.IsEntry(_idxArchetype);

        public IEntry Archetype
        {
            get => _fileRef.GetEntry(_idxArchetype);
            set => idxArchetype = value?.UIndex ?? 0;
        }

        public bool HasSuperClass => _fileRef.IsEntry(_idxSuperClass);

        public IEntry SuperClass
        {
            get => _fileRef.GetEntry(_idxSuperClass);
            set => idxSuperClass = value?.UIndex ?? 0;
        }
        
        public bool IsClass => _idxClass == 0;

        public IEntry Class
        {
            get => _fileRef.GetEntry(_idxClass);
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
                propsEndOffset = null;
                EntryHasPendingChanges = true;
                _fileRef.IsModified = true; // mark package as modified if the existing header is changing.
            }
        }

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

        /// <summary>
        /// Gets properties of an export.
        /// </summary>
        /// <param name="forceReload">obsolete, does nothing. Removing it could cause usages to behave differently. Consider this usage: <c>GetProperties(true)</c>. It would still compile if this arg was removed, but would now include none properties</param>
        /// <param name="includeNoneProperties">Include NoneProperties in the resulting property collection</param>
        /// <returns></returns>
        public PropertyCollection GetProperties(bool forceReload = false, bool includeNoneProperties = false, int propStartPos = 0, PackageCache packageCache = null)
        {
            if (IsClass)
            {
                return new PropertyCollection { endOffset = 4, IsImmutable = true };
            } //no properties

            IEntry parsingClass = this;
            if (IsDefaultObject)
            {
                parsingClass = Class; //class we are defaults of
            }
            
            if (propStartPos == 0)
                propStartPos = GetPropertyStart();
            var stream = new MemoryStream(_data, false);
            stream.Seek(propStartPos, SeekOrigin.Current);
            return PropertyCollection.ReadProps(this, stream, ClassName, includeNoneProperties, true, parsingClass, packageCache);
        }

        public void WriteProperties(PropertyCollection props)
        {
            var m = new EndianReader { Endian = _fileRef.Endian };
            m.Writer.Write(_data, 0, GetPropertyStart());
            props.WriteTo(m.Writer, _fileRef);
            int binStart = propsEnd();
            m.Writer.Write(_data, binStart, _data.Length - binStart);
            Data = m.ToArray();
        }

        public void WritePrePropsAndProperties(byte[] prePropBytes, PropertyCollection props, int binStart = -1)
        {
            var m = new EndianReader { Endian = _fileRef.Endian };
            m.Writer.WriteBytes(prePropBytes);
            props.WriteTo(m.Writer, _fileRef);
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
                    int count = EndianReader.ToInt32(_data, 0, _fileRef.Endian);
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
                        int count = EndianReader.ToInt32(_data, 0, _fileRef.Endian);
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
                MEGame.ME1 when _fileRef.Platform == MEPackage.GamePlatform.PS3 => 30,
                MEGame.ME2 when _fileRef.Platform == MEPackage.GamePlatform.PS3 => 30,
                _ => 32
            };

        public int NetIndex
        {
            get => EndianReader.ToInt32(_data, GetPropertyStart() - 4, _fileRef.Endian);
            set
            {
                if (value != NetIndex)
                {
                    var data = Data;
                    data.OverwriteRange(GetPropertyStart() - 4, EndianBitConverter.GetBytes(value, _fileRef.Endian));
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
            var m = new EndianReader { Endian = _fileRef.Endian };
            m.Writer.Write(_data, 0, propsEnd());
            m.Writer.WriteBytes(binaryData);
            Data = m.ToArray();
        }

        public void WriteBinary(ObjectBinary bin)
        {
            var m = new EndianReader { Endian = _fileRef.Endian };
            m.Writer.Write(_data, 0, propsEnd());
            bin.WriteTo(m.Writer, _fileRef, DataOffset);
            Data = m.ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="props"></param>
        /// <param name="binary"></param>
        public void WritePropertiesAndBinary(PropertyCollection props, ObjectBinary binary)
        {
            var m = new EndianReader { Endian = _fileRef.Endian };
            m.Writer.Write(_data, 0, GetPropertyStart());
            props?.WriteTo(m.Writer, _fileRef); //props could be null if this is a class
            binary.WriteTo(m.Writer, _fileRef, DataOffset);
            Data = m.ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="preProps"></param>
        /// <param name="props"></param>
        public void WritePrePropsAndPropertiesAndBinary(byte[] preProps, PropertyCollection props, ObjectBinary binary)
        {
            var m = new EndianReader { Endian = _fileRef.Endian };
            m.Writer.WriteBytes(preProps);
            props?.WriteTo(m.Writer, _fileRef); //props could be null if this is a class
            binary.WriteTo(m.Writer, _fileRef, DataOffset);
            Data = m.ToArray();
        }

        public ExportEntry Clone(int newIndex = -1)
        {
            var clone = (ExportEntry)MemberwiseClone();
            clone.Data = _data.ArrayClone();
            clone._generationNetObjectCounts = _generationNetObjectCounts.ArrayClone();
            if (HasComponentMap)
            {
                clone._componentMap = _componentMap.ArrayClone();
            }
            clone.HeaderOffset = 0;
            clone.DataOffset = 0;
            if (newIndex >= 0)
            {
                clone._indexValue = newIndex;
            }
            return clone;
        }

        IEntry IEntry.Clone(bool incrementIndex)
        {
            if (incrementIndex)
            {
                return Clone(_fileRef.GetNextIndexForInstancedName(this));
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
                return (EPropertyFlags)EndianReader.ToUInt64(_data, 0x18, _fileRef.Endian);
            }
            return null;
        }

        /// <summary>
        /// Gets preprops binary, properties, and binary, all without having to do multiple passes on the export
        /// </summary>
        /// <returns></returns>
        public ExportDatas GetExportDatasForPorting(IMEPackage destPackage)
        {
            ExportDatas ed = new ExportDatas();
            if (IsClass)
            {
                ed.prePropsBinary = Array.Empty<byte>();
                ed.Properties = null;
                ed.IsClass = true;
            }
            else
            {
                ed.PropStartOffset = GetPropertyStart();
                ed.prePropsBinary = _data.Slice(0, ed.PropStartOffset);
                ed.Properties = GetProperties(propStartPos: ed.PropStartOffset);
            }

            //for supported classes, this will add any names in binary to the Name table, as well as take care of binary differences for cross-game importing
            //for unsupported classes, this will just copy over the binary
            //sometimes converting binary requires altering the properties as well
            ed.postPropsBinary = ExportBinaryConverter.ConvertPostPropBinary(this, destPackage.Game, ed.Properties);
            return ed;
        }

        public class ExportDatas
        {
            public bool IsClass { get; set; }
            public byte[] prePropsBinary { get; set; }
            public PropertyCollection Properties { get; set; }
            public ObjectBinary postPropsBinary { get; set; }
            public int PropStartOffset { get; set; }
        }
    }
}
