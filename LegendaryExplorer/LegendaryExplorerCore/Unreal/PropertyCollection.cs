using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.DebugTools;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Memory;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using PropertyChanged;

#if AZURE
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace LegendaryExplorerCore.Unreal
{
    /// <summary>
    /// Collection of <see cref="Property"/>s
    /// </summary>
    public sealed class PropertyCollection : List<Property>
    {
        internal int EndOffset;

        /// <summary>
        /// Indicates that when serialized, the properties in this collection should use value-only serialization and that there should not be a <see cref="NoneProperty"/> at the end.
        /// </summary>
        public bool IsImmutable;

        /// <summary>
        /// Gets the <see cref="Property"/> with the specified name and optionally a static array index. The property name is checked case insensitively. 
        /// </summary>
        /// <remarks>Ensure the generic type matches the result you want or it will return null</remarks>
        /// <param name="name">Name of property to find</param>
        /// <param name="staticArrayIndex">Optional: If the named property is a static array (not an <see cref="ArrayProperty{T}"/>!), you can specify a specific element.</param>
        /// <returns>specified <typeparamref name="T"/> or null if not found</returns>
        public T GetProp<T>(NameReference name, int staticArrayIndex = 0) where T : Property
        {
            foreach (var prop in this)
            {
                if (prop.Name == name && prop.StaticArrayIndex == staticArrayIndex)
                {
                    return prop as T;
                }
            }
            return null;
        }

        /// <summary>
        /// If a property exists in the collection with the same <see cref="Property.Name"/> and <see cref="Property.StaticArrayIndex"/> as <paramref name="prop"/>, replace it,
        /// Otherwise do nothing.
        /// </summary>
        /// <param name="prop"></param>
        /// <returns>True if a property was replaced</returns>
        public bool TryReplaceProp(Property prop)
        {
            for (int i = 0; i < this.Count; i++)
            {
                if (this[i].Name == prop.Name && this[i].StaticArrayIndex == prop.StaticArrayIndex)
                {
                    this[i] = prop;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// If a property exists in the collection with the same <see cref="Property.Name"/> and <see cref="Property.StaticArrayIndex"/> as <paramref name="prop"/>, replace it,
        /// Otherwise add <paramref name="prop"/> to the collection.
        /// </summary>
        /// <param name="prop"></param>
        public void AddOrReplaceProp(Property prop)
        {
            if (!TryReplaceProp(prop))
            {
                Add(prop);
            }
        }

        /// <summary>
        /// Serializes the properties in this collection to an <see cref="EndianWriter"/>
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="pcc">The <see cref="IMEPackage"/> that these properties are in.</param>
        /// <param name="requireNoneAtEnd">Optional: Write a <see cref="NoneProperty"/> at the end if there isn't one. Ignored when <see cref="IsImmutable"/> is true</param>
        public void WriteTo(EndianWriter writer, IMEPackage pcc, bool requireNoneAtEnd = true)
        {
            foreach (var prop in this)
            {
                prop.WriteTo(writer, pcc, IsImmutable);
            }
            if (!IsImmutable && requireNoneAtEnd && (Count == 0 || this[^1] is not NoneProperty))
            {
                writer.WriteNoneProperty(pcc);
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

        /// <summary>
        /// Deserializes properties from <paramref name="rawStream"/>.
        /// </summary>
        /// <remarks>For advanced use. You should almost always just call <see cref="ExportEntry.GetProperties"/></remarks>
        /// <param name="export">The <see cref="ExportEntry"/> these props are from</param>
        /// <param name="rawStream">The stream to read the props from. This is assumed to taken from <paramref name="export"/>'s data.</param>
        /// <param name="typeName">The ClassName of the export, or the Name of the Struct being read.</param>
        /// <param name="includeNoneProperty">Optional: Should the returned <see cref="PropertyCollection"/> include the ending <see cref="NoneProperty"/></param>
        /// <param name="requireNoneAtEnd">Optional: Should this return an empty collection if there is no <see cref="NoneProperty"/> at the end</param>
        /// <param name="entry">Optional: The object these properties are logically in. Defaults to <paramref name="export"/>,
        /// but when parsing a DefaultObject or ScriptStruct defaults it should be the Class definition they belong to</param>
        /// <param name="packageCache">Optional: A cache that will be used for any neccesary lookups (such as default struct values)</param>
        /// <returns>A collection of properties read from <paramref name="rawStream"/> until a <see cref="NoneProperty"/> was found, or until the end of the stream if <paramref name="requireNoneAtEnd"/> is false</returns>
        public static PropertyCollection ReadProps(ExportEntry export, Stream rawStream, string typeName, bool includeNoneProperty = false, bool requireNoneAtEnd = true, IEntry entry = null, PackageCache packageCache = null)
        {
            entry ??= export;
            var stream = new EndianReader(rawStream) { Endian = export.FileRef.Endian };
#if !DEBUG
            long startPosition = stream.Position;//used in the non-DEBUG block at the end of this method!
#endif
            var props = new PropertyCollection();
            IMEPackage pcc = export.FileRef;
            try
            {
                while (stream.Position + 8 <= stream.Length)
                {
                    int propertyStartPosition = (int)stream.Position;
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
                    var nameRef = new NameReference(name, stream.ReadInt32());
                    int typeIdx = stream.ReadInt32();
                    stream.Seek(4, SeekOrigin.Current);
                    int size = stream.ReadInt32();
                    if (!pcc.IsName(typeIdx) || size < 0 || size > stream.Length - stream.Position)
                    {
                        stream.Seek(-16, SeekOrigin.Current);
                        LECLog.Warning($@"Found broken properties, could not resolve as name. Export: {export.InstancedFullPath}");
                        break;
                    }
                    int staticArrayIndex = stream.ReadInt32();
                    string namev = pcc.GetNameEntry(typeIdx);
                    //Debug.WriteLine("Reading " + nameRef.Instanced + " (" + namev + ") at 0x" + (stream.Position - 24).ToString("X8"));
                    if (!Enum.TryParse(namev, out PropertyType type))
                    {
                        type = PropertyType.Unknown;
                    }

                    Property prop = null;
                    switch (type)
                    {
                        case PropertyType.StructProperty:
                            string structType = pcc.GetNameEntry(stream.ReadInt32());
                            stream.Seek(4, SeekOrigin.Current);
                            int valOffset = (int)stream.Position;
                            if (GlobalUnrealObjectInfo.IsImmutable(structType, pcc.Game))
                            {
                                PropertyCollection defaultProps = null;
                                PropertyCollection structProps = ReadImmutableStruct(export, stream, structType, size, packageCache, ref defaultProps, entry);
                                prop = new StructProperty(structType, structProps, nameRef, true) { ValueOffset = valOffset };
                            }
                            else
                            {
                                PropertyCollection structProps = ReadProps(export, stream.BaseStream, structType, includeNoneProperty, entry: entry, packageCache: packageCache);
                                prop = new StructProperty(structType, structProps, nameRef) { ValueOffset = valOffset };
                            }
                            break;
                        case PropertyType.IntProperty:
                            prop = new IntProperty(stream, nameRef);
                            break;
                        case PropertyType.FloatProperty:
                            prop = new FloatProperty(stream, nameRef);
                            break;
                        case PropertyType.ObjectProperty:
                        case PropertyType.InterfaceProperty:
                        case PropertyType.ComponentProperty:
                            prop = new ObjectProperty(stream, nameRef)
                            {
                                InternalPropType = type
                            };
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
                                    if (pcc.Game is MEGame.ME3 or MEGame.LE1 or MEGame.LE2 or MEGame.LE3 or MEGame.UDK || pcc.Platform == MEPackage.GamePlatform.PS3)
                                    {
                                        enumType = new NameReference(pcc.GetNameEntry(stream.ReadInt32()), stream.ReadInt32());
                                    }
                                    else
                                    {
                                        //Debug.WriteLine("Reading enum for ME1/ME2 at 0x" + propertyStartPosition.ToString("X6"));

                                        //Attempt to get info without lookup first
                                        var enumname = GlobalUnrealObjectInfo.GetEnumType(pcc.Game, nameRef, typeName);
                                        ClassInfo classInfo = null;
                                        if (enumname == null && entry is ExportEntry exp)
                                        {
                                            classInfo = GlobalUnrealObjectInfo.generateClassInfo(exp, packageCache: packageCache);
                                        }

                                        //Use DB info or attempt lookup
                                        enumType = new NameReference(enumname ?? GlobalUnrealObjectInfo.GetEnumType(pcc.Game, nameRef, typeName == @"ScriptStruct" ? entry.ObjectName : typeName, classInfo));
                                    }
                                    try
                                    {
                                        prop = new EnumProperty(stream, pcc, enumType, nameRef);
                                    }
                                    catch (Exception ex)
                                    {
                                        //ERROR
                                        //Debugger.Break();
                                        LECLog.Warning($"Error parsing ByteProperty: {ex.Message}");
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
                                prop = ReadArrayProperty(stream, export, typeName, nameRef, IncludeNoneProperties: includeNoneProperty, parsingEntry: entry, packageCache: packageCache);
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
                if (requireNoneAtEnd && props[^1].PropType != PropertyType.None)
                {
                    if (entry != null)
                    {
                        Debug.WriteLine(entry.FileRef.FilePath);
                        Debug.WriteLine($"#{entry.UIndex} {entry.ObjectName.Instanced} - Invalid properties: Does not end with None");
                    }
#if DEBUG
                    props.EndOffset = (int)stream.Position;
                    return props;
#else
                    stream.Seek(startPosition, SeekOrigin.Begin);
                    return new PropertyCollection { EndOffset = (int)stream.Position };
#endif
                }
                //remove None Property
                if (!includeNoneProperty && props[^1].PropType == PropertyType.None)
                {
                    props.RemoveAt(props.Count - 1);
                }
            }
            props.EndOffset = (int)stream.Position;
            return props;
        }

        private static PropertyCollection ReadImmutableStruct(ExportEntry export, EndianReader stream, string structType, int size, PackageCache packageCache, ref PropertyCollection defaultProps, IEntry parsingEntry = null)
        {
            var props = new PropertyCollection();

            if (defaultProps is null)
            {
                IMEPackage pcc = export.FileRef;
                //strip transients unless this is a class definition
                bool stripTransients = parsingEntry is not {ClassName: "Class" or "ScriptStruct"};

                MEGame structValueLookupGame = pcc.Game;
                
                // This should be done already...
                //GlobalUnrealObjectInfo.EnsureLoaded(pcc.Game);
                switch (pcc.Game)
                {
                    case MEGame.ME1 when parsingEntry != null && parsingEntry.FileRef.Platform == MEPackage.GamePlatform.PS3 && ME3UnrealObjectInfo.ObjectInfo.Structs.ContainsKey(structType):
                    case MEGame.ME2 when parsingEntry != null && parsingEntry.FileRef.Platform == MEPackage.GamePlatform.PS3 && ME3UnrealObjectInfo.ObjectInfo.Structs.ContainsKey(structType):
                        structValueLookupGame = MEGame.ME3;
                        break;
                    case MEGame.ME3 when ME3UnrealObjectInfo.ObjectInfo.Structs.ContainsKey(structType):
                    case MEGame.UDK when UDKUnrealObjectInfo.ObjectInfo.Structs.ContainsKey(structType):
                    case MEGame.ME2 when ME2UnrealObjectInfo.ObjectInfo.Structs.ContainsKey(structType):
                    case MEGame.ME1 when ME1UnrealObjectInfo.ObjectInfo.Structs.ContainsKey(structType):
                    case MEGame.LE3 when LE3UnrealObjectInfo.ObjectInfo.Structs.ContainsKey(structType):
                    case MEGame.LE2 when LE2UnrealObjectInfo.ObjectInfo.Structs.ContainsKey(structType):
                    case MEGame.LE1 when LE1UnrealObjectInfo.ObjectInfo.Structs.ContainsKey(structType):
                        break;
                    default:
                        Debug.WriteLine("Unknown struct type: " + structType);
                        props.Add(new UnknownProperty(stream, size) { StartOffset = (int)stream.Position });
                        return props;
                }

                defaultProps = GlobalUnrealObjectInfo.getDefaultStructValue(structValueLookupGame, structType, stripTransients, packageCache, false);
                if (defaultProps == null)
                {
                    int startPos = (int)stream.Position;
                    props.Add(new UnknownProperty(stream, size) { StartOffset = startPos });
                    return props;
                }
            }

            foreach (var prop in defaultProps)
            {
                // Debug.WriteLine($"Reading immuatable property at 0x{stream?.Position:X8}: {prop?.Name}, in {structType}");
                Property property;
                if (prop is StructProperty defaultStructProperty)
                {
                    //Set correct struct type
                    property = ReadImmutableStructProp(export, stream, prop, structType, defaultStructProperty.StructType, packageCache);
                }
                else
                {
                    property = ReadImmutableStructProp(export, stream, prop, structType, packageCache: packageCache);
                }

                if (property.PropType != PropertyType.None)
                {
                    props.Add(property);
                }
            }
            return props;
        }

        //Nested struct type is for structs in structs
        private static Property ReadImmutableStructProp(ExportEntry export, EndianReader stream, Property template, string structType, string nestedStructType = null, PackageCache packageCache = null)
        {
            IMEPackage pcc = export.FileRef;
            if (stream.Position + 1 >= stream.Length)
            {
                throw new EndOfStreamException("tried to read past bounds of Export Data");
            }
            int startPos = (int)stream.Position;

            switch (template.PropType)
            {
                case PropertyType.FloatProperty:
                    return new FloatProperty(stream, template.Name) { StartOffset = startPos };
                case PropertyType.IntProperty:
                    return new IntProperty(stream, template.Name) { StartOffset = startPos };
                case PropertyType.ObjectProperty:
                case PropertyType.InterfaceProperty:
                case PropertyType.ComponentProperty:
                    return new ObjectProperty(stream, template.Name) { StartOffset = startPos, InternalPropType = template.PropType };
                case PropertyType.StringRefProperty:
                    return new StringRefProperty(stream, template.Name) { StartOffset = startPos };
                case PropertyType.NameProperty:
                    return new NameProperty(stream, pcc, template.Name) { StartOffset = startPos };
                case PropertyType.BoolProperty:
                    return new BoolProperty(stream, pcc, template.Name, true) { StartOffset = startPos };
                case PropertyType.ByteProperty:
                    if (template is EnumProperty)
                    {
                        string enumType = GlobalUnrealObjectInfo.GetEnumType(pcc.Game, template.Name, structType);
                        return new EnumProperty(stream, pcc, enumType, template.Name) { StartOffset = startPos };
                    }
                    return new ByteProperty(stream, template.Name) { StartOffset = startPos };
                case PropertyType.BioMask4Property:
                    return new BioMask4Property(stream, template.Name) { StartOffset = startPos };
                case PropertyType.StrProperty:
                    return new StrProperty(stream, template.Name) { StartOffset = startPos };
                case PropertyType.ArrayProperty:
                    var arrayProperty = ReadArrayProperty(stream, export, structType, template.Name, true, packageCache: packageCache);
                    arrayProperty.StartOffset = startPos;
                    return arrayProperty;//this implementation needs checked, as I am not 100% sure of it's validity.
                case PropertyType.StructProperty:
                    int valuePos = (int)stream.Position;
                    string reference = GlobalUnrealObjectInfo.GetPropertyInfo(pcc.Game, template.Name, structType, containingExport: export, packageCache: packageCache).Reference;
                    PropertyCollection defaultProps = null;
                    PropertyCollection structProps = ReadImmutableStruct(export, stream, reference, 0, packageCache, ref defaultProps, export);
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

        private static Property ReadArrayProperty(EndianReader stream, ExportEntry export, string enclosingType, NameReference name, bool IsInImmutable = false, bool IncludeNoneProperties = false, IEntry parsingEntry = null, PackageCache packageCache = null)
        {
            IMEPackage pcc = export.FileRef;
            long arrayOffset = IsInImmutable ? stream.Position : stream.Position - 24;
            ArrayType arrayType = GlobalUnrealObjectInfo.GetArrayType(pcc.Game, name, enclosingType == "ScriptStruct" ? export.ObjectName : enclosingType, parsingEntry, packageCache);
            // Debug.WriteLine($"Reading {enclosingType} array length at 0x" + stream.Position.ToString("X5"));
            int count = stream.ReadInt32();
            switch (arrayType)
            {
                case ArrayType.Object:
                    {
                        var props = new List<ObjectProperty>(count);
                        for (int i = 0; i < count; i++)
                        {
                            int startPos = (int)stream.Position;
                            props.Add(new ObjectProperty(stream) { StartOffset = startPos });
                        }
                        return new ArrayProperty<ObjectProperty>(arrayOffset, props, name) { Reference = "Object" }; //TODO: set reference to specific object type?
                    }
                case ArrayType.Name:
                    {
                        var props = new List<NameProperty>(count);
                        for (int i = 0; i < count; i++)
                        {
                            int startPos = (int)stream.Position;
                            props.Add(new NameProperty(stream, pcc) { StartOffset = startPos });
                        }
                        return new ArrayProperty<NameProperty>(arrayOffset, props, name) { Reference = "Name" };
                    }
                case ArrayType.Enum:
                    {
                        //Attempt to get info without lookup first
                        // PS3 is based on ME3 engine. So use ME3
                        var enumname = GlobalUnrealObjectInfo.GetEnumType(pcc.Platform != MEPackage.GamePlatform.PS3 ? pcc.Game : MEGame.ME3, name, enclosingType);
                        ClassInfo classInfo = null;
                        if (enumname == null && parsingEntry is ExportEntry parsingExport)
                        {
                            classInfo = GlobalUnrealObjectInfo.generateClassInfo(parsingExport, packageCache: packageCache);
                        }

                        //Use DB info or attempt lookup
                        var enumType = new NameReference(enumname ?? GlobalUnrealObjectInfo.GetEnumType(pcc.Game, name, enclosingType, classInfo));

                        var props = new List<EnumProperty>(count);
                        for (int i = 0; i < count; i++)
                        {
                            int startPos = (int)stream.Position;
                            props.Add(new EnumProperty(stream, pcc, enumType) { StartOffset = startPos });
                        }
                        return new ArrayProperty<EnumProperty>(arrayOffset, props, name) { Reference = enumType };
                    }
                case ArrayType.Struct:
                    {
                        var props = new List<StructProperty>(count);
                        var propertyInfo = GlobalUnrealObjectInfo.GetPropertyInfo(pcc.Game, name, enclosingType, containingExport: parsingEntry as ExportEntry, packageCache: packageCache);
                        if (propertyInfo == null && parsingEntry is ExportEntry parsingExport)
                        {
                            var currentInfo = GlobalUnrealObjectInfo.generateClassInfo(parsingExport, packageCache: packageCache);
                            propertyInfo = GlobalUnrealObjectInfo.GetPropertyInfo(pcc.Game, name, enclosingType, currentInfo, parsingExport, packageCache: packageCache);
                        }

                        string arrayStructType = propertyInfo?.Reference;
                        if (IsInImmutable || GlobalUnrealObjectInfo.IsImmutable(arrayStructType, pcc.Platform != MEPackage.GamePlatform.PS3 ? pcc.Game : MEGame.ME3))
                        {
                            int arraySize = 0;
                            if (!IsInImmutable)
                            {
                                stream.Seek(-12, SeekOrigin.Current);
                                //Debug.WriteLine("Arraysize at 0x" + stream.Position.ToString("X5"));
                                arraySize = stream.ReadInt32();
                                stream.Seek(8, SeekOrigin.Current);
                            }
                            PropertyCollection defaultProps = null;
                            for (int i = 0; i < count; i++)
                            {
                                int offset = (int)stream.Position;
                                try
                                {
                                    PropertyCollection structProps = ReadImmutableStruct(export, stream, arrayStructType, arraySize / count, packageCache, ref defaultProps, parsingEntry);
                                    props.Add(new StructProperty(arrayStructType, structProps, isImmutable: true)
                                    {
                                        StartOffset = offset,
                                        ValueOffset = offset
                                    });
                                }
                                catch (Exception)
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
                                int structOffset = (int)stream.Position;
                                //Debug.WriteLine("reading array struct: " + arrayStructType + " at 0x" + stream.Position.ToString("X5"));
                                PropertyCollection structProps = ReadProps(export, stream.BaseStream, arrayStructType, includeNoneProperty: IncludeNoneProperties, entry: parsingEntry, packageCache: packageCache);
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
                                catch (Exception)
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
                        var props = new List<BoolProperty>(count);
                        for (int i = 0; i < count; i++)
                        {
                            int startPos = (int)stream.Position;
                            props.Add(new BoolProperty(stream, pcc, isArrayContained: true) { StartOffset = startPos });
                        }
                        return new ArrayProperty<BoolProperty>(arrayOffset, props, name) { Reference = "Bool" };
                    }
                case ArrayType.String:
                    {
                        var props = new List<StrProperty>(count);
                        for (int i = 0; i < count; i++)
                        {
                            int startPos = (int)stream.Position;
                            props.Add(new StrProperty(stream) { StartOffset = startPos });
                        }
                        return new ArrayProperty<StrProperty>(arrayOffset, props, name) { Reference = "String" };
                    }
                case ArrayType.Float:
                    {
                        var props = new List<FloatProperty>(count);
                        for (int i = 0; i < count; i++)
                        {
                            int startPos = (int)stream.Position;
                            props.Add(new FloatProperty(stream) { StartOffset = startPos });
                        }
                        return new ArrayProperty<FloatProperty>(arrayOffset, props, name) { Reference = "Float" };
                    }
                case ArrayType.Byte:
                    return new ImmutableByteArrayProperty(arrayOffset, count, stream, name) { Reference = "Byte" };
                case ArrayType.StringRef:
                    {
                        var props = new List<StringRefProperty>(count);
                        for (int i = 0; i < count; i++)
                        {
                            int startPos = (int)stream.Position;
                            props.Add(new StringRefProperty(stream) { StartOffset = startPos });
                        }
                        return new ArrayProperty<StringRefProperty>(arrayOffset, props, name) { Reference = "StringRef" };
                    }
                case ArrayType.Int:
                default:
                    {
                        var props = new List<IntProperty>(count);
                        for (int i = 0; i < count; i++)
                        {
                            int startPos = (int)stream.Position;
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
        public bool RemoveNamedProperty(string propname)
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

        /// <summary>
        /// Creates a deep copy of the <see cref="PropertyCollection"/>
        /// </summary>
        /// <returns>A deep copy of the <see cref="PropertyCollection"/></returns>
        public PropertyCollection DeepClone()
        {
            var clone = new PropertyCollection {EndOffset = EndOffset, IsImmutable = IsImmutable};
            for (int i = 0; i < Count; i++)
            {
                clone.Add(this[i].DeepClone());
            }
            return clone;
        }

        /// <summary>
        /// Checks to see if the two collections have the same properties (defined by <see cref="Property.Equivalent"/>). Order does not matter
        /// </summary>
        /// <param name="other">The <see cref="PropertyCollection"/> to compare to.</param>
        /// <returns>True if the <see cref="PropertyCollection"/>s are equivalent, false if not</returns>
        public bool Equivalent(PropertyCollection other)
        {
            if (other?.Count != Count)
            {
                return false;
            }
            foreach (Property otherProp in other)
            {
                if (GetProp<Property>(otherProp.Name, otherProp.StaticArrayIndex) is not Property prop || !prop.Equivalent(otherProp))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Returns a new <see cref="PropertyCollection"/> containing deep clones of all properties in this collection that are not <see cref="Property.Equivalent"/> to ones in <paramref name="other"/>
        /// </summary>
        /// <param name="other"></param>
        /// <param name="structDiff"> If true, will diff within non-atomic structs. If false, will treat all structs as atomic</param>
        /// <returns></returns>
        public PropertyCollection Diff(PropertyCollection other, bool structDiff = false)
        {
            var diff = new PropertyCollection();
            foreach (Property thisProp in this)
            {
                if (other.GetProp<Property>(thisProp.Name, thisProp.StaticArrayIndex) is not Property otherProp)
                {
                    diff.Add(thisProp.DeepClone());
                }
                else if (!thisProp.Equivalent(otherProp))
                {
                    if (structDiff && thisProp is StructProperty {IsImmutable: false} thisStruct && otherProp is StructProperty otherStruct)
                    {
                        diff.Add(new StructProperty(thisStruct.StructType, thisStruct.Properties.Diff(otherStruct.Properties), thisStruct.Name, thisStruct.IsImmutable));
                    }
                    else
                    {
                        diff.Add(thisProp.DeepClone());
                    }
                }
            }
            return diff;
        }
    }

    /// <summary>
    /// Base class for all the Unreal property types that go in <see cref="PropertyCollection"/>
    /// </summary>
    public abstract class Property
    {
        /// <summary>
        /// The type of the Property. Mostly useful for getting the string representation.
        /// </summary>
        public abstract PropertyType PropType { get; }

        /// <summary>
        /// Some properties are defined as static arrays. Each element will be a seperate <see cref="Property"/>, and this is the index.
        /// </summary>
        public int StaticArrayIndex { get; set; }

        /// <summary>
        /// Offset to the value for this property - note not all properties have actual values.
        /// </summary>
        public int ValueOffset;

        /// <summary>
        /// Offset to the start of this property as it was read by PropertyCollection.ReadProps()
        /// </summary>
        public int StartOffset { get; set; }

        /// <summary>
        /// The name of this property
        /// </summary>
        public NameReference Name { get; set; }

        protected internal Property(NameReference? name)
        {
            Name = name ?? new NameReference();
        }

        protected internal Property()
        {
            Name = new NameReference();
        }

        /// <summary>
        /// Serializes this property to an <see cref="EndianWriter"/>.
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="pcc">The <see cref="IMEPackage"/> that this property is in.</param>
        /// <param name="valueOnly">Should the property header be serialized.</param>
        public abstract void WriteTo(EndianWriter writer, IMEPackage pcc, bool valueOnly = false);

        /// <summary>
        /// Gets the length of this property in bytes. Do not use this if this is an ArrayProperty child object.
        /// </summary>
        /// <remarks>Works by serializing the property and seeing how much was serialized, so it's not cheap.</remarks>
        /// <param name="pcc"></param>
        /// <param name="valueOnly">Should the property header be counted in the length.</param>
        /// <returns></returns>
        public long GetLength(IMEPackage pcc, bool valueOnly = false)
        {
            using var stream = new EndianReader(MemoryManager.GetMemoryStream());
            WriteTo(stream.Writer, pcc, valueOnly);
            return stream.Length;
        }

        /// <summary>
        /// Creates a deep copy of the property
        /// </summary>
        /// <returns>A deep copy of the property</returns>
        public abstract Property DeepClone();

        /// <summary>
        /// Checks if this represents the same property (Name and StaticArrayIndex), and has the same value.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public virtual bool Equivalent(Property other) => other.Name == Name && other.StaticArrayIndex == StaticArrayIndex;
    }

    /// <summary>
    /// Property that marks the end of a collection of properties.
    /// </summary>
    [DebuggerDisplay("NoneProperty")]
    public sealed class NoneProperty : Property
    {
        ///<inheritdoc/>
        public override PropertyType PropType => PropertyType.None;

        /// <summary>
        /// Creates a <see cref="NoneProperty"/>
        /// </summary>
        public NoneProperty() : base(NameReference.None) { }

        internal NoneProperty(EndianReader stream) : this()
        {
            ValueOffset = (int)stream.Position;
        }

        ///<inheritdoc/>
        public override void WriteTo(EndianWriter writer, IMEPackage pcc, bool valueOnly = false)
        {
            if (!valueOnly)
            {
                writer.WriteNoneProperty(pcc);
            }
        }
        ///<inheritdoc/>
        public override NoneProperty DeepClone() => (NoneProperty)MemberwiseClone();

        /// <inheritdoc />
        public override bool Equivalent(Property other) => other is NoneProperty;
    }

    /// <summary>
    /// Property that represents a struct
    /// </summary>
    [DebuggerDisplay("StructProperty | {Name.Instanced} - {StructType}")]
    public class StructProperty : Property, INotifyPropertyChanged
    {
        ///<inheritdoc/>
        public override PropertyType PropType => PropertyType.StructProperty;

        /// <summary>
        /// The name of the Struct this represents
        /// </summary>
        public string StructType { get; }

        /// <summary>
        /// The properties in this struct
        /// </summary>
        [OnChangedMethod(nameof(OnPropertiesChanged))]
        public PropertyCollection Properties { get; set; }

        /// <summary>
        /// Whether this represents a struct that uses value-only serialization
        /// </summary>
        [OnChangedMethod(nameof(OnIsImmutableChanged))]
        public bool IsImmutable { get; set; }

        private void OnPropertiesChanged()
        {
            if (Properties != null)
            {
                Properties.IsImmutable = IsImmutable;
            }
        }

        private void OnIsImmutableChanged()
        {
            Properties.IsImmutable = IsImmutable;
        }

        /// <summary>
        /// Creates a <see cref="StructProperty"/>.
        /// </summary>
        /// <param name="structType">The name of the Struct this represents</param>
        /// <param name="props">The properties in the struct</param>
        /// <param name="name">Tha property name</param>
        /// <param name="isImmutable">Whether this represents a struct that uses value-only serialization</param>
        public StructProperty(string structType, PropertyCollection props, NameReference? name = null, bool isImmutable = false) : base(name)
        {
            StructType = structType;
            Properties = props ?? [];
            IsImmutable = isImmutable;
        }

        /// <summary>
        /// Creates a <see cref="StructProperty"/>.
        /// </summary>
        /// <param name="structType">The name of the Struct this represents</param>
        /// <param name="isImmutable">Whether this represents a struct that uses value-only serialization</param>
        /// <param name="props">The properties in the struct</param>
        public StructProperty(string structType, bool isImmutable, params Property[] props) : base(null)
        {
            StructType = structType;
            Properties = [];
            IsImmutable = isImmutable;
            foreach (var prop in props)
            {
                Properties.Add(prop);
            }
        }

        ///<inheritdoc cref="PropertyCollection.GetProp{T}"/>
        public T GetProp<T>(string name, int staticArrayIndex = 0) where T : Property
        {
            return Properties.GetProp<T>(name, staticArrayIndex);
        }

        ///<inheritdoc/>
        public override void WriteTo(EndianWriter writer, IMEPackage pcc, bool valueOnly = false)
        {
            if (valueOnly)
            {
                Properties.WriteTo(writer, pcc);
            }
            else
            {
                writer.WriteStructProperty(pcc, Name, StructType, () =>
                {
                    var m = new EndianReader(MemoryManager.GetMemoryStream()) { Endian = pcc.Endian };
                    Properties.WriteTo(m.Writer, pcc);
                    return m.BaseStream;
                }, StaticArrayIndex);
            }
        }

        ///<inheritdoc/>
        public override StructProperty DeepClone()
        {
            var clone = (StructProperty)MemberwiseClone();
            clone.Properties = Properties.DeepClone();
            return clone;
        }

        ///<inheritdoc/>
        public override bool Equivalent(Property other) => other is StructProperty structProperty && base.Equivalent(structProperty) && structProperty.StructType.CaseInsensitiveEquals(StructType)
                                                           && Properties.Equivalent(structProperty.Properties);

        /// <summary>
        /// Generates a StructProperty (with the specified name) from the specified Guid
        /// </summary>
        /// <param name="guid"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public static StructProperty FromGuid(Guid guid, string name = null)
        {
            var pc = new PropertyCollection();
            var ba = guid.ToByteArray();
            pc.Add(new IntProperty(BitConverter.ToInt32(ba, 0), "A"));
            pc.Add(new IntProperty(BitConverter.ToInt32(ba, 4), "B"));
            pc.Add(new IntProperty(BitConverter.ToInt32(ba, 8), "C"));
            pc.Add(new IntProperty(BitConverter.ToInt32(ba, 12), "D"));
            return new StructProperty("Guid", pc, name, true);
        }

#pragma warning disable
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore
    }

    /// <summary>
    /// Property containing a 32-bit signed integer
    /// </summary>
    [DebuggerDisplay("IntProperty | {Name.Instanced} = {Value}")]
    public sealed class IntProperty : Property, IComparable, INotifyPropertyChanged
    {
        ///<inheritdoc/>
        public override PropertyType PropType => PropertyType.IntProperty;

        /// <summary>
        /// The integer this property contains
        /// </summary>
        public int Value { get; set; }

        /// <summary>
        /// Creates an <see cref="IntProperty"/>
        /// </summary>
        /// <param name="value">The integer value</param>
        /// <param name="name">Optional: The property name. This should only be null when it's in an <see cref="ArrayProperty{T}"/></param>
        public IntProperty(int value, NameReference? name = null) : base(name)
        {
            Value = value;
        }

        internal IntProperty(EndianReader stream, NameReference? name = null) : base(name)
        {
            ValueOffset = (int)stream.Position;
            Value = stream.ReadInt32();
        }

        ///<inheritdoc/>
        public override void WriteTo(EndianWriter writer, IMEPackage pcc, bool valueOnly = false)
        {
            if (!valueOnly)
            {
                writer.WriteIntProperty(pcc, Name, Value, StaticArrayIndex);
            }
            else
            {
                writer.WriteInt32(Value);
            }
        }

        ///<inheritdoc/>
        public override IntProperty DeepClone() => (IntProperty) MemberwiseClone();
        ///<inheritdoc/>
        public override bool Equivalent(Property other) => other is IntProperty intProperty && base.Equivalent(intProperty) && intProperty.Value == Value;

        int IComparable.CompareTo(object obj)
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

        public event PropertyChangedEventHandler PropertyChanged;
    }

    /// <summary>
    /// Property containing a single precision floating point number
    /// </summary>
    [DebuggerDisplay("FloatProperty | {Name.Instanced} = {Value}")]
    public sealed class FloatProperty : Property, IComparable, INotifyPropertyChanged
    {
        ///<inheritdoc/>
        public override PropertyType PropType => PropertyType.FloatProperty;

        private float _value;
        /// <summary>
        /// The float this property contains
        /// </summary>
        [DoNotNotify] //we're doing it manually
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
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
                }
            }
        }

        /// <summary>
        /// Creates a <see cref="FloatProperty"/>
        /// </summary>
        /// <param name="value">The float value</param>
        /// <param name="name">Optional: The property name. This should only be null when it's in an <see cref="ArrayProperty{T}"/></param>
        public FloatProperty(float value, NameReference? name = null) : base(name)
        {
            Value = value;
        }

        internal FloatProperty(EndianReader stream, NameReference? name = null) : base(name)
        {
            ValueOffset = (int)stream.Position;
            Value = stream.ReadFloat();
        }

        ///<inheritdoc/>
        public override void WriteTo(EndianWriter writer, IMEPackage pcc, bool valueOnly = false)
        {
            if (!valueOnly)
            {
                writer.WriteFloatProperty(pcc, Name, Value, StaticArrayIndex);
            }
            else
            {
                writer.WriteFloat(Value);
            }
        }

        ///<inheritdoc/>
        public override FloatProperty DeepClone() => (FloatProperty) MemberwiseClone();
        ///<inheritdoc/>
        public override bool Equivalent(Property other) => other is FloatProperty floatProperty && base.Equivalent(floatProperty) && floatProperty.Value == Value;

        int IComparable.CompareTo(object obj)
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

        public event PropertyChangedEventHandler PropertyChanged;
    }

    /// <summary>
    /// Property containing a UIndex
    /// </summary>
    [DebuggerDisplay("ObjectProperty | {Name.Instanced} = {Value}")]
    public sealed class ObjectProperty : Property, IComparable, INotifyPropertyChanged
    {
        /// <summary>
        /// Resolves this object property to its corresponding <see cref="IEntry"/> from <paramref name="package"/>.
        /// </summary>
        /// <param name="package">The package to look up the UIndex in</param>
        /// <returns>An IEntry, or null if <see cref="Value"/> is 0 or outside the range of valid UIndexes</returns>
        public IEntry ResolveToEntry(IMEPackage package) => package.GetEntry(Value);

        ///<inheritdoc/>
        public override PropertyType PropType => PropertyType.ObjectProperty;

        //We use ObjectProperty to represent InterfaceProperty and ComponentProperty too. This stores the "real" type.
        public PropertyType InternalPropType = PropertyType.ObjectProperty;

        /// <summary>
        /// The UIndex this property contains
        /// </summary>
        public int Value { get; set; }

        /// <summary>
        /// Creates an <see cref="ObjectProperty"/>
        /// </summary>
        /// <param name="val">The UIndex this property will contain</param>
        /// <param name="name">Optional: The property name. This should only be null when it's in an <see cref="ArrayProperty{T}"/></param>
        public ObjectProperty(int val, NameReference? name = null) : base(name)
        {
            Value = val;
        }

        /// <summary>
        /// Creates an <see cref="ObjectProperty"/>
        /// </summary>
        /// <param name="referencedEntry">The <see cref="IEntry"/> whose UIndex this property will contain</param>
        /// <param name="name">Optional: The property name. This should only be null when it's in an <see cref="ArrayProperty{T}"/></param>
        public ObjectProperty(IEntry referencedEntry, NameReference? name = null) : base(name)
        {
            Value = referencedEntry?.UIndex ?? 0;
        }

        internal ObjectProperty(EndianReader stream, NameReference? name = null) : base(name)
        {
            ValueOffset = (int)stream.Position;
            Value = stream.ReadInt32();
        }

        ///<inheritdoc/>
        public override void WriteTo(EndianWriter writer, IMEPackage pcc, bool valueOnly = false)
        {
            if (!valueOnly)
            {
                writer.WriteObjectProperty(pcc, Name, Value, StaticArrayIndex, InternalPropType);
            }
            else
            {
                writer.WriteInt32(Value);
            }
        }

        ///<inheritdoc/>
        public override ObjectProperty DeepClone() => (ObjectProperty)MemberwiseClone();
        ///<inheritdoc/>
        public override bool Equivalent(Property other) => other is ObjectProperty objectProperty && base.Equivalent(objectProperty) && objectProperty.Value == Value;

        int IComparable.CompareTo(object obj)
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

        ///<inheritdoc/>
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

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        /// <summary>
        /// Creates a new object property from the given import, with no name. Only use in object arrays!
        /// </summary>
        /// <param name="entry"></param>
        public static implicit operator ObjectProperty(ImportEntry entry)
        {
            return new ObjectProperty(entry.UIndex);
        }

        /// <summary>
        /// Creates a new object property from the given export, with no name. Only use in object arrays!
        /// </summary>
        /// <param name="entry"></param>
        public static implicit operator ObjectProperty(ExportEntry entry)
        {
            return new ObjectProperty(entry.UIndex);
        }

#pragma warning disable
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore
    }

    /// <summary>
    /// Property containg a <see cref="NameReference"/>
    /// </summary>
    [DebuggerDisplay("NameProperty | {Name.Instanced} = {Value}")]
    public sealed class NameProperty : Property, INotifyPropertyChanged
    {
        ///<inheritdoc/>
        public override PropertyType PropType => PropertyType.NameProperty;

        /// <summary>
        /// The <see cref="NameReference"/> this property contains
        /// </summary>
        public NameReference Value { get; set; }

        /// <summary>
        /// Creates a <see cref="NameProperty"/>
        /// </summary>
        /// <param name="value">The <see cref="NameReference"/> this property will contain</param>
        /// <param name="propertyName">Optional: The name of the property (not the value!). This should only be null when it's in an <see cref="ArrayProperty{T}"/></param>
        public NameProperty(NameReference value, NameReference? propertyName = null) : base(propertyName)
        {
            Value = value;
        }

        internal NameProperty(EndianReader stream, IMEPackage pcc, NameReference? propertyName = null) : base(propertyName)
        {
            ValueOffset = (int)stream.Position;
            Value = new NameReference(pcc.GetNameEntry(stream.ReadInt32()), stream.ReadInt32());
        }

        ///<inheritdoc/>
        public override void WriteTo(EndianWriter writer, IMEPackage pcc, bool valueOnly = false)
        {
            if (!valueOnly)
            {
                writer.WriteNameProperty(pcc, Name, Value, StaticArrayIndex);
            }
            else
            {
                writer.WriteInt32(pcc.FindNameOrAdd(Value.Name));
                writer.WriteInt32(Value.Number);
            }
        }

        ///<inheritdoc/>
        public override NameProperty DeepClone() => (NameProperty)MemberwiseClone();
        ///<inheritdoc/>
        public override bool Equivalent(Property other) => other is NameProperty nameProperty && base.Equivalent(nameProperty) && nameProperty.Value == Value;

        ///<inheritdoc/>
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
            return Value.Instanced;
        }
#pragma warning disable
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore
    }

    /// <summary>
    /// Property containing a boolean
    /// </summary>
    [DebuggerDisplay("BoolProperty | {Name.Instanced} = {Value}")]
    public sealed class BoolProperty : Property, INotifyPropertyChanged
    {
        ///<inheritdoc/>
        public override PropertyType PropType => PropertyType.BoolProperty;

        /// <summary>
        /// The boolean this property contains
        /// </summary>
        public bool Value { get; set; }

        /// <summary>
        /// Creates a <see cref="BoolProperty"/>
        /// </summary>
        /// <param name="value"></param>
        /// <param name="name">Optional: The property name. This should only be null when it's in an <see cref="ArrayProperty{T}"/></param>
        public BoolProperty(bool value, NameReference? name = null) : base(name)
        {
            Value = value;
        }

        internal BoolProperty(EndianReader stream, IMEPackage pcc, NameReference? name = null, bool isArrayContained = false) : base(name)
        {
            ValueOffset = (int)stream.Position;
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

        ///<inheritdoc/>
        public override void WriteTo(EndianWriter writer, IMEPackage pcc, bool valueOnly = false)
        {
            if (!valueOnly)
            {
                writer.WriteBoolProperty(pcc, Name, Value, StaticArrayIndex);
            }
            else
            {
                writer.WriteBoolByte(Value);

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

        ///<inheritdoc/>
        public override BoolProperty DeepClone() => (BoolProperty)MemberwiseClone();
        ///<inheritdoc/>
        public override bool Equivalent(Property other) => other is BoolProperty boolProperty && base.Equivalent(boolProperty) && boolProperty.Value == Value;

        public static implicit operator BoolProperty(bool n)
        {
            return new BoolProperty(n);
        }

        public static implicit operator bool(BoolProperty p)
        {
            return p?.Value == true;
        }
#pragma warning disable
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore
    }

    /// <summary>
    /// Property containing a byte
    /// </summary>
    /// <remarks>Unlike the UE3 UByteProperty class, which represents both bytes and enum values, this is just for bytes. Enum values are split out into the <see cref="EnumProperty"/> class</remarks>
    [DebuggerDisplay("ByteProperty | {Name.Instanced} = {Value}")]
    public sealed class ByteProperty : Property, INotifyPropertyChanged
    {
        ///<inheritdoc/>
        public override PropertyType PropType => PropertyType.ByteProperty;

        /// <summary>
        /// The byte this property contains
        /// </summary>
        public byte Value { get; set; }

        /// <summary>
        /// Creates a <see cref="ByteProperty"/>
        /// </summary>
        /// <param name="value"></param>
        /// <param name="name">Optional: The property name. This should only be null when it's in an <see cref="ArrayProperty{T}"/></param>
        public ByteProperty(byte value, NameReference? name = null) : base(name)
        {
            Value = value;
        }

        internal ByteProperty(EndianReader stream, NameReference? name = null) : base(name)
        {
            ValueOffset = (int)stream.Position;
            Value = stream.ReadByte();
        }

        ///<inheritdoc/>
        public override void WriteTo(EndianWriter writer, IMEPackage pcc, bool valueOnly = false)
        {
            if (!valueOnly)
            {
                writer.WriteByteProperty(pcc, Name, Value, StaticArrayIndex);
            }
            else
            {
                writer.WriteByte(Value);
            }
        }

        ///<inheritdoc/>
        public override ByteProperty DeepClone() => (ByteProperty)MemberwiseClone();
        ///<inheritdoc/>
        public override bool Equivalent(Property other) => other is ByteProperty byteProperty && base.Equivalent(byteProperty) && byteProperty.Value == Value;
#pragma warning disable
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore
    }

    /// <summary>
    /// Property containing a BioMask4 (a byte)
    /// </summary>
    /// <remarks>This property only exists in ME3/LE and is used in a single struct.</remarks>
    public sealed class BioMask4Property : Property, INotifyPropertyChanged
    {
        ///<inheritdoc/>
        public override PropertyType PropType => PropertyType.BioMask4Property;

        /// <summary>
        /// The byte that this property contains
        /// </summary>
        public byte Value { get; set; }

        /// <summary>
        /// Creates a <see cref="ByteProperty"/>
        /// </summary>
        /// <param name="value"></param>
        /// <param name="name">Optional: The property name. This should only be null when it's in an <see cref="ArrayProperty{T}"/></param>
        public BioMask4Property(byte value, NameReference? name = null) : base(name)
        {
            Value = value;
        }

        internal BioMask4Property(EndianReader stream, NameReference? name = null) : base(name)
        {
            ValueOffset = (int)stream.Position;
            Value = stream.ReadByte();
        }

        ///<inheritdoc/>
        public override void WriteTo(EndianWriter writer, IMEPackage pcc, bool valueOnly = false)
        {
            if (!valueOnly)
            {
                writer.WritePropHeader(pcc, Name, PropType, 1, StaticArrayIndex);
            }
            writer.WriteByte(Value);
        }

        ///<inheritdoc/>
        public override BioMask4Property DeepClone() => (BioMask4Property)MemberwiseClone();
        ///<inheritdoc/>
        public override bool Equivalent(Property other) => other is BioMask4Property bioMask4Property && base.Equivalent(bioMask4Property) && bioMask4Property.Value == Value;
#pragma warning disable
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore
    }

    /// <summary>
    /// Property containing an enum value
    /// </summary>
    [DebuggerDisplay("EnumProperty | {Name.Instanced} = {Value.Instanced}")]
    public sealed class EnumProperty : Property, INotifyPropertyChanged
    {
        ///<inheritdoc/>
        public override PropertyType PropType => PropertyType.ByteProperty;

        /// <summary>
        /// The enum name
        /// </summary>
        public NameReference EnumType { get; }

        /// <summary>
        /// The enum value
        /// </summary>
        public NameReference Value { get; set; }

        /// <summary>
        /// Creates an <see cref="EnumProperty"/>
        /// </summary>
        /// <param name="value">The enum value</param>
        /// <param name="enumType">Name of enum</param>
        /// <param name="meGame">Which game this property is for</param>
        /// <param name="name">Optional: The property name. This should only be null when it's in an <see cref="ArrayProperty{T}"/></param>
        public EnumProperty(NameReference value, NameReference enumType, MEGame meGame, NameReference? name = null) : base(name)
        {
            EnumType = enumType;
            Value = value;
        }

        /// <summary>
        /// Creates an enum property and sets the value to "None"
        /// </summary>
        /// <param name="enumType">Name of enum</param>
        /// <param name="meGame">Which game this property is for</param>
        /// <param name="name">Optional: The property name. This should only be null when it's in an <see cref="ArrayProperty{T}"/></param>
        /// <exception>Throws if <paramref name="enumType"/> does not exist in <paramref name="meGame"/></exception>
        public EnumProperty(NameReference enumType, MEGame meGame, NameReference? name = null) : base(name)
        {
            EnumType = enumType;
            if (!GlobalUnrealObjectInfo.IsValidEnum(meGame, enumType))
            {
                throw new Exception($"{enumType} is not a valid enum type in {meGame}!");
            }
            Value = NameReference.None;
        }

        internal EnumProperty(EndianReader stream, IMEPackage pcc, NameReference enumType, NameReference? name = null) : base(name)
        {
            ValueOffset = (int)stream.Position;
            EnumType = enumType;

            if (pcc.Game == MEGame.ME1 && pcc.Platform == MEPackage.GamePlatform.Xenon)
            {
                // ME1 Xenon uses 1 byte values
                var enumIdx = stream.ReadByte();
                Value = GlobalUnrealObjectInfo.GetEnumValues(pcc.Game, enumType)[enumIdx];
            }
            else
            {
                var eNameIdx = stream.ReadInt32();
                var eName = pcc.GetNameEntry(eNameIdx);
#if AZURE || DEBUG
                if (eName == "")
                {
                    throw new Exception($"Enum being initialized with invalid name reference idx: {eNameIdx}");
                }
#endif
                var eNameNumber = stream.ReadInt32();
                Value = new NameReference(eName, eNameNumber);
            }
        }

        ///<inheritdoc/>
        public override void WriteTo(EndianWriter writer, IMEPackage pcc, bool valueOnly = false)
        {
            if (!valueOnly)
            {
                writer.WriteEnumProperty(pcc, Name, EnumType, Value, StaticArrayIndex);
            }
            else
            {
                writer.WriteInt32(pcc.FindNameOrAdd(Value.Name));
                writer.WriteInt32(Value.Number);
            }
        }

        ///<inheritdoc/>
        public override EnumProperty DeepClone() => (EnumProperty)MemberwiseClone();
        ///<inheritdoc/>
        public override bool Equivalent(Property other) => other is EnumProperty enumProperty && base.Equivalent(enumProperty) && enumProperty.EnumType == EnumType && enumProperty.Value == Value;
#pragma warning disable
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore
    }

    /// <summary>
    /// Base class for <see cref="ArrayProperty{T}"/> and <see cref="ImmutableByteArrayProperty"/>
    /// </summary>
    public abstract class ArrayPropertyBase : Property, IEnumerable
    {
        /// <summary>
        /// Name of the UnrealScript type this array contains
        /// </summary>
        public string Reference;

        ///<inheritdoc/>
        public override PropertyType PropType => PropertyType.ArrayProperty;

        /// <summary>
        /// Read-only view into the Values of the array 
        /// </summary>
        public abstract IReadOnlyList<Property> Properties { get; }

        /// <summary>
        /// Number of elements in the array
        /// </summary>
        public abstract int Count { get; }

        protected internal ArrayPropertyBase(NameReference name) : base(name)
        {
        }

        IEnumerator IEnumerable.GetEnumerator() => Properties.GetEnumerator();

        /// <summary>
        /// Removes all elements
        /// </summary>
        public abstract void Clear();

        /// <summary>
        /// Removes the <see cref="Property"/> at the specified <paramref name="index"/>
        /// </summary>
        /// <param name="index">The zero based index of the <see cref="Property"/> to remove.</param>
        public abstract void RemoveAt(int index);

        /// <summary>
        /// Gets the <see cref="Property"/> at the specified index
        /// </summary>
        /// <param name="index">The zero based index of the <see cref="Property"/> to get.</param>
        public Property this[int index] => Properties[index];

        /// <summary>
        /// Swaps the elements at indexes <paramref name="i"/> and <paramref name="j"/>
        /// </summary>
        /// <param name="i"></param>
        /// <param name="j"></param>
        public abstract void SwapElements(int i, int j);
    }

    /// <summary>
    /// Replacement for <see cref="ArrayProperty{T}"/> where T is <see cref="ByteProperty"/>.
    /// Arrays of bytes are almost always embedded data (such as swfs) that are thousands or millions of bytes long.
    /// Allocating a <see cref="ByteProperty"/> for each byte is grossly inefficient, so this just keeps it internally as a byte[]
    /// </summary>
    [DebuggerDisplay("ImmutableByteArrayProperty | {Name.Instanced}, Length = {Bytes.Length}")]
    public sealed class ImmutableByteArrayProperty : ArrayPropertyBase
    {
        /// <summary>
        /// The bytes in the arrays
        /// </summary>
        public byte[] Bytes;

        /// <summary>
        /// Creates an <see cref="ImmutableByteArrayProperty"/> from a byte array
        /// </summary>
        /// <param name="array"></param>
        /// <param name="name">The property name.</param>
        public ImmutableByteArrayProperty(byte[] array, NameReference name) : base(name)
        {
            Bytes = array;
        }

        /// <summary>
        /// Creates an empty <see cref="ImmutableByteArrayProperty"/>
        /// </summary>
        /// <param name="name">The property name.</param>
        public ImmutableByteArrayProperty(NameReference name) : base(name)
        {
            Bytes = [];
        }

        internal ImmutableByteArrayProperty(long startOffset, int count, EndianReader stream, NameReference name) : base(name)
        {
            ValueOffset = (int)startOffset;
            Bytes = stream.ReadBytes(count);
        }

        ///<inheritdoc/>
        public override void WriteTo(EndianWriter writer, IMEPackage pcc, bool valueOnly = false)
        {
            if (!valueOnly)
            {
                writer.WriteArrayProperty(pcc, Name, Bytes.Length, () =>
                {
                    Stream m = MemoryManager.GetMemoryStream();
                    m.WriteFromBuffer(Bytes);
                    return m;
                }, StaticArrayIndex);
            }
            else
            {
                writer.WriteInt32(Bytes.Length);
                writer.WriteFromBuffer(Bytes);
            }
        }

        ///<inheritdoc/>
        public override ImmutableByteArrayProperty DeepClone()
        {
            var clone = (ImmutableByteArrayProperty)MemberwiseClone();
            clone.Bytes = Bytes.ArrayClone();
            return clone;
        }

        ///<inheritdoc/>
        public override bool Equivalent(Property other) => other is ImmutableByteArrayProperty arrayProperty && base.Equivalent(arrayProperty) && Bytes.AsSpan().SequenceEqual(arrayProperty.Bytes);

        /// <summary>
        /// Do not use with <see cref="ImmutableByteArrayProperty"/>! Returns an empty list
        /// </summary>
        public override IReadOnlyList<Property> Properties => [];

        /// <summary>
        /// Number of bytes
        /// </summary>
        public override int Count => Bytes.Length;
        /// <summary>
        /// Sets <see cref="Bytes"/> to an empty array
        /// </summary>
        public override void Clear()
        {
            Bytes = [];
        }
        /// <summary>
        /// Do not use with <see cref="ImmutableByteArrayProperty"/>! No-op
        /// </summary>
        /// <param name="index"></param>
        public override void RemoveAt(int index)
        {
        }
        /// <summary>
        /// Do not use with <see cref="ImmutableByteArrayProperty"/>! No-op
        /// </summary>
        /// <param name="i"></param>
        /// <param name="j"></param>
        public override void SwapElements(int i, int j)
        {
        }
    }

    /// <summary>
    /// Property containing a dynamic array of <typeparamref name="T"/>s
    /// </summary>
    /// <typeparam name="T">The <see cref="Property"/> subclass this is an array of</typeparam>
    [DebuggerDisplay("ArrayProperty<{typeof(T).Name,nq}> | {Name.Instanced}, Length = {Values.Count}")]
    public sealed class ArrayProperty<T> : ArrayPropertyBase, IList<T> where T : Property
    {
        /// <summary>
        /// The <typeparamref name="T"/>s in this array
        /// </summary>
        public List<T> Values { get; set; }
        ///<inheritdoc/>
        public override IReadOnlyList<T> Properties => Values;

        /// <summary>
        /// Creates an empty <see cref="ArrayProperty{T}"/>
        /// </summary>
        /// <param name="name">The property name.</param>
        public ArrayProperty(NameReference name) : this([], name)
        {
        }

        /// <summary>
        /// Creates an <see cref="ArrayProperty{T}"/>
        /// </summary>
        /// <param name="values"></param>
        /// <param name="name">The property name.</param>
        public ArrayProperty(IEnumerable<T> values, NameReference name) : this(values.ToList(), name)
        {
        }

        /// <summary>
        /// Creates an <see cref="ArrayProperty<typeparamref name="T"/>"/> from a <see cref="List<typeparamref name="T"/>"/>
        /// </summary>
        /// <param name="values"></param>
        /// <param name="name">The property name.</param>
        public ArrayProperty(List<T> values, NameReference name) : base(name)
        {
            Values = values;
        }

        // Deserialization constructor
        internal ArrayProperty(long startOffset, List<T> values, NameReference name) : this(values, name)
        {
            ValueOffset = (int)startOffset;
        }

        ///<inheritdoc/>
        public override void WriteTo(EndianWriter writer, IMEPackage pcc, bool valueOnly = false)
        {
            if (!valueOnly)
            {
                writer.WriteArrayProperty(pcc, Name, Values.Count, () =>
                {
                    var m = new EndianReader(MemoryManager.GetMemoryStream()) { Endian = pcc.Endian };
                    foreach (var prop in Values)
                    {
                        prop.WriteTo(m.Writer, pcc, true);
                    }
                    return m.BaseStream;
                }, StaticArrayIndex);
            }
            else
            {
                writer.WriteInt32(Values.Count);
                foreach (var prop in Values)
                {
                    prop.WriteTo(writer, pcc, true);
                }
            }
        }

        ///<inheritdoc/>
        public override ArrayProperty<T> DeepClone()
        {
            var clone = (ArrayProperty<T>)MemberwiseClone();
            clone.Values = new List<T>(Values.Count);
            for (int i = 0; i < Values.Count; i++)
            {
                clone.Values.Add((T)Values[i].DeepClone());
            }

            return clone;
        }

        ///<inheritdoc/>
        public override bool Equivalent(Property other)
        {
            if (other is ArrayProperty<T> arrayProperty && base.Equivalent(arrayProperty) && arrayProperty.Count == Count)
            {
                for (int i = 0; i < Count; i++)
                {
                    if (!Values[i].Equivalent(arrayProperty[i]))
                    {
                        return false;
                    }
                }
                return true;
            }
            return false;
        }

        #region IEnumerable<T>
        public IEnumerator<T> GetEnumerator()
        {
            return Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return Values.GetEnumerator();
        }
        #endregion

        #region IList<T>

        /// <inheritdoc cref="ArrayPropertyBase.Count" />
        public override int Count => Values.Count;
        bool ICollection<T>.IsReadOnly => false;

        ///<inheritdoc/>
        public new T this[int index]
        {
            get => Values[index];
            set => Values[index] = value;
        }

        ///<inheritdoc/>
        public void Add(T item)
        {
            Values.Add(item);
        }

        /// <inheritdoc cref="ArrayPropertyBase.Clear" />
        public override void Clear()
        {
            Values.Clear();
        }

        ///<inheritdoc/>
        public bool Contains(T item)
        {
            return Values.Contains(item);
        }

        ///<inheritdoc/>
        public void CopyTo(T[] array, int arrayIndex)
        {
            Values.CopyTo(array, arrayIndex);
        }

        ///<inheritdoc/>
        public bool Remove(T item)
        {
            return Values.Remove(item);
        }

        /// <inheritdoc cref="ArrayPropertyBase.RemoveAt" />
        public override void RemoveAt(int index)
        {
            Values.RemoveAt(index);
        }

        ///<inheritdoc/>
        public int IndexOf(T item)
        {
            return Values.IndexOf(item);
        }

        ///<inheritdoc/>
        public void Insert(int index, T item)
        {
            Values.Insert(index, item);
        }

        /// <inheritdoc cref="List{T}.InsertRange" />
        public void InsertRange(int index, IEnumerable<T> collection)
        {
            Values.InsertRange(index, collection);
        }
        #endregion

        ///<inheritdoc/>
        public override void SwapElements(int i, int j)
        {
            if (i == j || i < 0 || i >= Count || j < 0 || j >= Count)
            {
                return;
            }

            (this[i], this[j]) = (this[j], this[i]);
        }
    }

    /// <summary>
    /// Property containing a string
    /// </summary>
    [DebuggerDisplay("StrProperty | {Name.Instanced} = {Value}")]
    public sealed class StrProperty : Property
    {
        ///<inheritdoc/>
        public override PropertyType PropType => PropertyType.StrProperty;

        /// <summary>
        /// The string this property contains
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// Creates a <see cref="StrProperty"/>
        /// </summary>
        /// <param name="value"></param>
        /// <param name="name">Optional: The property name. This should only be null when it's in an <see cref="ArrayProperty{T}"/></param>
        public StrProperty(string value, NameReference? name = null) : base(name)
        {
            Value = value ?? string.Empty;
        }

        internal StrProperty(EndianReader stream, NameReference? name = null) : base(name)
        {
            ValueOffset = (int)stream.Position;
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
                Value = stream.BaseStream.ReadStringLatin1Null(count);
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

        ///<inheritdoc/>
        public override void WriteTo(EndianWriter writer, IMEPackage pcc, bool valueOnly = false)
        {
            if (!valueOnly)
            {
                writer.WriteStringProperty(pcc, Name, Value, StaticArrayIndex);
            }
            else
            {
                writer.WriteUnrealString(Value, pcc.Game);
            }
        }

        ///<inheritdoc/>
        public override StrProperty DeepClone() => (StrProperty)MemberwiseClone();
        ///<inheritdoc/>
        public override bool Equivalent(Property other) => other is StrProperty strProperty && base.Equivalent(strProperty) && strProperty.Value == Value;

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

    /// <summary>
    /// Property containing a string ref (an integer referring to a string in a tlk)
    /// </summary>
    [DebuggerDisplay("StringRefProperty | {Name.Instanced} = ${Value}")]
    public sealed class StringRefProperty : Property, INotifyPropertyChanged
    {
        ///<inheritdoc/>
        public override PropertyType PropType => PropertyType.StringRefProperty;

        /// <summary>
        /// The string ref
        /// </summary>
        public int Value { get; set; }

        /// <summary>
        /// Creates a <see cref="StringRefProperty"/>
        /// </summary>
        /// <param name="val"></param>
        /// <param name="name">Optional: The property name. This should only be null when it's in an <see cref="ArrayProperty{T}"/></param>
        public StringRefProperty(int val, NameReference? name = null) : base(name)
        {
            Value = val;
        }

        internal StringRefProperty(EndianReader stream, NameReference? name = null) : base(name)
        {
            ValueOffset = (int)stream.Position;
            Value = stream.ReadInt32();
        }

        /// <summary>
        /// For constructing new property
        /// </summary>
        /// <param name="name"></param>
        public StringRefProperty(NameReference? name = null) : base(name) { }

        ///<inheritdoc/>
        public override void WriteTo(EndianWriter writer, IMEPackage pcc, bool valueOnly = false)
        {
            if (!valueOnly)
            {
                writer.WriteStringRefProperty(pcc, Name, Value, StaticArrayIndex);
            }
            else
            {
                writer.WriteInt32(Value);
            }
        }

        ///<inheritdoc/>
        public override StringRefProperty DeepClone() => (StringRefProperty)MemberwiseClone();
        ///<inheritdoc/>
        public override bool Equivalent(Property other) => other is StringRefProperty stringRefProperty && base.Equivalent(stringRefProperty) && stringRefProperty.Value == Value;

#pragma warning disable
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore
    }

    /// <summary>
    /// Property containing a <see cref="ScriptDelegate"/>
    /// </summary>
    public sealed class DelegateProperty : Property, INotifyPropertyChanged
    {
        ///<inheritdoc/>
        public override PropertyType PropType => PropertyType.DelegateProperty;

        /// <summary>
        /// The <see cref="ScriptDelegate"/> this property contains
        /// </summary>
        public ScriptDelegate Value { get; set; }

        /// <summary>
        /// Creates a <see cref="DelegateProperty"/>
        /// </summary>
        /// <param name="functionName">The name of the delegate function</param>
        /// <param name="containingObjectUIndex">Optional: The UIndex of the object containing the function. Often not specified</param>
        /// <param name="name">Optional: The property name. This should only be null when it's in an <see cref="ArrayProperty{T}"/></param>
        public DelegateProperty(NameReference functionName, int containingObjectUIndex = 0, NameReference? name = null) : this(new ScriptDelegate(containingObjectUIndex, functionName), name)
        {
        }

        /// <summary>
        /// Creates a <see cref="DelegateProperty"/>
        /// </summary>
        /// <param name="value"></param>
        /// <param name="name">Optional: The property name. This should only be null when it's in an <see cref="ArrayProperty{T}"/></param>
        public DelegateProperty(ScriptDelegate value, NameReference? name = null) : base(name)
        {
            Value = value;
        }

        internal DelegateProperty(EndianReader stream, IMEPackage pcc, NameReference? name = null) : base(name)
        {
            ValueOffset = (int)stream.Position;
            Value = new ScriptDelegate(stream.ReadInt32(), new NameReference(pcc.GetNameEntry(stream.ReadInt32()), stream.ReadInt32()));
        }

        ///<inheritdoc/>
        public override void WriteTo(EndianWriter writer, IMEPackage pcc, bool valueOnly = false)
        {
            if (!valueOnly)
            {
                writer.WriteDelegateProperty(pcc, Name, Value, StaticArrayIndex);
            }
            else
            {
                writer.WriteInt32(Value.ContainingObjectUIndex);
                writer.WriteNameReference(Value.FunctionName, pcc);
            }
        }

        ///<inheritdoc/>
        public override DelegateProperty DeepClone() => (DelegateProperty)MemberwiseClone();
        ///<inheritdoc/>
        public override bool Equivalent(Property other)
        {
            return other is DelegateProperty delegateProperty && base.Equivalent(delegateProperty) && delegateProperty.Value == Value;
        }
#pragma warning disable
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore
    }

    /// <summary>
    /// Only created through deserialization. Indicates either an error in the deserializer, or malformed data.
    /// </summary>
    public sealed class UnknownProperty : Property
    {
        ///<inheritdoc/>
        public override PropertyType PropType => PropertyType.Unknown;

        private byte[] raw;
        private readonly string TypeName;

        internal UnknownProperty(EndianReader stream, int size, string typeName = null, NameReference? name = null) : base(name)
        {
            ValueOffset = (int)stream.Position;
            TypeName = typeName ?? "Unknown";
            raw = stream.ReadBytes(size);
#if AZURE
            Assert.Fail("Encountered an unknownproperty!");
#endif
            LECLog.Warning($"Initializing an UnknownProperty object! Position: 0x{stream.Position - size:X8}");
        }

        ///<inheritdoc/>
        public override void WriteTo(EndianWriter writer, IMEPackage pcc, bool valueOnly = false)
        {
            if (!valueOnly)
            {
                writer.WriteNameReference(Name, pcc);
                writer.WriteNameReference(TypeName, pcc);
                writer.WriteInt32(raw.Length);
                writer.WriteInt32(StaticArrayIndex);
            }
            writer.WriteFromBuffer(raw);
        }

        ///<inheritdoc/>
        public override UnknownProperty DeepClone()
        {
            var clone = (UnknownProperty)MemberwiseClone();
            clone.raw = raw.ArrayClone();
            return clone;
        }

        ///<inheritdoc/>
        public override bool Equivalent(Property other) => false;
    }
}
