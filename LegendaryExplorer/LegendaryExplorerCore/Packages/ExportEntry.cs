using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Memory;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.Collections;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using PropertyChanged;
using static LegendaryExplorerCore.Unreal.UnrealFlags;

namespace LegendaryExplorerCore.Packages
{
    [DebuggerDisplay("{Game} ExportEntry | {UIndex} {ObjectName.Instanced}({ClassName}) in {System.IO.Path.GetFileName(_fileRef.FilePath)}")]
    [DoNotNotify]//disable Fody/PropertyChanged for this class. Do notification manually
    public sealed class ExportEntry :  IEntry
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
            _commonHeaderFields._idxLink = parent_uIndex;
            _commonHeaderFields._idxObjectName = _fileRef.FindNameOrAdd(name.Name);
            _commonHeaderFields._indexValue = name.Number;
            _generationNetObjectCounts = [];
            if (HasComponentMap)
            {
                _componentMap = [];
            }

            _commonHeaderFields._dataOffset = 0;
            ObjectFlags = EObjectFlags.LoadForClient | EObjectFlags.LoadForServer | EObjectFlags.LoadForEdit; //sensible defaults?

            var ms = new EndianWriter { Endian = file.Endian };
            if (prePropBinary == null)
            {
                Span<byte> span = stackalloc byte[4];
                span.Clear();
                ms.Write(span);
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
            _commonHeaderFields._dataSize = _data.Length;
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
            _commonHeaderFields._dataOffset = 0;

            var ms = new EndianWriter { Endian = file.Endian };
            if (prePropBinary == null)
            {
                Span<byte> span = stackalloc byte[4];
                span.Clear();
                ms.Write(span);
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
            _commonHeaderFields._dataSize = _data.Length;
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

                stream.Seek(_commonHeaderFields._dataOffset, SeekOrigin.Begin);
                _data = stream.ReadBytes(_commonHeaderFields._dataSize);
                stream.Seek(headerEnd, SeekOrigin.Begin);
            }
        }

        public bool HasStack => _commonHeaderFields._objectFlags.Has(EObjectFlags.HasStack);

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
        
        public bool IsDefaultObject => _commonHeaderFields._objectFlags.Has(EObjectFlags.ClassDefaultObject);

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

            int dataSize = _commonHeaderFields._dataSize;
            DeserializeHeader(new EndianReader(value));
            _commonHeaderFields._dataSize = dataSize; //should never be altered by Header overwrite

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
                    + 4 //GenerationNetObjectCountsLength
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
            if (pcc.Endian.IsNative && pcc.Game > MEGame.ME2 && _commonHeaderFields._generationNetObjectCountsLength == 0)
            {
                unsafe
                {
                    byte[] bytes = new byte[sizeof(CommonHeaderFields)];
                    MemoryMarshal.Write(bytes.AsSpan(), in _commonHeaderFields);
                    return bytes;
                }
            }
            using var bin = new EndianWriter(MemoryManager.GetMemoryStream(HeaderLength)) { Endian = pcc.Endian };
            bin.WriteInt32(_commonHeaderFields._idxClass);
            bin.WriteInt32(_commonHeaderFields._idxSuperClass);
            bin.WriteInt32(_commonHeaderFields._idxLink);
            bin.WriteInt32(_commonHeaderFields._idxObjectName);
            bin.WriteInt32(_commonHeaderFields._indexValue);
            bin.WriteInt32(_commonHeaderFields._idxArchetype);
            bin.WriteUInt64((ulong)_commonHeaderFields._objectFlags);
            bin.WriteInt32(_commonHeaderFields._dataSize);
            bin.WriteInt32(_commonHeaderFields._dataOffset);
            if (pcc.Game <= MEGame.ME2 && pcc.Platform != MEPackage.GamePlatform.PS3)
            {
                if (clearComponentMap)
                {
                    bin.WriteInt32(0);
                }
                else
                {
                    byte[] componentMap = _componentMap ?? [];
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
            //happy path
            if (stream.Endian.IsNative && _fileRef.Game > MEGame.ME2)
            {
                stream.Read(MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref _commonHeaderFields, 1)));
                
                //rarely true. Only for ForcedExport packages I think?
                if (_commonHeaderFields._generationNetObjectCountsLength > 0)
                {
                    const int SIZE_OF_PACKAGEGUID_AND_PACKAGEFLAGS = 20;
                    stream.Seek(-SIZE_OF_PACKAGEGUID_AND_PACKAGEFLAGS, SeekOrigin.Current);
                    goto GenNetObjsNotEmpty;
                }
                _generationNetObjectCounts = [];
                return;
            }
            //slower fallback :(

            _commonHeaderFields._idxClass = stream.ReadInt32();
            _commonHeaderFields._idxSuperClass = stream.ReadInt32();
            _commonHeaderFields._idxLink = stream.ReadInt32();
            _commonHeaderFields._idxObjectName = stream.ReadInt32();
            _commonHeaderFields._indexValue = stream.ReadInt32();
            _commonHeaderFields._idxArchetype = stream.ReadInt32();
            _commonHeaderFields._objectFlags = (EObjectFlags)stream.ReadUInt64();
            _commonHeaderFields._dataSize = stream.ReadInt32();
            _commonHeaderFields._dataOffset = stream.ReadInt32();

            if (HasComponentMap)
            {
                _componentMap = stream.ReadBytes(stream.ReadInt32() * 12);
            }
            _commonHeaderFields._exportFlags = (EExportFlags)stream.ReadUInt32();

            _commonHeaderFields._generationNetObjectCountsLength = stream.ReadInt32();

        GenNetObjsNotEmpty:

            int count = _commonHeaderFields._generationNetObjectCountsLength;
            _generationNetObjectCounts = new int[count];
            for (int i = 0; i < count; i++)
            {
                _generationNetObjectCounts[i] = stream.ReadInt32();
            }
            _commonHeaderFields._packageGuid = stream.ReadGuid();
            if (Game != MEGame.ME1 || _fileRef.Platform != MEPackage.GamePlatform.Xenon)
            {
                _commonHeaderFields._packageFlags = (EPackageFlags)stream.ReadUInt32();
            }
        }

        public void SerializeHeader(Stream bin)
        {
            if (_fileRef.Endian.IsNative && _fileRef.Game > MEGame.ME2 && _commonHeaderFields._generationNetObjectCountsLength == 0)
            {
                bin.Write(MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref _commonHeaderFields, 1)));
            }
            else
            {
                bin.WriteInt32(_commonHeaderFields._idxClass);
                bin.WriteInt32(_commonHeaderFields._idxSuperClass);
                bin.WriteInt32(_commonHeaderFields._idxLink);
                bin.WriteInt32(_commonHeaderFields._idxObjectName);
                bin.WriteInt32(_commonHeaderFields._indexValue);
                bin.WriteInt32(_commonHeaderFields._idxArchetype);
                bin.WriteUInt64((ulong)_commonHeaderFields._objectFlags);
                bin.WriteInt32(_commonHeaderFields._dataSize);
                bin.WriteInt32(_commonHeaderFields._dataOffset);
                if (HasComponentMap)
                {
                    bin.WriteInt32(_componentMap.Length / 12);
                    bin.Write(_componentMap);
                }
                bin.WriteUInt32((uint)_commonHeaderFields._exportFlags);
                int[] genobjCounts = _generationNetObjectCounts;
                bin.WriteInt32(genobjCounts.Length);
                for (int i = 0; i < genobjCounts.Length; i++)
                {
                    int count = genobjCounts[i];
                    bin.WriteInt32(count);
                }
                bin.WriteGuid(_commonHeaderFields._packageGuid);
                //doesn't exist on ME1 Xenon, but we don't support saving Xenon packages
                bin.WriteUInt32((uint)_commonHeaderFields._packageFlags);
            }
        }

        public int HeaderOffset;

        //Do not even think about touching this struct!
        //It is read directly from memory, so it must _exactly_ match the serialized layout of the export header.
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct CommonHeaderFields
        {
            public int _idxClass;
            public int _idxSuperClass;
            public int _idxLink;
            public int _idxObjectName;
            public int _indexValue;
            public int _idxArchetype;
            public EObjectFlags _objectFlags;
            public int _dataSize;
            public int _dataOffset;
            public EExportFlags _exportFlags;
            public int _generationNetObjectCountsLength;
            public Guid _packageGuid;
            public EPackageFlags _packageFlags;
        }

        private CommonHeaderFields _commonHeaderFields;

        public int idxClass
        {
            get => _commonHeaderFields._idxClass;
            private set
            {
                if (value != _commonHeaderFields._idxClass)
                {
                    _commonHeaderFields._idxClass = value;
                    HeaderChanged = true;
                }
            }
        }

        public int idxSuperClass
        {
            get => _commonHeaderFields._idxSuperClass;
            private set
            {
                if (value != _commonHeaderFields._idxSuperClass)
                {
                    // 0 check for setup
                    if (_uIndex != 0 && value == _uIndex)
                    {
                        throw new Exception("Cannot set export superclass to itself, this will cause infinite recursion");
                    }
                    _commonHeaderFields._idxSuperClass = value;
                    HeaderChanged = true;
                }
            }
        }

        public int idxLink
        {
            get => _commonHeaderFields._idxLink;
            set
            {
                if (value != _commonHeaderFields._idxLink)
                {
                    // HeaderOffset = 0 means this was instantiated and not read in from a stream
                    if (value == _uIndex && HeaderOffset != 0)
                    {
                        throw new Exception("Cannot set import link to itself, this will cause infinite recursion");
                    }
                    _commonHeaderFields._idxLink = value;
                    HeaderChanged = true;
                    _fileRef.IsModified = true;
                    _fileRef.InvalidateLookupTable();
                }
            }
        }

        private int idxObjectName
        {
            get => _commonHeaderFields._idxObjectName;
            set
            {
                if (value != _commonHeaderFields._idxObjectName)
                {
                    _commonHeaderFields._idxObjectName = value;
                    HeaderChanged = true;
                    _fileRef.InvalidateLookupTable();
                }
            }
        }

        public int indexValue
        {
            get => _commonHeaderFields._indexValue;
            set
            {
                if (value != _commonHeaderFields._indexValue)
                {
                    _commonHeaderFields._indexValue = value;
                    HeaderChanged = true;
                    _fileRef.InvalidateLookupTable();
                }
            }
        }

        public int idxArchetype
        {
            get => _commonHeaderFields._idxArchetype;
            private set
            {
                if (value != _commonHeaderFields._idxArchetype)
                {
                    _commonHeaderFields._idxArchetype = value;
                    HeaderChanged = true;
                }
            }
        }

        public EObjectFlags ObjectFlags
        {
            get => _commonHeaderFields._objectFlags;
            set
            {
                if (value != _commonHeaderFields._objectFlags)
                {
                    _commonHeaderFields._objectFlags = value;
                    HeaderChanged = true;
                }
            }
        }

        public int DataSize => _commonHeaderFields._dataSize;

        public int DataOffset
        {
            get => _commonHeaderFields._dataOffset;
            set => _commonHeaderFields._dataOffset = value;
        }

        public bool HasComponentMap => _fileRef.Game <= MEGame.ME2 && _fileRef.Platform != MEPackage.GamePlatform.PS3;

        //me1 and me2 only
        private byte[] _componentMap;
        
        //Does not contain UIndexes! The ints in this are indexes into the exports array. +1 to get the UIndex
        public UMultiMap<NameReference, int> ComponentMap
        {
            get
            {
                var componentMap = new UMultiMap<NameReference, int>();
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
                    _componentMap = [];
                    HeaderChanged = true;
                    return;
                }
                var bin = new MemoryStream(value.Count * 12);
                foreach ((NameReference name, int uIndex) in value)
                {
                    bin.WriteInt32(_fileRef.FindNameOrAdd(name.Name));
                    bin.WriteInt32(name.Number);
                    bin.WriteInt32(uIndex); // 0-based index
                }
                var newMap = bin.ToArray();
                if (!newMap.AsSpan().SequenceEqual(_componentMap))
                {
                    _componentMap = newMap;
                    HeaderChanged = true;
                }
            }
        }

        public EExportFlags ExportFlags
        {
            get => _commonHeaderFields._exportFlags;
            set
            {
                if (value != _commonHeaderFields._exportFlags)
                {
                    _commonHeaderFields._exportFlags = value;
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
                    _commonHeaderFields._generationNetObjectCountsLength = _generationNetObjectCounts.Length;
                    HeaderChanged = true;
                }
            }
        }

        public Guid PackageGUID
        {
            get => _commonHeaderFields._packageGuid;
            set
            {
                if (value != _commonHeaderFields._packageGuid)
                {
                    _commonHeaderFields._packageGuid = value;
                    HeaderChanged = true;
                }
            }
        }

        public EPackageFlags PackageFlags
        {
            get => _commonHeaderFields._packageFlags;
            set
            {
                if (value != _commonHeaderFields._packageFlags)
                {
                    _commonHeaderFields._packageFlags = value;
                    HeaderChanged = true;
                }
            }
        }

        public string ObjectNameString
        {
            get => _fileRef.Names[_commonHeaderFields._idxObjectName];
            set => idxObjectName = _fileRef.FindNameOrAdd(value);
        }

        public NameReference ObjectName
        {
            get => new NameReference(ObjectNameString, _commonHeaderFields._indexValue);
            set => (ObjectNameString, indexValue) = value;
        }

        public string ClassName => Class?.ObjectNameString ?? "Class";

        public string SuperClassName => SuperClass?.ObjectNameString ?? "Class";

        public string ParentName => _fileRef.GetEntry(_commonHeaderFields._idxLink)?.ObjectName ?? "";

        public string ParentFullPath => _fileRef.GetEntry(_commonHeaderFields._idxLink)?.FullPath ?? "";

        public string FullPath => _fileRef.IsEntry(_commonHeaderFields._idxLink) ? $"{ParentFullPath}.{ObjectNameString}" : ObjectNameString;

        public string ParentInstancedFullPath => _fileRef.GetEntry(_commonHeaderFields._idxLink)?.InstancedFullPath ?? "";
        public string InstancedFullPath => _fileRef.IsEntry(_commonHeaderFields._idxLink) ? ObjectName.AddToPath(ParentInstancedFullPath) : ObjectName.Instanced;
        public string MemoryFullPath => IsForcedExport ? InstancedFullPath : $"{FileRef.FileNameNoExtension.StripUnrealLocalization()}.{InstancedFullPath}";

        public bool HasParent => _fileRef.IsEntry(_commonHeaderFields._idxLink);

        public IEntry Parent
        {
            get => _fileRef.GetEntry(_commonHeaderFields._idxLink);
            set => idxLink = value?.UIndex ?? 0;
        }

        public bool HasArchetype => _fileRef.IsEntry(_commonHeaderFields._idxArchetype);

        public IEntry Archetype
        {
            get => _fileRef.GetEntry(_commonHeaderFields._idxArchetype);
            set => idxArchetype = value?.UIndex ?? 0;
        }

        public bool HasSuperClass => _fileRef.IsEntry(_commonHeaderFields._idxSuperClass);

        public IEntry SuperClass
        {
            get => _fileRef.GetEntry(_commonHeaderFields._idxSuperClass);
            set => idxSuperClass = value?.UIndex ?? 0;
        }
        
        public bool IsClass => _commonHeaderFields._idxClass == 0;

        public IEntry Class
        {
            get => _fileRef.GetEntry(_commonHeaderFields._idxClass);
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
                _commonHeaderFields._dataSize = value.Length;
                DataChanged = true;
                propsEndOffset = null;
                EntryHasPendingChanges = true;
                _fileRef.IsModified = true; // mark package as modified if the existing header is changing.
            }
        }

        private static readonly PropertyChangedEventArgs DataChangedEventArgs = new(nameof(DataChanged));
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
                PropertyChanged?.Invoke(this, DataChangedEventArgs);
                //    }
            }
        }

        private static readonly PropertyChangedEventArgs HeaderChangedEventArgs = new(nameof(HeaderChanged));
        bool headerChanged;
        public bool HeaderChanged
        {
            get => headerChanged;

            set
            {
                headerChanged = value;
                EntryHasPendingChanges |= value;
                PropertyChanged?.Invoke(this, HeaderChangedEventArgs);
            }
        }

        private static readonly PropertyChangedEventArgs EmptyPropertyChangedEventArgs = new("");
        private bool _entryHasPendingChanges;
        public bool EntryHasPendingChanges
        {
            get => _entryHasPendingChanges;
            set
            {
                if (value != _entryHasPendingChanges)
                {
                    _entryHasPendingChanges = value;
                    PropertyChanged?.Invoke(this, EmptyPropertyChangedEventArgs);
                    _fileRef.IsModified |= value; // This is kind of a safeguard in the event other code missed stuff
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
                return new PropertyCollection { EndOffset = 4, IsImmutable = true };
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
            var props = PropertyCollection.ReadProps(this, stream, ClassName, includeNoneProperties, true, parsingClass, packageCache);
            propsEndOffset = props.EndOffset;
            return props;
        }

        public void WriteProperties(PropertyCollection props)
        {
            MemoryStream ms = MemoryManager.GetMemoryStream(_data.Length);
            var m = new EndianReader(ms) { Endian = _fileRef.Endian };
            m.Writer.Write(_data, 0, GetPropertyStart());
            props.WriteTo(m.Writer, _fileRef);
            int binStart = propsEnd();
            m.Writer.Write(_data, binStart, _data.Length - binStart);
            Data = m.ToArray();
        }

        public void WritePrePropsAndProperties(byte[] prePropBytes, PropertyCollection props, int binStart = -1)
        {
            // This does not properly work when porting assets across games
            // if the binary format significantly changes! An example is porting Texture2D across games, binStart could be wrong, which
            // leads to wrong branch taken
            MemoryStream ms = MemoryManager.GetMemoryStream(_data.Length);
            var m = new EndianReader(ms) { Endian = _fileRef.Endian };
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
                IEntry parent = Parent;
                while (parent is not null)
                {
                    if (parent is ExportEntry {IsDefaultObject: true})
                    {
                        start += 8; //TemplateName
                        break;
                    }

                    // 11/21/2023 - if parent is import also check if it is a default object
                    // This is technically a hack. The right way to do this would be to resolve
                    // the import but that would be slow.
                    if (parent is ImportEntry && parent.ObjectName.Name.StartsWith("Default__"))
                    {
                        start += 8; // TemplateName
                        break;
                    }
                    parent = parent.Parent;
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
            if (propsEndOffset is null)
            {
                if (IsClass)
                {
                    propsEndOffset = 4;
                }
                else if (!FileRef.Endian.IsNative)
                {
                    propsEndOffset = GetProperties(true, true).EndOffset;
                }
                else
                {
                    //This is much faster than actually reading the properties and allocating a full PropertyCollection
                    bool modernEngineVersion = Game >= MEGame.ME3 || FileRef.Platform == MEPackage.GamePlatform.PS3;
                    int pos = GetPropertyStart();
                    var data = DataReadOnly;
                    while (pos + 8 <= data.Length)
                    {
                        string propName = FileRef.GetNameEntry(MemoryMarshal.Read<int>(data[pos..]));
                        pos += 8;
                        if (propName == "")
                        {
#if DEBUG
                            if (Debugger.IsAttached)
                            {
                                Debugger.Break();
                            }
#endif
                            //broken properties
                            break;
                        }
                        if (propName == "None")
                        {
                            break;
                        }
                        if (pos + 12 >= data.Length)
                        {
#if DEBUG
                            if (Debugger.IsAttached)
                            {
                                Debugger.Break();
                            }
#endif
                            //broken properties
                            break;
                        }
                        string propType = FileRef.GetNameEntry(MemoryMarshal.Read<int>(data[pos..]));
                        pos += 8;
                        int size = MemoryMarshal.Read<int>(data[pos..]);
                        pos += 8 + size;
                        switch (propType)
                        {
                            case "StructProperty":
                                pos += 8;
                                break;
                            case "BoolProperty":
                                pos += modernEngineVersion ? 1 : 4;
                                break;
                            case "ByteProperty":
                                if (modernEngineVersion)
                                {
                                    pos += 8;
                                }
                                break;
                        }
                    }
                    propsEndOffset = pos;
                }
            }
            
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
            int binStart = propsEnd();
            var m = new EndianReader(MemoryManager.GetMemoryStream(binStart + binaryData.Length)) { Endian = _fileRef.Endian };
            m.Writer.Write(_data, 0, binStart);
            m.Writer.WriteBytes(binaryData);
            Data = m.ToArray();
        }

        public void WriteBinary(ObjectBinary bin)
        {
            MemoryStream ms = MemoryManager.GetMemoryStream(_data.Length);
            var m = new EndianReader(ms) { Endian = _fileRef.Endian };
            m.Writer.Write(_data, 0, propsEnd());
            bin.WriteTo(m.Writer, _fileRef, _commonHeaderFields._dataOffset);
            Data = m.ToArray();
        }

        /// <summary>
        /// Writes the existing pre-prop binary (if it exists), then appends props and binary
        /// </summary>
        /// <param name="props"></param>
        /// <param name="binary"></param>
        public void WritePropertiesAndBinary(PropertyCollection props, ObjectBinary binary)
        {
            MemoryStream ms = MemoryManager.GetMemoryStream(_data.Length);
            var m = new EndianReader(ms) { Endian = _fileRef.Endian };
            m.Writer.Write(_data, 0, GetPropertyStart());
            props?.WriteTo(m.Writer, _fileRef); //props could be null if this is a class
            binary.WriteTo(m.Writer, _fileRef, _commonHeaderFields._dataOffset);
            Data = m.ToArray();
        }

        /// <summary>
        /// Writes the entire export data
        /// </summary>
        /// <param name="preProps"></param>
        /// <param name="props"></param>
        public void WritePrePropsAndPropertiesAndBinary(byte[] preProps, PropertyCollection props, ObjectBinary binary)
        {
            MemoryStream ms = MemoryManager.GetMemoryStream(_data.Length);
            var m = new EndianReader(ms) { Endian = _fileRef.Endian };
            m.Writer.WriteBytes(preProps);
            props?.WriteTo(m.Writer, _fileRef); //props could be null if this is a class
            binary.WriteTo(m.Writer, _fileRef, _commonHeaderFields._dataOffset);
            Data = m.ToArray();
        }

        /// <summary>
        /// Clones this export. If you don't supply a new index, it will remain the same - ENSURE YOU CHANGE IT OR YOU'LL WASTE TIME DEBUGGING THE GAME!!
        /// </summary>
        /// <param name="newIndex"></param>
        /// <returns></returns>
        public ExportEntry Clone(int newIndex = -1)
        {
            var clone = (ExportEntry)MemberwiseClone();

            //set to empty array to avoid the sequenceequal optimization when setting Data
            clone._data = [];
            clone.Data = _data.ArrayClone();

            clone._generationNetObjectCounts = _generationNetObjectCounts.ArrayClone();
            if (HasComponentMap)
            {
                clone._componentMap = _componentMap.ArrayClone();
            }
            clone.HeaderOffset = 0;
            clone._commonHeaderFields._dataOffset = 0;
            if (newIndex >= 0)
            {
                clone._commonHeaderFields._indexValue = newIndex;
            }
            return clone;
        }

        /// <summary>
        /// If this object (or any parent) is marked ForcedExport
        /// </summary>
        public bool IsForcedExport
        {
            get
            {
                if ((ExportFlags & EExportFlags.ForcedExport) != 0) return true;
                if (Parent is ExportEntry exp) return exp.IsForcedExport;
                // Need to handle ImportEntry parents, I think? Are all downlevel children marked in vanilla?
                return false;
            }
        }

        IEntry IEntry.Clone(bool incrementIndex)
        {
            if (incrementIndex)
            {
                return Clone(_fileRef.GetNextIndexForInstancedName(this));
            }

            return Clone();
        }

        //only for temporary use! Do not add the export returned by this to the file
        internal ExportEntry CreateTempCopyWithNewData(byte[] newData)
        {
            var clone = (ExportEntry)MemberwiseClone();
            clone._data = newData;
            clone._commonHeaderFields._dataSize = newData.Length;
            return clone;
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
        /// Sets the property flags for this export to the ones specified. Exports that do not have a stack will not be modified, as they don't have property flags.
        /// </summary>
        /// <returns>True if the export has a stack, false otherwise</returns>
        public bool SetPropertyFlags(EPropertyFlags flags)
        {
            if (_fileRef.Platform != MEPackage.GamePlatform.PC) throw new Exception("Cannot call SetPropertyFlags() on non PC platform");
            if (HasStack)
            {
                // This might be able to be optimized. Have to go through .Data as it needs to call the side effects
                var data = Data;
                data.OverwriteRange(0x18, BitConverter.GetBytes((ulong)flags));
                Data = data;
                return true;
            }

            return false;
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

        /// <summary>
        /// Checks if the data part of this export is loaded. This can be useful when partially loading packages for performance.
        /// </summary>
        /// <returns>True if data was loaded, false otherwise</returns>
        public bool IsDataLoaded()
        {
            return _data != null;
        }

        //used by MEPackage during saving. Implemented on ExportEntry so that it can edit _data without allocating a copy (since ShaderCaches are quite large)
        [EditorBrowsable(EditorBrowsableState.Never)]
        internal void UpdateShaderCacheOffsets(int oldDataOffset)
        {
            int newDataOffset = DataOffset;
            if (ClassName != "ShaderCache" || oldDataOffset == newDataOffset)
            {
                return;
            }

            MEGame game = Game;
            var binData = new MemoryStream(_data, 0, DataSize, true, true);
            binData.Seek(propsEnd() + 1, SeekOrigin.Begin);

            int nameList1Count = binData.ReadInt32();
            binData.Seek(nameList1Count * 12, SeekOrigin.Current);

            if (game is MEGame.ME3 || game.IsLEGame())
            {
                int namelist2Count = binData.ReadInt32();//namelist2
                binData.Seek(namelist2Count * 12, SeekOrigin.Current);
            }

            if (game is MEGame.ME1)
            {
                int vertexFactoryMapCount = binData.ReadInt32();
                binData.Seek(vertexFactoryMapCount * 12, SeekOrigin.Current);
            }

            int shaderCount = binData.ReadInt32();
            for (int i = 0; i < shaderCount; i++)
            {
                binData.Seek(24, SeekOrigin.Current);
                int nextShaderOffset = binData.ReadInt32() - oldDataOffset;
                binData.Seek(-4, SeekOrigin.Current);
                binData.WriteInt32(nextShaderOffset + newDataOffset);
                binData.Seek(nextShaderOffset, SeekOrigin.Begin);
            }

            if (game is not MEGame.ME1)
            {
                int vertexFactoryMapCount = binData.ReadInt32();
                binData.Seek(vertexFactoryMapCount * 12, SeekOrigin.Current);
            }

            int materialShaderMapCount = binData.ReadInt32();
            for (int i = 0; i < materialShaderMapCount; i++)
            {
                binData.Seek(16, SeekOrigin.Current);

                int switchParamCount = binData.ReadInt32();
                binData.Seek(switchParamCount * 32, SeekOrigin.Current);

                int componentMaskParamCount = binData.ReadInt32();
                binData.Seek(componentMaskParamCount * 44, SeekOrigin.Current);

                if (game is MEGame.ME3 || game.IsLEGame())
                {
                    int normalParams = binData.ReadInt32();
                    binData.Seek(normalParams * 29, SeekOrigin.Current);

                    binData.Seek(8, SeekOrigin.Current);
                }

                int nextMaterialShaderMapOffset = binData.ReadInt32() - oldDataOffset;
                binData.Seek(-4, SeekOrigin.Current);
                binData.WriteInt32(nextMaterialShaderMapOffset + newDataOffset);
                binData.Seek(nextMaterialShaderMapOffset, SeekOrigin.Begin);
            }

            //set the datachanged poperties that would have been set had we gone through Data
            DataChanged = true;
            EntryHasPendingChanges = true;
            _fileRef.IsModified = true;
        }
    }
}
