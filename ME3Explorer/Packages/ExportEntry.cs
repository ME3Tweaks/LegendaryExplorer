using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Gammtek.Conduit.Extensions.IO;
using ME3Explorer.Unreal;
using StreamHelpers;
using static ME3Explorer.Unreal.UnrealFlags;

namespace ME3Explorer.Packages
{
    [DebuggerDisplay("{Game} ExportEntry | {UIndex} {ObjectName}({ClassName}) in {System.IO.Path.GetFileName(FileRef.FilePath)}")]

    public class ExportEntry : NotifyPropertyChangedBase, IEntry
    {
        public IMEPackage FileRef { get; protected set; }

        public MEGame Game => FileRef.Game;

        public int Index { get; set; }
        public int UIndex => Index + 1;

        public ExportEntry(IMEPackage file, byte[] prePropBinary = null, PropertyCollection properties = null, byte[] binary = null)
        {
            FileRef = file;
            OriginalDataSize = 0;
            _header = new byte[HasComponentMap ? 72 : 68];
            ObjectFlags = EObjectFlags.LoadForClient | EObjectFlags.LoadForServer | EObjectFlags.LoadForEdit; //sensible defaults?

            var ms = new MemoryStream();
            if (prePropBinary == null)
            {
                prePropBinary = new byte[4];
            }
            ms.WriteFromBuffer(prePropBinary);
            if (properties == null)
            {
                properties = new PropertyCollection();
            }
            properties.WriteTo(ms, file);
            if (binary != null)
            {
                ms.WriteFromBuffer(binary);
            }

            _data = ms.ToArray();
            DataSize = _data.Length;
        }

        public ExportEntry(IMEPackage file, Stream stream)
        {
            FileRef = file;
            OriginalDataSize = 0;
            HeaderOffset = (uint)stream.Position;
            switch (file.Game)
            {
                case MEGame.ME1:
                case MEGame.ME2:
                {

                    long start = stream.Position;
                    stream.Seek(40, SeekOrigin.Current);
                    int count = stream.ReadInt32();
                    stream.Seek(4 + count * 12, SeekOrigin.Current);
                    count = stream.ReadInt32();
                    stream.Seek(16, SeekOrigin.Current);
                    stream.Seek(4 + count * 4, SeekOrigin.Current);
                    long end = stream.Position;
                    stream.Seek(start, SeekOrigin.Begin);

                        //read header
                        _header = stream.ReadToBuffer((int)(end - start));
                        break;
                }
                case MEGame.ME3:
                case MEGame.UDK:
                {
                    stream.Seek(44, SeekOrigin.Current);
                    int count = stream.ReadInt32();
                    stream.Seek(-48, SeekOrigin.Current);

                    int expInfoSize = 68 + (count * 4);
                    _header = stream.ReadToBuffer(expInfoSize);
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }
            OriginalDataSize = DataSize;
            long headerEnd = stream.Position;

            stream.Seek(DataOffset, SeekOrigin.Begin);
            _data = stream.ReadToBuffer(DataSize);
            stream.Seek(headerEnd, SeekOrigin.Begin);
            if (file.Game == MEGame.ME1 && ClassName.Contains("Property") || file.Game != MEGame.ME1 && HasStack)
            {
                ReadsFromConfig = _data.Length > 25 && (_data[25] & 64) != 0;
            }
            else
            {
                ReadsFromConfig = false;
            }
        }

        public bool HasStack => ObjectFlags.HasFlag(EObjectFlags.HasStack);

        public bool IsDefaultObject => ObjectFlags.HasFlag(EObjectFlags.ClassDefaultObject);

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
                if (_header != null && value != null && _header.SequenceEqual(value))
                {
                    return; //if the data is the same don't write it and trigger the side effects
                }

                int dataSize = _header != null ? DataSize : (_data?.Length ?? 0);
                _header = value;
                DataSize = dataSize; //should never be altered by Header overwrite
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

        public byte[] GenerateHeader(MEGame game) => GenerateHeader(null, null, game == MEGame.ME1 || game == MEGame.ME2);

        public void RegenerateHeader(MEGame game) => Header = GenerateHeader(game);

        private byte[] GenerateHeader(OrderedMultiValueDictionary<NameReference, int> componentMap, int[] generationNetObjectCount, bool? hasComponentMap = null)
        {
            var bin = new MemoryStream();
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
                OrderedMultiValueDictionary<NameReference, int> cmpMap = componentMap ?? ComponentMap;
                bin.WriteInt32(cmpMap.Count);
                foreach ((NameReference name, int uIndex) in cmpMap)
                {
                    bin.WriteInt32(FileRef.FindNameOrAdd(name.Name));
                    bin.WriteInt32(name.Number);
                    bin.WriteInt32(uIndex);
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
            Header = GenerateHeader(componentMap, generationNetObjectCount, hasComponentMap);
        }

        public uint HeaderOffset { get; set; }

        private int idxClass
        {
            get => BitConverter.ToInt32(_header, 0);
            set
            {
                Buffer.BlockCopy(BitConverter.GetBytes(value), 0, _header, 0, sizeof(int));
                HeaderChanged = true;
            }
        }

        private int idxSuperClass
        {
            get => BitConverter.ToInt32(_header, 4);
            set
            {
                Buffer.BlockCopy(BitConverter.GetBytes(value), 0, _header, 4, sizeof(int));
                HeaderChanged = true;
            }
        }

        public int idxLink
        {
            get => BitConverter.ToInt32(_header, 8);
            set
            {
                Buffer.BlockCopy(BitConverter.GetBytes(value), 0, _header, 8, sizeof(int));
                HeaderChanged = true;
            }
        }

        private int idxObjectName
        {
            get => BitConverter.ToInt32(_header, 12);
            set
            {
                Buffer.BlockCopy(BitConverter.GetBytes(value), 0, _header, 12, sizeof(int));
                HeaderChanged = true;
            }
        }

        public int indexValue
        {
            get => BitConverter.ToInt32(_header, 16);
            set
            {
                if (indexValue != value)
                {
                    Buffer.BlockCopy(BitConverter.GetBytes(value), 0, _header, 16, sizeof(int));
                    HeaderChanged = true;
                }
            }
        }

        private int idxArchetype
        {
            get => BitConverter.ToInt32(_header, 20);
            set
            {
                Buffer.BlockCopy(BitConverter.GetBytes(value), 0, _header, 20, sizeof(int));
                HeaderChanged = true;
            }
        }

        public EObjectFlags ObjectFlags
        {
            get => (EObjectFlags)BitConverter.ToUInt64(_header, 24);
            set
            {
                Buffer.BlockCopy(BitConverter.GetBytes((ulong)value), 0, _header, 24, sizeof(ulong));
                HeaderChanged = true;
            }
        }

        public int DataSize
        {
            get => BitConverter.ToInt32(_header, 32);
            private set => Buffer.BlockCopy(BitConverter.GetBytes(value), 0, _header, 32, sizeof(int));
        }

        public int DataOffset
        {
            get => BitConverter.ToInt32(_header, 36);
            set => Buffer.BlockCopy(BitConverter.GetBytes(value), 0, _header, 36, sizeof(int));
        }

        public bool HasComponentMap => FileRef.Game == MEGame.ME1 || FileRef.Game == MEGame.ME2;

        //me1 and me2 only
        public OrderedMultiValueDictionary<NameReference, int> ComponentMap
        {
            get
            {
                var componentMap = new OrderedMultiValueDictionary<NameReference, int>();
                if (!HasComponentMap) return componentMap;
                int count = BitConverter.ToInt32(_header, 40);
                for (int i = 0; i < count; i++)
                {
                    int pairIndex = 44 + i * 12;
                    string name = FileRef.GetNameEntry(BitConverter.ToInt32(_header, pairIndex));
                    componentMap.Add(new NameReference(name, BitConverter.ToInt32(_header, pairIndex + 4)),
                                                                          BitConverter.ToInt32(_header, pairIndex + 8));
                }
                return componentMap;
            }
            set
            {
                if (!HasComponentMap) return;
                RegenerateHeader(value, null);
            }
        }

        public int ExportFlagsOffset => HasComponentMap ? 44 + BitConverter.ToInt32(_header, 40) * 12 : 40;

        public EExportFlags ExportFlags
        {
            get => (EExportFlags)BitConverter.ToUInt32(_header, ExportFlagsOffset);
            set
            {
                Buffer.BlockCopy(BitConverter.GetBytes((uint)value), 0, _header, ExportFlagsOffset, sizeof(uint));
                HeaderChanged = true;
            }
        }

        public int[] GenerationNetObjectCount
        {
            get
            {
                int count = BitConverter.ToInt32(_header, ExportFlagsOffset + 4);
                var result = new int[count];
                for (int i = 0; i < count; i++)
                {
                    result[i] = BitConverter.ToInt32(_header, ExportFlagsOffset + 8 + i * 4);
                }
                return result;
            }
            set => RegenerateHeader(null, value);
        }

        public int PackageGuidOffset => ExportFlagsOffset + 8 + BitConverter.ToInt32(_header, ExportFlagsOffset + 4) * 4;

        public Guid PackageGUID
        {
            get => new Guid(_header.Slice(PackageGuidOffset, 16));
            set
            {
                Buffer.BlockCopy(value.ToByteArray(), 0, _header, PackageGuidOffset, 16);
                HeaderChanged = true;
            }
        }

        public EPackageFlags PackageFlags
        {
            get => (EPackageFlags)BitConverter.ToUInt32(_header, PackageGuidOffset + 16);
            set
            {
                Buffer.BlockCopy(BitConverter.GetBytes((uint)value), 0, _header, PackageGuidOffset + 16, sizeof(uint));
                HeaderChanged = true;
            }
        }

        public string ObjectName
        {
            get => FileRef.Names[idxObjectName];
            set => idxObjectName = FileRef.FindNameOrAdd(value);
        }

        public string ClassName => Class?.ObjectName ?? "Class";

        public string SuperClassName => SuperClass?.ObjectName ?? "Class";

        public string PackageName
        {
            get
            {
                int val = idxLink;
                if (val != 0)
                {
                    IEntry entry = FileRef.GetEntry(val);
                    return entry.ObjectName;
                }
                else return "Package";
            }
        }

        public string PackageNameInstanced
        {
            get
            {
                int val = idxLink;
                if (val != 0)
                {
                    IEntry entry = FileRef.GetEntry(val);
                    string result =  entry.ObjectName;
                    if (entry.indexValue > 0)
                    {
                        return result + "_" + entry.indexValue; //Should be -1 for 4.1, will remain as-is for 4.0
                    }
                    return result;
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
                    string newPackageName = FileRef.GetEntry(idxNewPackName).PackageName;
                    if (newPackageName != "Package")
                        result = newPackageName + "." + result;
                    idxNewPackName = FileRef.GetEntry(idxNewPackName).idxLink;
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

        public string GetIndexedFullPath => GetFullPath + "_" + indexValue;

        public string PackageFullNameInstanced
        {
            get
            {
                string result = PackageNameInstanced;
                int idxNewPackName = idxLink;

                while (idxNewPackName != 0)
                {
                    IEntry e = FileRef.GetEntry(idxNewPackName);
                    string newPackageName = e.PackageNameInstanced;
                    if (newPackageName != "Package")
                        result = newPackageName + "." + result;
                    idxNewPackName = FileRef.GetEntry(idxNewPackName).idxLink;
                }
                return result;
            }
        }

        public string GetInstancedFullPath
        {
            get
            {
                string s = "";
                if (PackageFullNameInstanced != "Class" && PackageFullNameInstanced != "Package")
                    s += PackageFullNameInstanced + ".";
                s += ObjectName;
                if (indexValue > 0)
                {
                    s += "_" + indexValue; // Should be -1, but will wait for 4.1 to correct this for consistency
                }
                return s;
            }
        }

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
        protected byte[] _data;

        /// <summary>
        /// RETURNS A CLONE
        /// </summary>
        public byte[] Data
        {
            get => _data.TypedClone();

            set
            {
                if (_data != null && value != null && _data.SequenceEqual(value))
                {
                    return; //if the data is the same don't write it and trigger the side effects
                }

                _data = value;
                DataSize = value.Length;
                DataChanged = true;
                properties = null;
                propsEndOffset = null;
                EntryHasPendingChanges = true;
            }
        }

        public int LinkerIndex => BitConverter.ToInt32(_data, 0);

        public int OriginalDataSize { get; protected set; }
        public bool ReadsFromConfig { get; protected set; }

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
                OnPropertyChanged();
                //    }
            }
        }

        bool headerChanged;

        public bool HeaderChanged
        {
            get => headerChanged;

            set
            {
                //This cannot be optimized as we cannot subscribe to array chagne events
                //if (headerChanged != value)
                //{
                headerChanged = value;
                EntryHasPendingChanges |= value;
                //    if (value)
                //    {
                OnPropertyChanged();
                //    }
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

        PropertyCollection properties;

        /// <summary>
        /// Gets properties of an export. You can force it to reload which is useful when debugging the property engine.
        /// </summary>
        /// <param name="forceReload">Forces full property release rather than using the property collection cache</param>
        /// <param name="includeNoneProperties">Include NoneProperties in the resulting property collection</param>
        /// <returns></returns>
        public PropertyCollection GetProperties(bool forceReload = false, bool includeNoneProperties = false)
        {
            if (properties != null && !forceReload && !includeNoneProperties)
            {
                return properties;
            }

            if (ClassName == "Class")
            {
                return new PropertyCollection();
            } //no properties

            //else if (!includeNoneProperties)
            //{
            //    int start = GetPropertyStart();
            //    MemoryStream stream = new MemoryStream(_data, false);
            //    stream.Seek(start, SeekOrigin.Current);
            //    return properties = PropertyCollection.ReadProps(FileRef, stream, ClassName, includeNoneProperties, true, ObjectName);
            //}
            //else
            //{
            int start = GetPropertyStart();
            MemoryStream stream = new MemoryStream(_data, false);
            stream.Seek(start, SeekOrigin.Current);
            IEntry parsingClass = this;
            if (IsDefaultObject)
            {
                parsingClass = Class; //class we are defaults of
            }

            return PropertyCollection.ReadProps(this, stream, ClassName, includeNoneProperties, true, parsingClass); //do not set properties as this may interfere with some other code. may change later.
            //  }
        }

        public void WriteProperties(PropertyCollection props)
        {
            MemoryStream m = new MemoryStream();
            props.WriteTo(m, FileRef);
            int propStart = GetPropertyStart();
            int propEnd = propsEnd();
            byte[] propData = m.ToArray();
            Data = _data.Take(propStart).Concat(propData).Concat(_data.Skip(propEnd)).ToArray();
        }

        public int GetPropertyStart()
        {
            if (ClassName == "Class")
            {
                return 0;
            }
            IMEPackage pcc = FileRef;
            if (HasStack)
            {
                if (pcc.Game != MEGame.ME3)
                {
                    return 32;
                }

                return 30;
            }

            //TODO: If there are more classes which have binary before the props, could be worth creating a more extensible system for this
            if (pcc.Game == MEGame.ME3 && ClassName == "DominantDirectionalLightComponent" || ClassName == "DominantSpotLightComponent")
            {
                int count = BitConverter.ToInt32(_data, 0);
                return count * 2 + 12;
            }

            //if (!IsDefaultObject)
            //{
            //    switch (ClassName)
            //    {
            //        case "ParticleSystemComponent":
            //            return 0x10;
            //    }
            //}
            if (_data.Length == 4)
            {
                return 4;
            }

            int result = 8;
            int test0 = BitConverter.ToInt32(_data, 0);
            int test1 = BitConverter.ToInt32(_data, 4);
            int test2 = BitConverter.ToInt32(_data, 8); //Name index if Test1 is actually a name. Should be 0 since we wouldn't have indexes here
            if (pcc.IsName(test1) && test2 == 0) //is 0x4 a proper 8 byte name?
                result = 4;
            if (pcc.IsName(test1) && pcc.IsName(test2) && test2 != 0)
                result = 8;

            if (_data.Length > 0x10 && pcc.IsName(test1) && pcc.GetNameEntry(test1) == ObjectName && test0 == 0 && test2 == indexValue) //!= UIndex will cover more cases, but there's still the very tiny case where they line up
            {
                int test3 = BitConverter.ToInt32(_data, 0x10);
                string namev = pcc.GetNameEntry(test3);
                //Debug.WriteLine("Reading " + name + " (" + namev + ") at 0x" + (stream.Position - 24).ToString("X8"));
                if (namev != null && Enum.IsDefined(typeof(PropertyType), namev) && Enum.TryParse(namev, out PropertyType propertyType))
                {
                    if (propertyType > PropertyType.None)
                    {
                        //Edge case
                        return 0x8;
                    }
                }

                //Debug.WriteLine("Primitive Component: " + ClassName + " (" + ObjectName + ")");
                return 0x10; //Primitive Component
            }

            return result;
        }

        public int NetIndex
        {
            get => BitConverter.ToInt32(_data, GetPropertyStart() - 4);
            set {
                if (value != NetIndex)
                {
                    var data = Data;
                    data.OverwriteRange(GetPropertyStart() - 4, BitConverter.GetBytes(value));
                    Data = data;
                    OnPropertyChanged();
                }
            }
        }

        private int? propsEndOffset;
        public int propsEnd()
        {
            if (propsEndOffset.HasValue)
            {
                return propsEndOffset.Value;
            }
            var props = GetProperties(true, true);
            propsEndOffset = props.endOffset;
            return propsEndOffset.Value;
        }

        public byte[] getBinaryData()
        {
            return _data.Skip(propsEnd()).ToArray();
        }

        public void setBinaryData(byte[] binaryData)
        {
            Data = _data.Take(propsEnd()).Concat(binaryData).ToArray();
        }

        public ExportEntry Clone()
        {
            int index = 0;
            string name = ObjectName;
            foreach (ExportEntry ent in FileRef.Exports)
            {
                if (name == ent.ObjectName && ent.indexValue > index)
                {
                    index = ent.indexValue;
                }
            }
            index++;
            return new ExportEntry(FileRef)
            {
                _header = _header.TypedClone(),
                HeaderOffset = 0,
                Data = this.Data,
                indexValue = index
            };
        }
    }
}
