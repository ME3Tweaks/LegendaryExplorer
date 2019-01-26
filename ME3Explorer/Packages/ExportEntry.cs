using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Gibbed.IO;
using ME3Explorer.Unreal;
using UsefulThings.WPF;

namespace ME3Explorer.Packages
{
    public abstract class ExportEntry : ViewModelBase, IEntry
    {
        public IMEPackage FileRef { get; protected set; }

        public int Index { get; set; }
        public int UIndex { get { return Index + 1; } }

        protected ExportEntry(IMEPackage file)
        {
            FileRef = file;
            OriginalDataSize = 0;
        }

        /// <summary>
        /// NEVER DIRECTLY SET THIS OUTSIDE OF CONSTRUCTOR!
        /// </summary>
        protected byte[] _header;
        /// <summary>
        /// The underlying header is directly returned by this getter. If you want to write a new header back, use the copy provided by getHeader()!
        /// Otherwise some events may not trigger
        /// </summary>
        public byte[] Header
        {
            get { return _header; }
            set
            {
                if (_header != null && value != null && _header.SequenceEqual(value))
                {
                    return; //if the data is the same don't write it and trigger the side effects
                }

                bool isFirstLoad = _header == null;
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

        public uint HeaderOffset { get; set; }

        public int idxClass { get { return BitConverter.ToInt32(Header, 0); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, Header, 0, sizeof(int)); HeaderChanged = true; } }
        public int idxClassParent { get { return BitConverter.ToInt32(Header, 4); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, Header, 4, sizeof(int)); HeaderChanged = true; } }
        public int idxLink { get { return BitConverter.ToInt32(Header, 8); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, Header, 8, sizeof(int)); HeaderChanged = true; } }
        public int idxObjectName { get { return BitConverter.ToInt32(Header, 12); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, Header, 12, sizeof(int)); HeaderChanged = true; } }
        public int indexValue { get { return BitConverter.ToInt32(Header, 16); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, Header, 16, sizeof(int)); HeaderChanged = true; } }
        public int idxArchtype { get { return BitConverter.ToInt32(Header, 20); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, Header, 20, sizeof(int)); HeaderChanged = true; } }
        public ulong ObjectFlags { get { return BitConverter.ToUInt64(Header, 24); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, Header, 24, sizeof(long)); HeaderChanged = true; } }
        public int DataSize { get { return BitConverter.ToInt32(Header, 32); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, Header, 32, sizeof(int)); } }
        public int DataOffset { get { return BitConverter.ToInt32(Header, 36); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, Header, 36, sizeof(int)); } }
        //if me1 or me2: int unkcount1
        byte[][] unkList1;//if me1 or me2: unkcount1 * 12 bytes
        int unk1; //int unk1 
        //int unkcount2 
        int unk2;//int unk2 
        public Guid PackageGUID { get; set; } //GUID
        int[] unkList2;//unkcount2 * 4 bytes 

        public string ObjectName { get { return FileRef.Names[idxObjectName]; } }
        public string ClassName { get { int val = idxClass; if (val != 0) return FileRef.Names[FileRef.getEntry(val).idxObjectName]; else return "Class"; } }
        public string ClassParent { get { int val = idxClassParent; if (val != 0) return FileRef.Names[FileRef.getEntry(val).idxObjectName]; else return "Class"; } }
        public string ArchtypeName { get { int val = idxArchtype; if (val != 0) return FileRef.getNameEntry(FileRef.getEntry(val).idxObjectName); else return "None"; } }

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

        //NEVER DIRECTLY SET THIS OUTSIDE OF CONSTRUCTOR!
        protected byte[] _data;
        /// <summary>
        /// RETURNS A CLONE
        /// </summary>
        public byte[] Data
        {
            get { return _data.TypedClone(); }

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

        public int OriginalDataSize { get; protected set; }
        public bool ReadsFromConfig { get; protected set; }

        bool dataChanged;
        public bool DataChanged
        {
            get
            {
                return dataChanged;
            }

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
            get
            {
                return headerChanged;
            }

            set
            {
                //This cannot be optimized as we cannot subscribe to array chagne events
                //if (headerChanged != value)
                //{
                headerChanged = value;
                //    if (value)
                //    {
                OnPropertyChanged();
                //    }
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

        PropertyCollection properties;

        /// <summary>
        /// Gets properties of an export. You can force it to reload which is useful when debugging the property engine.
        /// </summary>
        /// <param name="forceReload">Forces full property release rather than using the property collection cache</param>
        /// <param name="includeNoneProeprty">Include NoneProperties in the resulting property collection</param>
        /// <returns></returns>
        public PropertyCollection GetProperties(bool forceReload = false, bool includeNoneProperties = false)
        {
            if (properties != null && !forceReload && !includeNoneProperties)
            {
                return properties;
            }
            if (ClassName == "Class") { return new PropertyCollection(); } //no properties
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
            if (ObjectName.StartsWith("Default__"))
            {
                parsingClass = FileRef.getEntry(idxClass); //class we are defaults of
            }
            return PropertyCollection.ReadProps(FileRef, stream, ClassName, includeNoneProperties, true, parsingClass); //do not set properties as this may interfere with some other code. may change later.
                                                                                                                        //  }
        }

        public T GetProperty<T>(string name) where T : UProperty
        {
            return GetProperties().GetProp<T>(name);
        }

        public void WriteProperties(PropertyCollection props)
        {
            MemoryStream m = new MemoryStream();
            props.WriteTo(m, FileRef);
            int propStart = GetPropertyStart();
            int propEnd = propsEnd();
            byte[] propData = m.ToArray();
            this.Data = _data.Take(propStart).Concat(propData).Concat(_data.Skip(propEnd)).ToArray();
        }

        public void WriteProperty(UProperty prop)
        {
            var props = GetProperties();
            props.AddOrReplaceProp(prop);
            WriteProperties(props);
        }

        public int GetPropertyStart()
        {
            IMEPackage pcc = FileRef;
            if ((ObjectFlags & (ulong)UnrealFlags.EObjectFlags.HasStack) != 0)
            {
                if (pcc.Game != MEGame.ME3)
                {
                    return 32;
                }
                return 30;
            }
            //if (!ObjectName.StartsWith("Default__"))
            //{
            //    switch (ClassName)
            //    {
            //        case "ParticleSystemComponent":
            //            return 0x10;
            //    }
            //}
            int result = 8;
            int test0 = BitConverter.ToInt32(_data, 0);
            int test1 = BitConverter.ToInt32(_data, 4);
            int test2 = BitConverter.ToInt32(_data, 8); //Name index if Test1 is actually a name. Should be 0 since we wouldn't have indexes here
            if (pcc.isName(test1) && test2 == 0) //is 0x4 a proper 8 byte name?
                result = 4;
            if (pcc.isName(test1) && pcc.isName(test2) && test2 != 0)
                result = 8;

            if (_data.Length > 0x10 && pcc.isName(test1) && pcc.getNameEntry(test1) == ObjectName && test0 == 0 && test2 == indexValue) //!= UIndex will cover more cases, but there's still the very tiny case where they line up
            {
                int test3 = BitConverter.ToInt32(_data, 0x10);
                string namev = pcc.getNameEntry(test3);
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

        private int? propsEndOffset;
        public int propsEnd()
        {
            if (propsEndOffset.HasValue)
            {
                return propsEndOffset.Value;
            }
            var props = GetProperties(true);
            propsEndOffset = props.endOffset;
            return propsEndOffset.Value;
        }

        public byte[] getBinaryData()
        {
            return _data.Skip(propsEnd()).ToArray();
        }

        public void setBinaryData(byte[] binaryData)
        {
            this.Data = _data.Take(propsEnd()).Concat(binaryData).ToArray();
        }
    }

    [DebuggerDisplay("UDKExportEntry | {UIndex} = {GetFullPath}")]
    public class UDKExportEntry : ExportEntry, IExportEntry
    {
        public UDKExportEntry(UDKPackage udkFile, Stream stream) : base(udkFile)
        {
            HeaderOffset = (uint)stream.Position;
            stream.Seek(44, SeekOrigin.Current);
            int count = stream.ReadValueS32();
            stream.Seek(-48, SeekOrigin.Current);

            int expInfoSize = 68 + (count * 4);
            Header = stream.ReadBytes(expInfoSize);
            OriginalDataSize = DataSize;
            long headerEnd = stream.Position;

            stream.Seek(DataOffset, SeekOrigin.Begin);
            _data = stream.ReadBytes(DataSize);
            stream.Seek(headerEnd, SeekOrigin.Begin);
            if ((ObjectFlags & (ulong)UnrealFlags.EObjectFlags.HasStack) != 0)
            {
                ReadsFromConfig = (Data[25] & 64) != 0;
            }
            else
            {
                ReadsFromConfig = false;
            }
        }

        public UDKExportEntry(UDKPackage pccFile) : base(pccFile)
        {
        }

        public IExportEntry Clone()
        {
            UDKExportEntry newExport = new UDKExportEntry(FileRef as UDKPackage)
            {
                Header = this.Header.TypedClone(),
                HeaderOffset = 0,
                Data = this.Data
            };
            int index = 0;
            string name = ObjectName;
            foreach (IExportEntry ent in FileRef.Exports)
            {
                if (name == ent.ObjectName && ent.indexValue > index)
                {
                    index = ent.indexValue;
                }
            }
            index++;
            newExport.indexValue = index;
            return newExport;
        }
    }

    [DebuggerDisplay("ME3ExportEntry | {UIndex} = {GetFullPath}")]
    public class ME3ExportEntry : ExportEntry, IExportEntry
    {
        public ME3ExportEntry(ME3Package pccFile, Stream stream) : base(pccFile)
        {
            HeaderOffset = (uint)stream.Position;
            stream.Seek(44, SeekOrigin.Current);
            int count = stream.ReadValueS32();
            stream.Seek(-48, SeekOrigin.Current);

            int expInfoSize = 68 + (count * 4);
            Header = stream.ReadBytes(expInfoSize);
            OriginalDataSize = DataSize;
            long headerEnd = stream.Position;

            stream.Seek(DataOffset, SeekOrigin.Begin);
            _data = stream.ReadBytes(DataSize);
            stream.Seek(headerEnd, SeekOrigin.Begin);
            if ((ObjectFlags & (ulong)UnrealFlags.EObjectFlags.HasStack) != 0)
            {
                ReadsFromConfig = (Data[25] & 64) != 0;
            }
            else
            {
                ReadsFromConfig = false;
            }
        }

        public ME3ExportEntry(ME3Package pccFile) : base(pccFile)
        {
        }

        public IExportEntry Clone()
        {
            ME3ExportEntry newExport = new ME3ExportEntry(FileRef as ME3Package)
            {
                Header = this.Header.TypedClone(),
                HeaderOffset = 0,
                Data = this.Data
            };
            int index = 0;
            string name = ObjectName;
            foreach (IExportEntry ent in FileRef.Exports)
            {
                if (name == ent.ObjectName && ent.indexValue > index)
                {
                    index = ent.indexValue;
                }
            }
            index++;
            newExport.indexValue = index;
            return newExport;
        }
    }

    [DebuggerDisplay("ME2ExportEntry | {UIndex} = {GetFullPath}")]
    public class ME2ExportEntry : ExportEntry, IExportEntry
    {
        public ME2ExportEntry(ME2Package pccFile, Stream stream) : base(pccFile)
        {
            //determine header length
            long start = stream.Position;
            stream.Seek(40, SeekOrigin.Current);
            int count = stream.ReadValueS32();
            stream.Seek(4 + count * 12, SeekOrigin.Current);
            count = stream.ReadValueS32();
            stream.Seek(16, SeekOrigin.Current);
            stream.Seek(4 + count * 4, SeekOrigin.Current);
            long end = stream.Position;
            stream.Seek(start, SeekOrigin.Begin);

            //read header
            Header = stream.ReadBytes((int)(end - start));
            HeaderOffset = (uint)start;
            OriginalDataSize = DataSize;

            //read data
            stream.Seek(DataOffset, SeekOrigin.Begin);
            _data = stream.ReadBytes(DataSize);
            stream.Seek(end, SeekOrigin.Begin);
            if ((ObjectFlags & (ulong)UnrealFlags.EObjectFlags.HasStack) != 0)
            {
                ReadsFromConfig = (Data[25] & 64) != 0;
            }
            else
            {
                ReadsFromConfig = false;
            }
        }

        public ME2ExportEntry(ME2Package pccFile) : base(pccFile)
        {
        }

        public IExportEntry Clone()
        {
            ME2ExportEntry newExport = new ME2ExportEntry(FileRef as ME2Package)
            {
                Header = this.Header.TypedClone(),
                HeaderOffset = 0,
                Data = this.Data
            };
            int index = 0;
            string name = ObjectName;
            foreach (IExportEntry ent in FileRef.Exports)
            {
                if (name == ent.ObjectName && ent.indexValue > index)
                {
                    index = ent.indexValue;
                }
            }
            index++;
            newExport.indexValue = index;
            return newExport;
        }
    }

    [DebuggerDisplay("ME1ExportEntry | {UIndex} = {GetFullPath}")]
    public class ME1ExportEntry : ExportEntry, IExportEntry
    {
        public ME1ExportEntry(ME1Package pccFile, Stream stream) : base(pccFile)
        {
            //determine header length
            long start = stream.Position;
            stream.Seek(40, SeekOrigin.Current);
            int count = stream.ReadValueS32();
            stream.Seek(4 + count * 12, SeekOrigin.Current);
            count = stream.ReadValueS32();
            stream.Seek(16, SeekOrigin.Current);
            stream.Seek(4 + count * 4, SeekOrigin.Current);
            long end = stream.Position;
            stream.Seek(start, SeekOrigin.Begin);

            //read header
            Header = stream.ReadBytes((int)(end - start));
            HeaderOffset = (uint)start;
            OriginalDataSize = DataSize;

            //read data
            stream.Seek(DataOffset, SeekOrigin.Begin);
            _data = stream.ReadBytes(DataSize);
            stream.Seek(end, SeekOrigin.Begin);
            if (ClassName.Contains("Property"))
            {
                ReadsFromConfig = Data.Length > 25 && (Data[25] & 64) != 0;
            }
            else
            {
                ReadsFromConfig = false;
            }
        }

        public ME1ExportEntry(ME1Package file) : base(file)
        {
        }

        public IExportEntry Clone()
        {
            ME1ExportEntry newExport = new ME1ExportEntry(FileRef as ME1Package)
            {
                Header = this.Header.TypedClone(),
                HeaderOffset = 0,
                Data = this.Data
            };
            int index = 0;
            string name = ObjectName;
            foreach (IExportEntry ent in FileRef.Exports)
            {
                if (name == ent.ObjectName && ent.indexValue > index)
                {
                    index = ent.indexValue;
                }
            }
            index++;
            newExport.indexValue = index;
            return newExport;
        }
    }
}
