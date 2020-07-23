using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ME3Explorer.Packages;
using System.Collections.ObjectModel;
using System.Collections;
using System.Diagnostics;
using System.Collections.Concurrent;
using Gammtek.Conduit.IO;
using StreamHelpers;

namespace ME3Explorer.Unreal
{
    public class PropertyCollection : ObservableCollection<Property>
    {
        static readonly ConcurrentDictionary<string, PropertyCollection> defaultStructValuesME3 = new ConcurrentDictionary<string, PropertyCollection>();
        static readonly ConcurrentDictionary<string, PropertyCollection> defaultStructValuesME2 = new ConcurrentDictionary<string, PropertyCollection>();
        static readonly ConcurrentDictionary<string, PropertyCollection> defaultStructValuesME1 = new ConcurrentDictionary<string, PropertyCollection>();

        public int endOffset;
        public bool IsImmutable;

        private readonly string TypeName;
        private readonly ClassInfo info;
        private readonly MEGame game;
        private readonly string sourceFilePath;
        private readonly int sourceExportUIndex;

        /// <summary>
        /// Gets the UProperty with the specified name, returns null if not found. The property name is checked case insensitively. 
        /// Ensure the generic type matches the result you want or you will receive a null object back.
        /// </summary>
        /// <param name="name">Name of property to find</param>
        /// <returns>specified UProperty or null if not found</returns>
        public T GetProp<T>(NameReference name) where T : Property
        {
            foreach (var prop in this)
            {
                if (prop.Name == name)
                {
                    return prop as T;
                }
            }
            return null;
        }

        /// <summary>
        /// Gets the UProperty with the specified name. Will get default values for properties that are part of the type but do not appear in the collection.
        /// The property name is checked case insensitively. 
        /// Ensure the property name is spelled correctly and that generic type matches the result you want or it will throw an exception.
        /// </summary>
        /// <param name="name">Name of property to find</param>
        /// <returns>specified UProperty</returns>
        public T GetPropOrDefault<T>(string name) where T : Property
        {
            foreach (var prop in this)
            {
                if (prop.Name.Name != null && string.Equals(prop.Name.Name, name, StringComparison.CurrentCultureIgnoreCase))
                {
                    return (T)prop;
                }
            }

            if (info.TryGetPropInfo(name, game, out PropertyInfo propInfo))
            {
                return (T)UnrealObjectInfo.getDefaultProperty(game, name, propInfo, true, IsImmutable);
            }
            //dynamic lookup
            try
            {
                using IMEPackage sourcePackage = MEPackageHandler.OpenMEPackage(sourceFilePath);
                ExportEntry exportToBuildFor = sourcePackage.GetUExport(sourceExportUIndex);
                if (!exportToBuildFor.IsClass && exportToBuildFor.Class is ExportEntry classExport)
                {
                    exportToBuildFor = classExport;
                }
                ClassInfo classInfo = UnrealObjectInfo.generateClassInfo(exportToBuildFor);
                if (classInfo.TryGetPropInfo(name, game, out propInfo))
                {
                    return (T)UnrealObjectInfo.getDefaultProperty(game, name, propInfo, true, IsImmutable);
                }
            }
            catch
            {
                throw new ArgumentException($"Property \"{name}\" does not exist on {TypeName}", nameof(name));
            }

            throw new ArgumentException($"Property \"{name}\" does not exist on {TypeName}", nameof(name));
        }

        public bool TryReplaceProp(Property prop)
        {
            for (int i = 0; i < this.Count; i++)
            {
                if (this[i].Name == prop.Name)
                {
                    this[i] = prop;
                    return true;
                }
            }
            return false;
        }

        public void AddOrReplaceProp(Property prop)
        {
            if (!TryReplaceProp(prop))
            {
                this.Add(prop);
            }
        }

        public void WriteTo(EndianWriter stream, IMEPackage pcc, bool requireNoneAtEnd = true)
        {
            foreach (var prop in this)
            {
                prop.WriteTo(stream, pcc, IsImmutable);
            }
            if (!IsImmutable && requireNoneAtEnd && (Count == 0 || !(this.Last() is NoneProperty)))
            {
                stream.WriteNoneProperty(pcc);
            }
        }

        /// <summary>
        /// Checks if a property with the specified name exists in this property collection
        /// </summary>
        /// <param name="name">Name of property to find. If an empty name is passed in, any property without a name will cause this to return true.</param>
        /// <returns>True if property is found, false if list is empty or not found</returns>
        public bool ContainsNamedProp(NameReference name)
        {
            return Count > 0 && this.Any(x => x.Name == name);
        }

        public PropertyCollection() { }

        public PropertyCollection(ExportEntry export, string typeName)
        {
            sourceExportUIndex = export.UIndex;
            sourceFilePath = export.FileRef.FilePath;
            TypeName = typeName;
            game = export.FileRef.Game;
            info = UnrealObjectInfo.GetClassOrStructInfo(game, typeName);
        }

        public static PropertyCollection ReadProps(ExportEntry export, Stream rawStream, string typeName, bool includeNoneProperty = false, bool requireNoneAtEnd = true, IEntry entry = null)
        {
            EndianReader stream = new EndianReader(rawStream) { Endian = export.FileRef.Endian };
            PropertyCollection props = new PropertyCollection(export, typeName);
            long startPosition = stream.Position;
            IMEPackage pcc = export.FileRef;
            try
            {
                while (stream.Position + 8 <= stream.Length)
                {
                    long propertyStartPosition = stream.Position;
                    int nameIdx = stream.ReadInt32();
                    if (!pcc.IsName(nameIdx))
                    {
                        stream.Seek(-4, SeekOrigin.Current);
                        break;
                    }
                    string name = pcc.GetNameEntry(nameIdx);
                    if (name == "None")
                    {
                        props.Add(new NoneProperty(stream) { StartOffset = propertyStartPosition, ValueOffset = propertyStartPosition });
                        stream.Seek(4, SeekOrigin.Current);
                        break;
                    }
                    NameReference nameRef = new NameReference(name, stream.ReadInt32());
                    int typeIdx = stream.ReadInt32();
                    stream.Seek(4, SeekOrigin.Current);
                    int size = stream.ReadInt32();
                    if (!pcc.IsName(typeIdx) || size < 0 || size > stream.Length - stream.Position)
                    {
                        stream.Seek(-16, SeekOrigin.Current);
                        break;
                    }
                    int staticArrayIndex = stream.ReadInt32();
                    PropertyType type;
                    string namev = pcc.GetNameEntry(typeIdx);
                    //Debug.WriteLine("Reading " + name + " (" + namev + ") at 0x" + (stream.Position - 24).ToString("X8"));
                    if (Enum.IsDefined(typeof(PropertyType), namev))
                    {
                        Enum.TryParse(namev, out type);
                    }
                    else
                    {
                        type = PropertyType.Unknown;
                    }

                    Property prop = null;
                    switch (type)
                    {
                        case PropertyType.StructProperty:
                            string structType = pcc.GetNameEntry(stream.ReadInt32());
                            stream.Seek(4, SeekOrigin.Current);
                            long valOffset = stream.Position;
                            if (UnrealObjectInfo.IsImmutable(structType, pcc.Game))
                            {
                                PropertyCollection structProps = ReadImmutableStruct(export, stream, structType, size, entry);
                                prop = new StructProperty(structType, structProps, nameRef, true) { ValueOffset = valOffset };
                            }
                            else
                            {
                                PropertyCollection structProps = ReadProps(export, stream.BaseStream, structType, includeNoneProperty, entry: entry);
                                prop = new StructProperty(structType, structProps, nameRef) { ValueOffset = valOffset };
                            }
                            break;
                        case PropertyType.IntProperty:
                            IntProperty ip = new IntProperty(stream, nameRef);
                            prop = ip;
                            break;
                        case PropertyType.FloatProperty:
                            prop = new FloatProperty(stream, nameRef);
                            break;
                        case PropertyType.ObjectProperty:
                            prop = new ObjectProperty(stream, nameRef);
                            break;
                        case PropertyType.NameProperty:
                            prop = new NameProperty(stream, pcc, nameRef);
                            break;
                        case PropertyType.BoolProperty:
                            prop = new BoolProperty(stream, pcc, nameRef);
                            break;
                        case PropertyType.BioMask4Property:
                            prop = new BioMask4Property(stream, nameRef);
                            break;
                        case PropertyType.ByteProperty:
                            {
                                if (size != 1)
                                {
                                    NameReference enumType;
                                    if (pcc.Game == MEGame.ME3 || pcc.Game == MEGame.UDK || pcc.Platform == MEPackage.GamePlatform.PS3)
                                    {
                                        enumType = new NameReference(pcc.GetNameEntry(stream.ReadInt32()), stream.ReadInt32());
                                    }
                                    else
                                    {
                                        //Debug.WriteLine("Reading enum for ME1/ME2 at 0x" + propertyStartPosition.ToString("X6"));

                                        //Attempt to get info without lookup first
                                        var enumname = UnrealObjectInfo.GetEnumType(pcc.Game, name, typeName);
                                        ClassInfo classInfo = null;
                                        if (enumname == null && entry is ExportEntry exp)
                                        {
                                            classInfo = UnrealObjectInfo.generateClassInfo(exp);
                                        }

                                        //Use DB info or attempt lookup
                                        enumType = new NameReference(enumname ?? UnrealObjectInfo.GetEnumType(pcc.Game, name, typeName, classInfo));
                                    }
                                    try
                                    {
                                        prop = new EnumProperty(stream, pcc, enumType, nameRef);
                                    }
                                    catch (Exception)
                                    {
                                        //ERROR
                                        //Debugger.Break();
                                        prop = new UnknownProperty(stream, 0, enumType, nameRef);
                                    }
                                }
                                else
                                {
                                    if (pcc.Game >= MEGame.ME3 || pcc.Platform == MEPackage.GamePlatform.PS3)
                                    {
                                        stream.Seek(8, SeekOrigin.Current);
                                    }
                                    prop = new ByteProperty(stream, nameRef);
                                }
                            }
                            break;
                        case PropertyType.ArrayProperty:
                            {
                                //Debug.WriteLine("Reading array properties, starting at 0x" + stream.Position.ToString("X5"));
                                var valStart = stream.Position;
                                prop = ReadArrayProperty(stream, export, typeName, nameRef, IncludeNoneProperties: includeNoneProperty, parsingEntry: entry);
                                //this can happen with m_aObjComments that were hex edited with old versions of the toolset
                                //technically valid, so we should support reading them
                                if (stream.Position < valStart + size)
                                {
                                    //Old code: JumpTo
                                    stream.Seek(valStart + size, SeekOrigin.Begin);
                                }
                            }
                            break;
                        case PropertyType.StrProperty:
                            {
                                prop = new StrProperty(stream, nameRef);
                            }
                            break;
                        case PropertyType.StringRefProperty:
                            prop = new StringRefProperty(stream, nameRef);
                            break;
                        case PropertyType.DelegateProperty:
                            prop = new DelegateProperty(stream, pcc, nameRef);
                            break;
                        case PropertyType.Unknown:
                            {
                                // Debugger.Break();
                                prop = new UnknownProperty(stream, size, pcc.GetNameEntry(typeIdx), nameRef);
                            }
                            break;
                        case PropertyType.None:
                            prop = new NoneProperty(stream);
                            break;
                    }

                    if (prop != null)
                    {
                        prop.StaticArrayIndex = staticArrayIndex;
                        prop.StartOffset = propertyStartPosition;
                        props.Add(prop);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine("Exception: " + e.Message);
            }
            if (props.Count > 0)
            {
                //error reading props.
                if (props[props.Count - 1].PropType != PropertyType.None && requireNoneAtEnd)
                {
                    if (entry != null)
                    {
                        Debug.WriteLine(entry.FileRef.FilePath);
                        Debug.WriteLine($"#{entry.UIndex} {entry.ObjectName.Instanced} - Invalid properties: Does not end with None");
                    }
#if DEBUG
                    props.endOffset = (int)stream.Position;
                    return props;
#else
                    stream.Seek(startPosition, SeekOrigin.Begin);
                    return new PropertyCollection { endOffset = (int)stream.Position };
#endif
                }
                //remove None Property
                if (props[props.Count - 1].PropType == PropertyType.None && !includeNoneProperty)
                {
                    props.RemoveAt(props.Count - 1);
                }
            }
            props.endOffset = (int)stream.Position;
            return props;
        }

        public static PropertyCollection ReadImmutableStruct(ExportEntry export, EndianReader stream, string structType, int size, IEntry parsingEntry = null)
        {
            IMEPackage pcc = export.FileRef;
            //strip transients unless this is a class definition
            bool stripTransients = !(parsingEntry != null && (parsingEntry.ClassName == "Class" || parsingEntry.ClassName == "ScriptStruct"));

            PropertyCollection props = new PropertyCollection(export, structType);
            PropertyCollection defaultProps;
            ConcurrentDictionary<string, PropertyCollection> defaultStructDict;
            Func<string, bool, PropertyCollection> getDefaultStructValueFunc;
            switch (pcc.Game)
            {
                case MEGame.ME3 when ME3UnrealObjectInfo.Structs.ContainsKey(structType):
                case MEGame.UDK when ME3UnrealObjectInfo.Structs.ContainsKey(structType):
                    defaultStructDict = defaultStructValuesME3;
                    getDefaultStructValueFunc = ME3UnrealObjectInfo.getDefaultStructValue;
                    break;
                case MEGame.ME2 when ME2Explorer.Unreal.ME2UnrealObjectInfo.Structs.ContainsKey(structType):
                    defaultStructDict = defaultStructValuesME2;
                    getDefaultStructValueFunc = ME2Explorer.Unreal.ME2UnrealObjectInfo.getDefaultStructValue;
                    break;
                case MEGame.ME1 when ME1Explorer.Unreal.ME1UnrealObjectInfo.Structs.ContainsKey(structType):
                    defaultStructDict = defaultStructValuesME1;
                    getDefaultStructValueFunc = ME1Explorer.Unreal.ME1UnrealObjectInfo.getDefaultStructValue;
                    break;
                default:
                    Debug.WriteLine("Unknown struct type: " + structType);
                    props.Add(new UnknownProperty(stream, size) { StartOffset = stream.Position });
                    return props;
            }

            //cache
            if (defaultStructDict.ContainsKey(structType) && stripTransients)
            {
                defaultProps = defaultStructDict[structType];
            }
            else
            {
                defaultProps = getDefaultStructValueFunc(structType, stripTransients);
                if (defaultProps == null)
                {
                    long startPos = stream.Position;
                    props.Add(new UnknownProperty(stream, size) { StartOffset = startPos });
                    return props;
                }
                if (stripTransients)
                {
                    defaultStructDict.TryAdd(structType, defaultProps);
                }
            }

            foreach (var prop in defaultProps)
            {
                Property property;
                if (prop is StructProperty defaultStructProperty)
                {
                    //Set correct struct type
                    property = ReadImmutableStructProp(export, stream, prop, structType, defaultStructProperty.StructType);
                }
                else
                {
                    property = ReadImmutableStructProp(export, stream, prop, structType);
                }

                if (property.PropType != PropertyType.None)
                {
                    props.Add(property);
                }
            }
            return props;
        }

        //Nested struct type is for structs in structs
        static Property ReadImmutableStructProp(ExportEntry export, EndianReader stream, Property template, string structType, string nestedStructType = null)
        {
            IMEPackage pcc = export.FileRef;
            if (stream.Position + 1 >= stream.Length)
            {
                throw new EndOfStreamException("tried to read past bounds of Export Data");
            }
            long startPos = stream.Position;

            switch (template.PropType)
            {
                case PropertyType.FloatProperty:
                    return new FloatProperty(stream, template.Name) { StartOffset = startPos };
                case PropertyType.IntProperty:
                    return new IntProperty(stream, template.Name) { StartOffset = startPos };
                case PropertyType.ObjectProperty:
                    return new ObjectProperty(stream, template.Name) { StartOffset = startPos };
                case PropertyType.StringRefProperty:
                    return new StringRefProperty(stream, template.Name) { StartOffset = startPos };
                case PropertyType.NameProperty:
                    return new NameProperty(stream, pcc, template.Name) { StartOffset = startPos };
                case PropertyType.BoolProperty:
                    //always say it's ME3 so that bools get read as 1 byte
                    return new BoolProperty(stream, pcc, template.Name, true) { StartOffset = startPos };
                case PropertyType.ByteProperty:
                    if (template is EnumProperty)
                    {
                        string enumType = UnrealObjectInfo.GetEnumType(pcc.Game, template.Name, structType);
                        return new EnumProperty(stream, pcc, enumType, template.Name) { StartOffset = startPos };
                    }
                    return new ByteProperty(stream, template.Name) { StartOffset = startPos };
                case PropertyType.BioMask4Property:
                    return new BioMask4Property(stream, template.Name) { StartOffset = startPos };
                case PropertyType.StrProperty:
                    return new StrProperty(stream, template.Name) { StartOffset = startPos };
                case PropertyType.ArrayProperty:
                    var arrayProperty = ReadArrayProperty(stream, export, structType, template.Name, true);
                    arrayProperty.StartOffset = startPos;
                    return arrayProperty;//this implementation needs checked, as I am not 100% sure of it's validity.
                case PropertyType.StructProperty:
                    long valuePos = stream.Position;
                    PropertyCollection structProps = ReadImmutableStruct(export, stream, UnrealObjectInfo.GetPropertyInfo(pcc.Game, template.Name, structType).Reference, 0);
                    var structProp = new StructProperty(nestedStructType ?? structType, structProps, template.Name, true)
                    {
                        StartOffset = startPos,
                        ValueOffset = valuePos
                    };
                    return structProp;//this implementation needs checked, as I am not 100% sure of it's validity.
                case PropertyType.None:
                    return new NoneProperty { StartOffset = startPos };
                case PropertyType.DelegateProperty:
                    return new DelegateProperty(stream, pcc, template.Name) { StartOffset = startPos };
                case PropertyType.Unknown:
                default:
                    throw new NotImplementedException("cannot read Unknown property of Immutable struct");
            }
        }

        public static Property ReadArrayProperty(EndianReader stream, ExportEntry export, string enclosingType, NameReference name, bool IsInImmutable = false, bool IncludeNoneProperties = false, IEntry parsingEntry = null)
        {
            IMEPackage pcc = export.FileRef;
            long arrayOffset = IsInImmutable ? stream.Position : stream.Position - 24;
            ArrayType arrayType = UnrealObjectInfo.GetArrayType(pcc.Game, name, enclosingType, parsingEntry);
            //Debug.WriteLine("Reading array length at 0x" + stream.Position.ToString("X5"));
            int count = stream.ReadInt32();
            switch (arrayType)
            {
                case ArrayType.Object:
                    {
                        var props = new List<ObjectProperty>();
                        for (int i = 0; i < count; i++)
                        {
                            long startPos = stream.Position;
                            props.Add(new ObjectProperty(stream) { StartOffset = startPos });
                        }
                        return new ArrayProperty<ObjectProperty>(arrayOffset, props, name) { Reference = "Object" }; //TODO: set reference to specific object type?
                    }
                case ArrayType.Name:
                    {
                        var props = new List<NameProperty>();
                        for (int i = 0; i < count; i++)
                        {
                            long startPos = stream.Position;
                            props.Add(new NameProperty(stream, pcc) { StartOffset = startPos });
                        }
                        return new ArrayProperty<NameProperty>(arrayOffset, props, name) { Reference = "Name" };
                    }
                case ArrayType.Enum:
                    {
                        //Attempt to get info without lookup first
                        var enumname = UnrealObjectInfo.GetEnumType(pcc.Game, name, enclosingType);
                        ClassInfo classInfo = null;
                        if (enumname == null && parsingEntry is ExportEntry parsingExport)
                        {
                            classInfo = UnrealObjectInfo.generateClassInfo(parsingExport);
                        }

                        //Use DB info or attempt lookup
                        NameReference enumType = new NameReference(enumname ?? UnrealObjectInfo.GetEnumType(pcc.Game, name, enclosingType, classInfo));

                        var props = new List<EnumProperty>();
                        for (int i = 0; i < count; i++)
                        {
                            long startPos = stream.Position;
                            props.Add(new EnumProperty(stream, pcc, enumType) { StartOffset = startPos });
                        }
                        return new ArrayProperty<EnumProperty>(arrayOffset, props, name) { Reference = enumType };
                    }
                case ArrayType.Struct:
                    {
                        long startPos = stream.Position;

                        var props = new List<StructProperty>();
                        var propertyInfo = UnrealObjectInfo.GetPropertyInfo(pcc.Game, name, enclosingType);
                        if (propertyInfo == null && parsingEntry is ExportEntry parsingExport)
                        {
                            var currentInfo = UnrealObjectInfo.generateClassInfo(parsingExport);
                            propertyInfo = UnrealObjectInfo.GetPropertyInfo(pcc.Game, name, enclosingType, currentInfo, parsingExport);
                        }

                        string arrayStructType = propertyInfo?.Reference;
                        if (IsInImmutable || UnrealObjectInfo.IsImmutable(arrayStructType, pcc.Game))
                        {
                            int arraySize = 0;
                            if (!IsInImmutable)
                            {
                                stream.Seek(-16, SeekOrigin.Current);
                                //Debug.WriteLine("Arraysize at 0x" + stream.Position.ToString("X5"));
                                arraySize = stream.ReadInt32();
                                stream.Seek(12, SeekOrigin.Current);
                            }
                            for (int i = 0; i < count; i++)
                            {
                                long offset = stream.Position;
                                try
                                {
                                    PropertyCollection structProps = ReadImmutableStruct(export, stream, arrayStructType, arraySize / count, parsingEntry: parsingEntry);
                                    props.Add(new StructProperty(arrayStructType, structProps, isImmutable: true)
                                    {
                                        StartOffset = offset,
                                        ValueOffset = offset
                                    });
                                }
                                catch (Exception e)
                                {
                                    Debug.WriteLine("ERROR READING ARRAY PROP");
                                    return new ArrayProperty<StructProperty>(arrayOffset, props, name);
                                }
                            }
                        }
                        else
                        {
                            for (int i = 0; i < count; i++)
                            {
                                long structOffset = stream.Position;
                                //Debug.WriteLine("reading array struct: " + arrayStructType + " at 0x" + stream.Position.ToString("X5"));
                                PropertyCollection structProps = ReadProps(export, stream.BaseStream, arrayStructType, includeNoneProperty: IncludeNoneProperties, entry: parsingEntry);
#if DEBUG
                                try
                                {
#endif
                                    props.Add(new StructProperty(arrayStructType, structProps)
                                    {
                                        StartOffset = structOffset,
                                        ValueOffset = structProps[0].StartOffset
                                    });
#if DEBUG
                                }
                                catch (Exception e)
                                {
                                    return new ArrayProperty<StructProperty>(arrayOffset, props, name);
                                }
#endif
                            }
                        }
                        return new ArrayProperty<StructProperty>(arrayOffset, props, name) { Reference = arrayStructType };
                    }
                case ArrayType.Bool:
                    {
                        var props = new List<BoolProperty>();
                        for (int i = 0; i < count; i++)
                        {
                            long startPos = stream.Position;
                            props.Add(new BoolProperty(stream, pcc, isArrayContained: true) { StartOffset = startPos });
                        }
                        return new ArrayProperty<BoolProperty>(arrayOffset, props, name) { Reference = "Bool" }; ;
                    }
                case ArrayType.String:
                    {
                        var props = new List<StrProperty>();
                        for (int i = 0; i < count; i++)
                        {
                            long startPos = stream.Position;
                            props.Add(new StrProperty(stream) { StartOffset = startPos });
                        }
                        return new ArrayProperty<StrProperty>(arrayOffset, props, name) { Reference = "String" }; ;
                    }
                case ArrayType.Float:
                    {
                        var props = new List<FloatProperty>();
                        for (int i = 0; i < count; i++)
                        {
                            long startPos = stream.Position;
                            props.Add(new FloatProperty(stream) { StartOffset = startPos });
                        }
                        return new ArrayProperty<FloatProperty>(arrayOffset, props, name) { Reference = "Float" }; ;
                    }
                case ArrayType.Byte:
                    return new ImmutableByteArrayProperty(arrayOffset, count, stream, name) { Reference = "Byte" }; ;
                case ArrayType.Int:
                default:
                    {
                        var props = new List<IntProperty>();
                        for (int i = 0; i < count; i++)
                        {
                            long startPos = stream.Position;
                            props.Add(new IntProperty(stream) { StartOffset = startPos });
                        }
                        return new ArrayProperty<IntProperty>(arrayOffset, props, name) { Reference = "Int" }; ;
                    }
            }
        }

        /// <summary>
        /// Removes the first matching property name from the collection.
        /// </summary>
        /// <param name="propname">Property name to remove</param>
        internal bool RemoveNamedProperty(string propname)
        {
            for (int i = 0; i < Count; i++)
            {
                var property = this[i];
                if (property.Name.Name.Equals(propname, StringComparison.InvariantCultureIgnoreCase))
                {
                    RemoveAt(i);
                    return true;
                }
            }
            return false;
        }
    }

    public abstract class Property : NotifyPropertyChangedBase
    {
        public abstract PropertyType PropType { get; }
        private NameReference _name;
        public int StaticArrayIndex { get; set; }
        /// <summary>
        /// Offset to the value for this property - note not all properties have actual values.
        /// </summary>
        public long ValueOffset;

        /// <summary>
        /// Offset to the start of this property as it was read by PropertyCollection.ReadProps()
        /// </summary>
        public long StartOffset { get; set; }

        public NameReference Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        protected Property(NameReference? name)
        {
            _name = name ?? new NameReference();
        }

        protected Property()
        {
            _name = new NameReference();
        }

        //public abstract void WriteTo(EndianWriter stream, IMEPackage pcc, bool valueOnly = false);
        public abstract void WriteTo(EndianWriter stream, IMEPackage pcc, bool valueOnly = false);

        /// <summary>
        /// Gets the length of this property in bytes. Do not use this if this is an ArrayProperty child object.
        /// </summary>
        /// <param name="pcc"></param>
        /// <param name="valueOnly"></param>
        /// <returns></returns>
        public long GetLength(IMEPackage pcc, bool valueOnly = false)
        {
            var stream = new EndianReader(new MemoryStream());
            WriteTo(stream.Writer, pcc, valueOnly);
            return stream.Length;
        }
    }

    [DebuggerDisplay("NoneProperty")]
    public class NoneProperty : Property
    {
        public override PropertyType PropType => PropertyType.None;

        public NoneProperty() : base("None") { }

        public NoneProperty(EndianReader stream) : this()
        {
            ValueOffset = stream.Position;
        }

        public override void WriteTo(EndianWriter stream, IMEPackage pcc, bool valueOnly = false)
        {
            if (!valueOnly)
            {
                stream.WriteNoneProperty(pcc);
            }
        }
    }

    [DebuggerDisplay("StructProperty | {Name.Name} - {StructType}")]
    public class StructProperty : Property
    {
        public override PropertyType PropType => PropertyType.StructProperty;

        private bool _isImmutable;
        private PropertyCollection _properties;

        public string StructType { get; }

        public PropertyCollection Properties
        {
            get => _properties;
            set
            {
                _properties = value;
                _properties.IsImmutable = _isImmutable;
            }
        }

        public bool IsImmutable
        {
            get => _isImmutable;
            set => Properties.IsImmutable = _isImmutable = value;
        }

        public StructProperty(string structType, PropertyCollection props, NameReference? name = null, bool isImmutable = false) : base(name)
        {
            StructType = structType;
            Properties = props ?? new PropertyCollection();
            IsImmutable = isImmutable;
        }

        public StructProperty(string structType, bool isImmutable, params Property[] props) : base(null)
        {
            StructType = structType;
            Properties = new PropertyCollection();
            IsImmutable = isImmutable;
            foreach (var prop in props)
            {
                Properties.Add(prop);
            }
        }

        public T GetProp<T>(string name) where T : Property
        {
            return Properties.GetProp<T>(name);
        }

        public T GetPropOrDefault<T>(string name) where T : Property
        {
            return Properties.GetPropOrDefault<T>(name);
        }

        public override void WriteTo(EndianWriter stream, IMEPackage pcc, bool valueOnly = false)
        {
            if (valueOnly)
            {
                Properties.WriteTo(stream, pcc);
            }
            else
            {
                stream.WriteStructProperty(pcc, Name, StructType, () =>
                {
                    EndianReader m = new EndianReader(new MemoryStream()){Endian = pcc.Endian};
                    Properties.WriteTo(m.Writer, pcc);
                    return m.BaseStream;
                }, StaticArrayIndex);
            }
        }
    }

    [DebuggerDisplay("IntProperty | {Name} = {Value}")]
    public class IntProperty : Property, IComparable
    {
        public override PropertyType PropType => PropertyType.IntProperty;

        int _value;
        public int Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }

        public IntProperty(EndianReader stream, NameReference? name = null) : base(name)
        {
            ValueOffset = stream.Position;
            Value = stream.ReadInt32();
        }

        public IntProperty(int val, NameReference? name = null) : base(name)
        {
            Value = val;
        }

        public IntProperty() { }

        public override void WriteTo(EndianWriter stream, IMEPackage pcc, bool valueOnly = false)
        {
            if (!valueOnly)
            {
                stream.WriteIntProperty(pcc, Name, Value, StaticArrayIndex);
            }
            else
            {
                stream.WriteInt32(Value);
            }
        }

        public int CompareTo(object obj)
        {
            switch (obj)
            {
                case null:
                    return 1;
                case IntProperty otherInt:
                    return Value.CompareTo(otherInt.Value);
                default:
                    throw new ArgumentException("Cannot compare IntProperty to object that is not of type IntProperty.");
            }
        }

        public static implicit operator IntProperty(int n)
        {
            return new IntProperty(n);
        }

        public static implicit operator int(IntProperty p)
        {
            return p?.Value ?? 0;
        }
    }

    [DebuggerDisplay("FloatProperty | {Name} = {Value}")]
    public class FloatProperty : Property, IComparable
    {
        public override PropertyType PropType => PropertyType.FloatProperty;

        float _value;
        public float Value
        {
            get => _value;
            set
            {
                //There is more than one way to represent 0 for float binary, and the normal == does not distinguish between them
                //for our purposes, we need to write out the exact binary we read in if no changes were made, so a custom comparison is needed
                if (!_value.IsBinarilyIdentical(value))
                {
                    _value = value;
                    OnPropertyChanged(nameof(Value));
                }
            }
        }

        public FloatProperty(EndianReader stream, NameReference? name = null) : base(name)
        {
            ValueOffset = stream.Position;
            Value = stream.ReadSingle();
        }

        public FloatProperty(float val, NameReference? name = null) : base(name)
        {
            Value = val;
        }

        public FloatProperty() { }

        public override void WriteTo(EndianWriter stream, IMEPackage pcc, bool valueOnly = false)
        {
            if (!valueOnly)
            {
                stream.WriteFloatProperty(pcc, Name, Value, StaticArrayIndex);
            }
            else
            {
                stream.WriteFloat(Value);
            }
        }

        public int CompareTo(object obj)
        {
            switch (obj)
            {
                case null:
                    return 1;
                case FloatProperty otherFloat:
                    return Value.CompareTo(otherFloat.Value);
                default:
                    throw new ArgumentException("Cannot compare FloatProperty to object that is not of type FloatProperty.");
            }
        }

        public static implicit operator FloatProperty(float n)
        {
            return new FloatProperty(n);
        }

        public static implicit operator float(FloatProperty p)
        {
            return p?.Value ?? 0;
        }
    }

    [DebuggerDisplay("ObjectProperty | {Name} = {Value}")]
    public class ObjectProperty : Property, IComparable
    {
        /// <summary>
        /// Resolves this object property to its corresponding entry from the package parameter
        /// </summary>
        /// <param name="package"></param>
        /// <returns></returns>
        public IEntry ResolveToEntry(IMEPackage package)
        {
            return package.IsEntry(Value) ? package.GetEntry(Value) : null;
        }
        public override PropertyType PropType => PropertyType.ObjectProperty;

        int _value;
        public int Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }

        public ObjectProperty(EndianReader stream, NameReference? name = null) : base(name)
        {
            ValueOffset = stream.Position;
            Value = stream.ReadInt32();
        }

        public ObjectProperty(int val, NameReference? name = null) : base(name)
        {
            Value = val;
        }

        public ObjectProperty(IEntry referencedEntry, NameReference? name = null) : base(name)
        {
            Value = referencedEntry.UIndex;
        }

        public ObjectProperty() { }

        public override void WriteTo(EndianWriter stream, IMEPackage pcc, bool valueOnly = false)
        {
            if (!valueOnly)
            {
                stream.WriteObjectProperty(pcc, Name, Value, StaticArrayIndex);
            }
            else
            {
                stream.WriteInt32(Value);
            }
        }

        public int CompareTo(object obj)
        {
            switch (obj)
            {
                case null:
                    return 1;
                case ObjectProperty otherObj:
                    return Value.CompareTo(otherObj.Value);
                default:
                    throw new ArgumentException("Cannot compare ObjectProperty to object that is not of type ObjectProperty.");
            }
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ObjectProperty);
        }

        public bool Equals(ObjectProperty p)
        {
            // If parameter is null, return false.
            if (p is null)
            {
                return false;
            }

            // Optimization for a common success case.
            if (ReferenceEquals(this, p))
            {
                return true;
            }

            // If run-time types are not exactly the same, return false.
            if (this.GetType() != p.GetType())
            {
                return false;
            }

            // Return true if the fields match.
            // Note that the base class is not invoked because it is
            // System.Object, which defines Equals as reference equality.
            return (Value == p.Value);
        }
    }

    [DebuggerDisplay("NameProperty | {Name} = {Value}")]
    public class NameProperty : Property
    {
        public override PropertyType PropType => PropertyType.NameProperty;

        NameReference _value;
        public NameReference Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }

        public NameProperty(NameReference value, NameReference? propertyName = null) : base(propertyName)
        {
            if (value is NameReference name)
            {
                Value = name;
            }
        }

        public NameProperty(EndianReader stream, IMEPackage pcc, NameReference? propertyName = null) : base(propertyName)
        {
            ValueOffset = stream.Position;
            Value = new NameReference(pcc.GetNameEntry(stream.ReadInt32()), stream.ReadInt32());
        }

        public NameProperty()
        {
            Value = "None";
        }

        public override void WriteTo(EndianWriter stream, IMEPackage pcc, bool valueOnly = false)
        {
            if (!valueOnly)
            {
                stream.WriteNameProperty(pcc, Name, Value, StaticArrayIndex);
            }
            else
            {
                stream.WriteInt32(pcc.FindNameOrAdd(Value.Name));
                stream.WriteInt32(Value.Number);
            }
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as NameProperty);
        }

        public bool Equals(NameProperty p)
        {
            // If parameter is null, return false.
            if (p is null)
            {
                return false;
            }

            // Optimization for a common success case.
            if (ReferenceEquals(this, p))
            {
                return true;
            }

            // If run-time types are not exactly the same, return false.
            if (GetType() != p.GetType())
            {
                return false;
            }

            // Return true if the fields match.
            // Note that the base class is not invoked because it is
            // System.Object, which defines Equals as reference equality.
            return Value == p.Value.Name && Value.Number == p.Value.Number;
        }

        public override string ToString()
        {
            return Value;
        }
    }

    [DebuggerDisplay("BoolProperty | {Name} = {Value}")]
    public class BoolProperty : Property
    {
        public override PropertyType PropType => PropertyType.BoolProperty;

        bool _value;
        public bool Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }

        public BoolProperty(EndianReader stream, IMEPackage pcc, NameReference? name = null, bool isArrayContained = false) : base(name)
        {
            ValueOffset = stream.Position;
            if (pcc.Game < MEGame.ME3 && pcc.Platform != MEPackage.GamePlatform.PS3 && isArrayContained)
            {
                //ME2 seems to read 1 byte... sometimes...
                //ME1 as well
                Value = stream.BaseStream.ReadBoolByte();
            }
            else
            {
                Value = (pcc.Game >= MEGame.ME3 || pcc.Platform == MEPackage.GamePlatform.PS3) ? stream.BaseStream.ReadBoolByte() : stream.ReadBoolInt();
            }
        }

        public BoolProperty(bool val, NameReference? name = null) : base(name)
        {
            Value = val;
        }

        public BoolProperty() { }

        public override void WriteTo(EndianWriter stream, IMEPackage pcc, bool valueOnly = false)
        {
            if (!valueOnly)
            {
                stream.WriteBoolProperty(pcc, Name, Value, StaticArrayIndex);
            }
            else
            {
                stream.WriteBoolByte(Value);

                //if (pcc.Game == MEGame.ME3 || isArrayContained)
                //{
                //    stream.WriteValueB8(Value);
                //}
                //else
                //{
                //    stream.WriteValueB32(Value);
                //}
            }
        }

        public static implicit operator BoolProperty(bool n)
        {
            return new BoolProperty(n);
        }

        public static implicit operator bool(BoolProperty p)
        {
            return p?.Value == true;
        }
    }

    [DebuggerDisplay("ByteProperty | {Name} = {Value}")]
    public class ByteProperty : Property
    {
        public override PropertyType PropType => PropertyType.ByteProperty;

        byte _value;
        public byte Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }

        public ByteProperty(byte val, NameReference? name = null) : base(name)
        {
            Value = val;
        }

        public ByteProperty(EndianReader stream, NameReference? name = null) : base(name)
        {
            ValueOffset = stream.Position;
            Value = (byte)stream.ReadByte();
        }

        public override void WriteTo(EndianWriter stream, IMEPackage pcc, bool valueOnly = false)
        {
            if (!valueOnly)
            {
                stream.WriteByteProperty(pcc, Name, Value, StaticArrayIndex);
            }
            else
            {
                stream.WriteByte(Value);
            }
        }
    }

    public class BioMask4Property : Property
    {
        public override PropertyType PropType => PropertyType.BioMask4Property;

        byte _value;
        public byte Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }

        public BioMask4Property(byte val, NameReference? name = null) : base(name)
        {
            Value = val;
        }

        public BioMask4Property(EndianReader stream, NameReference? name = null) : base(name)
        {
            ValueOffset = stream.Position;
            Value = (byte)stream.ReadByte();
        }

        public override void WriteTo(EndianWriter stream, IMEPackage pcc, bool valueOnly = false)
        {
            if (!valueOnly)
            {
                stream.WritePropHeader(pcc, Name, PropType, 1, StaticArrayIndex);
            }
            stream.WriteByte(Value);
        }
    }

    [DebuggerDisplay("EnumProperty | {Name} = {Value.Name}")]
    public class EnumProperty : Property
    {
        public override PropertyType PropType => PropertyType.ByteProperty;

        public NameReference EnumType { get; }
        NameReference _value;
        public NameReference Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }
        public List<NameReference> EnumValues { get; }

        public EnumProperty(EndianReader stream, IMEPackage pcc, NameReference enumType, NameReference? name = null) : base(name)
        {
            ValueOffset = stream.Position;
            EnumType = enumType;
            var eNameIdx = stream.ReadInt32();
            var eName = pcc.GetNameEntry(eNameIdx);
            var eNameNumber = stream.ReadInt32();

            Value = new NameReference(eName, eNameNumber);
            EnumValues = UnrealObjectInfo.GetEnumValues(pcc.Game, enumType, true);
        }

        public EnumProperty(NameReference value, NameReference enumType, MEGame meGame, NameReference? name = null) : base(name)
        {
            EnumType = enumType;
            NameReference enumVal = value;
            Value = enumVal;
            EnumValues = UnrealObjectInfo.GetEnumValues(meGame, enumType, true);
        }

        /// <summary>
        /// Creates an enum property and sets the value to the first item in the values list.
        /// </summary>
        /// <param name="enumType">Name of enum</param>
        /// <param name="meGame">Which game this property is for</param>
        /// <param name="name">Optional name of EnumProperty</param>
        public EnumProperty(NameReference enumType, MEGame meGame, NameReference? name = null) : base(name)
        {
            EnumType = enumType;
            EnumValues = UnrealObjectInfo.GetEnumValues(meGame, enumType, true);
            if (EnumValues == null)
            {
                Debugger.Break();
            }
            Value = EnumValues[0];
        }

        public override void WriteTo(EndianWriter stream, IMEPackage pcc, bool valueOnly = false)
        {
            if (!valueOnly)
            {
                stream.WriteEnumProperty(pcc, Name, EnumType, Value, StaticArrayIndex);
            }
            else
            {
                stream.WriteInt32(pcc.FindNameOrAdd(Value.Name));
                stream.WriteInt32(Value.Number);
            }
        }
    }

    public abstract class ArrayPropertyBase : Property, IEnumerable
    {
        public string Reference;

        public override PropertyType PropType => PropertyType.ArrayProperty;

        public abstract IReadOnlyList<Property> Properties { get; }
        public abstract int Count { get; }
        public bool IsReadOnly => true;

        protected ArrayPropertyBase(NameReference? name) : base(name)
        {
        }

        public IEnumerator GetEnumerator() => Properties.GetEnumerator();

        public abstract void Clear();

        public abstract void RemoveAt(int index);

        public Property this[int index] => Properties[index];

        public abstract void SwapElements(int i, int j);
    }

    public class ImmutableByteArrayProperty : ArrayPropertyBase
    {
        public byte[] bytes;
        public ImmutableByteArrayProperty(long startOffset, int count, EndianReader stream, NameReference? name) : base(name)
        {
            ValueOffset = startOffset;
            bytes = stream.ReadBytes(count);
        }

        public ImmutableByteArrayProperty(byte[] array, NameReference? name) : base(name)
        {
            bytes = array;
        }
        public ImmutableByteArrayProperty(NameReference? name) : base(name)
        {
            bytes = Array.Empty<byte>();
        }

        public override void WriteTo(EndianWriter stream, IMEPackage pcc, bool valueOnly = false)
        {
            if (!valueOnly)
            {
                stream.WriteArrayProperty(pcc, Name, bytes.Length, () =>
                {
                    Stream m = new MemoryStream();
                    m.WriteFromBuffer(bytes);
                    return m;
                }, StaticArrayIndex);
            }
            else
            {
                stream.WriteInt32(bytes.Length);
                stream.WriteFromBuffer(bytes);
            }
        }

        public override IReadOnlyList<Property> Properties => new List<Property>();
        public override int Count => bytes.Length;
        public override void Clear()
        {
            bytes = Array.Empty<byte>();
        }

        public override void RemoveAt(int index)
        {
        }

        public override void SwapElements(int i, int j)
        {
        }
    }

    [DebuggerDisplay("ArrayProperty<{typeof(T).Name,nq}> | {Name}, Length = {Values.Count}")]
    public class ArrayProperty<T> : ArrayPropertyBase, IList<T> where T : Property
    {
        public List<T> Values { get; set; }
        public override IReadOnlyList<Property> Properties => Values;

        public ArrayProperty(long startOffset, List<T> values, NameReference name) : this(values, name)
        {
            ValueOffset = startOffset;
        }

        public ArrayProperty(NameReference name) : this(new List<T>(), name)
        {
        }

        public ArrayProperty(IEnumerable<T> values, NameReference name) : this(values.ToList(), name)
        {
        }

        public ArrayProperty(List<T> values, NameReference name) : base(name)
        {
            Values = values;
        }

        public override void WriteTo(EndianWriter stream, IMEPackage pcc, bool valueOnly = false)
        {
            if (!valueOnly)
            {
                stream.WriteArrayProperty(pcc, Name, Values.Count, () =>
                {
                    EndianReader m = new EndianReader(new MemoryStream()){Endian = pcc.Endian};
                    foreach (var prop in Values)
                    {
                        prop.WriteTo(m.Writer, pcc, true);
                    }
                    return m.BaseStream;
                }, StaticArrayIndex);
            }
            else
            {
                stream.WriteInt32(Values.Count);
                foreach (var prop in Values)
                {
                    prop.WriteTo(stream, pcc, true);
                }
            }
        }

        #region IEnumerable<T>
        public new IEnumerator<T> GetEnumerator()
        {
            return Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return Values.GetEnumerator();
        }
        #endregion

        #region IList<T>
        public override int Count => Values.Count;
        public new bool IsReadOnly => ((ICollection<T>)Values).IsReadOnly;

        public new T this[int index]
        {
            get => Values[index];
            set => Values[index] = value;
        }

        public void Add(T item)
        {
            Values.Add(item);
        }

        public override void Clear()
        {
            Values.Clear();
        }

        public bool Contains(T item)
        {
            return Values.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            Values.CopyTo(array, arrayIndex);
        }

        public bool Remove(T item)
        {
            return Values.Remove(item);
        }

        public override void RemoveAt(int index)
        {
            Values.RemoveAt(index);
        }

        public int IndexOf(T item)
        {
            return Values.IndexOf(item);
        }

        public void Insert(int index, T item)
        {
            Values.Insert(index, item);
        }

        public void InsertRange(int index, IEnumerable<T> collection)
        {
            Values.InsertRange(index, collection);
        }
        #endregion

        public override void SwapElements(int i, int j)
        {
            if (i == j || i < 0 || i >= Count || j < 0 || j >= Count)
            {
                return;
            }

            (this[i], this[j]) = (this[j], this[i]);
        }
    }

    [DebuggerDisplay("StrProperty | {Name} = {Value}")]
    public class StrProperty : Property
    {
        public override PropertyType PropType => PropertyType.StrProperty;

        string _value;
        public string Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }

        public StrProperty(EndianReader stream, NameReference? name = null) : base(name)
        {
            ValueOffset = stream.Position;
            int count = stream.ReadInt32();
            var streamPos = stream.Position;

            //TODO: ENDIAN - Check if strings are stored in big endian or not (i don't think they are)
            if (count < -1) // originally 0
            {
                count *= -2;
                Value = stream.BaseStream.ReadStringUnicodeNull(count);
            }
            else if (count > 0)
            {
                Value = stream.BaseStream.ReadStringASCIINull(count);
            }
            else
            {
                Value = string.Empty;
                //ME3Explorer 3.0.2 and below wrote a null terminator character when writing an empty string.
                //The game however does not write an empty string if the length is 0 - it just happened to still work but not 100% of the time
                //This is for backwards compatibility with that as it will have a count of 0 instead of -1
                if (count == -1)
                {
                    stream.Position += 2;
                }
            }

            //for when the end of the string has multiple nulls at the end
            if (stream.Position < streamPos + count)
            {
                stream.Seek(streamPos + count, SeekOrigin.Begin);
            }
        }

        public StrProperty(string val, NameReference? name = null) : base(name)
        {
            Value = val ?? string.Empty;
        }

        public override void WriteTo(EndianWriter stream, IMEPackage pcc, bool valueOnly = false)
        {
            if (!valueOnly)
            {
                stream.WriteStringProperty(pcc, Name, Value, StaticArrayIndex);
            }
            else
            {
                if (pcc.Game == MEGame.ME3)
                {
                    stream.WriteUnrealStringUnicode(Value);
                }
                else
                {
                    stream.WriteUnrealStringASCII(Value);
                }
            }
        }

        public static implicit operator StrProperty(string s)
        {
            return new StrProperty(s);
        }

        public static implicit operator string(StrProperty p)
        {
            return p.Value;
        }

        public override string ToString()
        {
            return Value;
        }
    }

    [DebuggerDisplay("StringRefProperty | {Name} = {Value}")]
    public class StringRefProperty : Property
    {
        public override PropertyType PropType => PropertyType.StringRefProperty;

        int _value;
        public int Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }

        public StringRefProperty(EndianReader stream, NameReference? name = null) : base(name)
        {
            ValueOffset = stream.Position;
            Value = stream.ReadInt32();
        }

        public StringRefProperty(int val, NameReference? name = null) : base(name)
        {
            Value = val;
        }

        /// <summary>
        /// For constructing new property
        /// </summary>
        /// <param name="name"></param>
        public StringRefProperty(NameReference? name = null) : base(name) { }

        public override void WriteTo(EndianWriter stream, IMEPackage pcc, bool valueOnly = false)
        {
            if (!valueOnly)
            {
                stream.WriteStringRefProperty(pcc, Name, Value, StaticArrayIndex);
            }
            else
            {
                stream.WriteInt32(Value);
            }
        }
    }

    public class DelegateProperty : Property
    {
        public override PropertyType PropType => PropertyType.DelegateProperty;

        private ScriptDelegate _value;

        public ScriptDelegate Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }

        public DelegateProperty(EndianReader stream, IMEPackage pcc, NameReference? name = null) : base(name)
        {
            ValueOffset = stream.Position;
            Value = new ScriptDelegate(stream.ReadInt32(), new NameReference(pcc.GetNameEntry(stream.ReadInt32()), stream.ReadInt32()));
        }

        public DelegateProperty(int _object, NameReference functionName, NameReference? name = null) : base(name)
        {
            Value = new ScriptDelegate(_object, functionName);
        }

        public override void WriteTo(EndianWriter stream, IMEPackage pcc, bool valueOnly = false)
        {
            if (!valueOnly)
            {
                stream.WriteDelegateProperty(pcc, Name, Value, StaticArrayIndex);
            }
            else
            {
                stream.WriteInt32(Value.Object);
                stream.WriteNameReference(Value.FunctionName, pcc);
            }
        }
    }

    public class UnknownProperty : Property
    {
        public override PropertyType PropType => PropertyType.Unknown;

        public byte[] raw;
        public readonly string TypeName;

        public UnknownProperty(NameReference? name = null) : base(name)
        {
            raw = new byte[0];
        }

        public UnknownProperty(EndianReader stream, int size, string typeName = null, NameReference? name = null) : base(name)
        {
            ValueOffset = stream.Position;
            TypeName = typeName ?? "Unknown";
            raw = stream.ReadBytes(size);
        }

        public override void WriteTo(EndianWriter stream, IMEPackage pcc, bool valueOnly = false)
        {
            if (!valueOnly)
            {
                stream.WriteNameReference(Name, pcc);
                stream.WriteNameReference(TypeName, pcc);
                stream.WriteInt32(raw.Length);
                stream.WriteInt32(StaticArrayIndex);
            }
            stream.WriteFromBuffer(raw);
        }
    }
}
