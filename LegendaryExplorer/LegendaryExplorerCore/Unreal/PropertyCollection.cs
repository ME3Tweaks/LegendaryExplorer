using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

#if AZURE
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace LegendaryExplorerCore.Unreal
{
    public sealed class PropertyCollection : Collection<Property>
    {

        public int endOffset;
        public bool IsImmutable;

        /// <summary>
        /// Gets the UProperty with the specified name and optionally a static array index, returns null if not found. The property name is checked case insensitively. 
        /// Ensure the generic type matches the result you want or you will receive a null object back.
        /// </summary>
        /// <param name="name">Name of property to find</param>
        /// <param name="staticArrayIdx"></param>
        /// <returns>specified UProperty or null if not found</returns>
        public T GetProp<T>(NameReference name, int staticArrayIdx = 0) where T : Property
        {
            foreach (var prop in this)
            {
                if (prop.Name == name && prop.StaticArrayIndex == staticArrayIdx)
                {
                    return prop as T;
                }
            }
            return null;
        }

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

        public void AddOrReplaceProp(Property prop)
        {
            if (!TryReplaceProp(prop))
            {
                this.Items.Add(prop);
            }
        }

        public void WriteTo(EndianWriter stream, IMEPackage pcc, bool requireNoneAtEnd = true)
        {
            foreach (var prop in this)
            {
                prop.WriteTo(stream, pcc, IsImmutable);
            }
            if (!IsImmutable && requireNoneAtEnd && (Count == 0 || this.Last() is not NoneProperty))
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

        //public static PropertyCollection ReadProps(ExportEntry export, Stream rawStream, string typeName, bool includeNoneProperty = false, bool requireNoneAtEnd = true, IEntry entry = null)
        public static PropertyCollection ReadProps(ExportEntry export, Stream rawStream, string typeName, bool includeNoneProperty = false, bool requireNoneAtEnd = true, IEntry entry = null, PackageCache packageCache = null)
        {
            var stream = new EndianReader(rawStream) { Endian = export.FileRef.Endian };
            var props = new PropertyCollection();
            long startPosition = stream.Position;
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
                        props.Items.Add(new NoneProperty(stream) { StartOffset = propertyStartPosition, ValueOffset = propertyStartPosition });
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
                    PropertyType type;
                    string namev = pcc.GetNameEntry(typeIdx);
                    //Debug.WriteLine("Reading " + nameRef.Instanced + " (" + namev + ") at 0x" + (stream.Position - 24).ToString("X8"));
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
                            int valOffset = (int)stream.Position;
                            if (GlobalUnrealObjectInfo.IsImmutable(structType, pcc.Game))
                            {
                                PropertyCollection structProps = ReadImmutableStruct(export, stream, structType, size, packageCache, entry);
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
                                            classInfo = GlobalUnrealObjectInfo.generateClassInfo(exp);
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
                        props.Items.Add(prop);
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
                if (props[^1].PropType != PropertyType.None && requireNoneAtEnd)
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
                if (props[^1].PropType == PropertyType.None && !includeNoneProperty)
                {
                    props.Items.RemoveAt(props.Count - 1);
                }
            }
            props.endOffset = (int)stream.Position;
            return props;
        }

        public static PropertyCollection ReadImmutableStruct(ExportEntry export, EndianReader stream, string structType, int size, PackageCache packageCache, IEntry parsingEntry = null)
        {
            IMEPackage pcc = export.FileRef;
            //strip transients unless this is a class definition
            bool stripTransients = parsingEntry is not {ClassName: "Class" or "ScriptStruct"};

            MEGame structValueLookupGame = pcc.Game;
            var props = new PropertyCollection();
            switch (pcc.Game)
            {
                case MEGame.ME1 when parsingEntry != null && parsingEntry.FileRef.Platform == MEPackage.GamePlatform.PS3 && ME3UnrealObjectInfo.Structs.ContainsKey(structType):
                case MEGame.ME2 when parsingEntry != null && parsingEntry.FileRef.Platform == MEPackage.GamePlatform.PS3 && ME3UnrealObjectInfo.Structs.ContainsKey(structType):
                    structValueLookupGame = MEGame.ME3;
                    break;
                case MEGame.ME3 when ME3UnrealObjectInfo.Structs.ContainsKey(structType):
                case MEGame.UDK when ME3UnrealObjectInfo.Structs.ContainsKey(structType):
                case MEGame.ME2 when ME2UnrealObjectInfo.Structs.ContainsKey(structType):
                case MEGame.ME1 when ME1UnrealObjectInfo.Structs.ContainsKey(structType):
                case MEGame.LE3 when ME3UnrealObjectInfo.Structs.ContainsKey(structType):
                case MEGame.LE2 when ME3UnrealObjectInfo.Structs.ContainsKey(structType):
                case MEGame.LE1 when ME3UnrealObjectInfo.Structs.ContainsKey(structType):
                    break;
                default:
                    Debug.WriteLine("Unknown struct type: " + structType);
                    props.Add(new UnknownProperty(stream, size) { StartOffset = (int)stream.Position });
                    return props;
            }

            PropertyCollection defaultProps = GlobalUnrealObjectInfo.getDefaultStructValue(structValueLookupGame, structType, stripTransients, packageCache, false);
            if (defaultProps == null)
            {
                int startPos = (int)stream.Position;
                props.Items.Add(new UnknownProperty(stream, size) { StartOffset = startPos });
                return props;
            }

            foreach (var prop in defaultProps)
            {
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
                    props.Items.Add(property);
                }
            }
            return props;
        }

        //Nested struct type is for structs in structs
        static Property ReadImmutableStructProp(ExportEntry export, EndianReader stream, Property template, string structType, string nestedStructType = null, PackageCache packageCache = null)
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
                    //always say it's ME3 so that bools get read as 1 byte
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
                    PropertyCollection structProps = ReadImmutableStruct(export, stream, GlobalUnrealObjectInfo.GetPropertyInfo(pcc.Game, template.Name, structType, containingExport: export).Reference, 0, packageCache, export);
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

        public static Property ReadArrayProperty(EndianReader stream, ExportEntry export, string enclosingType, NameReference name, bool IsInImmutable = false, bool IncludeNoneProperties = false, IEntry parsingEntry = null, PackageCache packageCache = null)
        {
            IMEPackage pcc = export.FileRef;
            long arrayOffset = IsInImmutable ? stream.Position : stream.Position - 24;
            ArrayType arrayType = GlobalUnrealObjectInfo.GetArrayType(pcc.Game, name, enclosingType == "ScriptStruct" ? export.ObjectName : enclosingType , parsingEntry);
            //Debug.WriteLine("Reading array length at 0x" + stream.Position.ToString("X5"));
            int count = stream.ReadInt32();
            switch (arrayType)
            {
                case ArrayType.Object:
                    {
                        var props = new List<ObjectProperty>();
                        for (int i = 0; i < count; i++)
                        {
                            int startPos = (int)stream.Position;
                            props.Add(new ObjectProperty(stream) { StartOffset = startPos });
                        }
                        return new ArrayProperty<ObjectProperty>(arrayOffset, props, name) { Reference = "Object" }; //TODO: set reference to specific object type?
                    }
                case ArrayType.Name:
                    {
                        var props = new List<NameProperty>();
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
                            classInfo = GlobalUnrealObjectInfo.generateClassInfo(parsingExport);
                        }

                        //Use DB info or attempt lookup
                        var enumType = new NameReference(enumname ?? GlobalUnrealObjectInfo.GetEnumType(pcc.Game, name, enclosingType, classInfo));

                        var props = new List<EnumProperty>();
                        for (int i = 0; i < count; i++)
                        {
                            int startPos = (int)stream.Position;
                            props.Add(new EnumProperty(stream, pcc, enumType) { StartOffset = startPos });
                        }
                        return new ArrayProperty<EnumProperty>(arrayOffset, props, name) { Reference = enumType };
                    }
                case ArrayType.Struct:
                    {
                        int startPos = (int)stream.Position;
                        var props = new List<StructProperty>();
                        var propertyInfo = GlobalUnrealObjectInfo.GetPropertyInfo(pcc.Game, name, enclosingType, containingExport: parsingEntry as ExportEntry);
                        if (propertyInfo == null && parsingEntry is ExportEntry parsingExport)
                        {
                            var currentInfo = GlobalUnrealObjectInfo.generateClassInfo(parsingExport);
                            propertyInfo = GlobalUnrealObjectInfo.GetPropertyInfo(pcc.Game, name, enclosingType, currentInfo, parsingExport);
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
                            for (int i = 0; i < count; i++)
                            {
                                int offset = (int)stream.Position;
                                try
                                {
                                    PropertyCollection structProps = ReadImmutableStruct(export, stream, arrayStructType, arraySize / count, parsingEntry: parsingEntry, packageCache: packageCache);
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
                        var props = new List<BoolProperty>();
                        for (int i = 0; i < count; i++)
                        {
                            int startPos = (int)stream.Position;
                            props.Add(new BoolProperty(stream, pcc, isArrayContained: true) { StartOffset = startPos });
                        }
                        return new ArrayProperty<BoolProperty>(arrayOffset, props, name) { Reference = "Bool" };
                    }
                case ArrayType.String:
                    {
                        var props = new List<StrProperty>();
                        for (int i = 0; i < count; i++)
                        {
                            int startPos = (int)stream.Position;
                            props.Add(new StrProperty(stream) { StartOffset = startPos });
                        }
                        return new ArrayProperty<StrProperty>(arrayOffset, props, name) { Reference = "String" };
                    }
                case ArrayType.Float:
                    {
                        var props = new List<FloatProperty>();
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
                        var props = new List<StringRefProperty>();
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
                        var props = new List<IntProperty>();
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

        public PropertyCollection DeepClone()
        {
            var clone = new PropertyCollection {endOffset = endOffset, IsImmutable = IsImmutable};
            for (int i = 0; i < Count; i++)
            {
                clone.Add(this[i].DeepClone());
            }
            return clone;
        }

        /// <summary>
        /// Checks to see if the two collections have the same properties (defined by <see cref="Property.Equivalent"/>). Order does not matter
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
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
        /// Returns a new <see cref="PropertyCollection"/> containing clones of all properties in this collection that are not <see cref="Property.Equivalent"/> to ones in <paramref name="other"/>
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

    public abstract class Property
    {
        public abstract PropertyType PropType { get; }
        public int StaticArrayIndex { get; set; }
        /// <summary>
        /// Offset to the value for this property - note not all properties have actual values.
        /// </summary>
        public int ValueOffset;

        /// <summary>
        /// Offset to the start of this property as it was read by PropertyCollection.ReadProps()
        /// </summary>
        public int StartOffset { get; set; }

        public NameReference Name { get; set; }

        protected Property(NameReference? name)
        {
            Name = name ?? new NameReference();
        }

        protected Property()
        {
            Name = new NameReference();
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
            using var stream = new EndianReader(MemoryManager.GetMemoryStream());
            WriteTo(stream.Writer, pcc, valueOnly);
            return stream.Length;
        }

        public abstract Property DeepClone();

        /// <summary>
        /// Checks if this represents the same property (Name and StaticArrayIndex), and has the same value.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public virtual bool Equivalent(Property other) => other.Name == Name && other.StaticArrayIndex == StaticArrayIndex;
    }

    [DebuggerDisplay("NoneProperty")]
    public sealed class NoneProperty : Property
    {
        public override PropertyType PropType => PropertyType.None;

        public NoneProperty() : base("None") { }

        public NoneProperty(EndianReader stream) : this()
        {
            ValueOffset = (int)stream.Position;
        }

        public override void WriteTo(EndianWriter stream, IMEPackage pcc, bool valueOnly = false)
        {
            if (!valueOnly)
            {
                stream.WriteNoneProperty(pcc);
            }
        }

        public override NoneProperty DeepClone() => (NoneProperty)MemberwiseClone();
        public override bool Equivalent(Property other) => other is NoneProperty;
    }

    [DebuggerDisplay("StructProperty | {Name.Instanced} - {StructType}")]
    public class StructProperty : Property, INotifyPropertyChanged
    {
        public override PropertyType PropType => PropertyType.StructProperty;
        public string StructType { get; }
        public PropertyCollection Properties { get; set; }

        public void OnPropertiesChanged()
        {
            if (Properties != null)
            {
                Properties.IsImmutable = IsImmutable;
            }
        }

        public void OnIsImmutableChanged()
        {
            Properties.IsImmutable = IsImmutable;
        }
        public bool IsImmutable { get; set; }

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

        public T GetProp<T>(string name, int staticArrayIndex = 0) where T : Property
        {
            return Properties.GetProp<T>(name, staticArrayIndex);
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
                    var m = new EndianReader(MemoryManager.GetMemoryStream()) { Endian = pcc.Endian };
                    Properties.WriteTo(m.Writer, pcc);
                    return m.BaseStream;
                }, StaticArrayIndex);
            }
        }

        public override StructProperty DeepClone()
        {
            var clone = (StructProperty)MemberwiseClone();
            clone.Properties = Properties.DeepClone();
            return clone;
        }

        public override bool Equivalent(Property other) => other is StructProperty structProperty && base.Equivalent(structProperty) && structProperty.StructType.CaseInsensitiveEquals(StructType) 
                                                           && structProperty.Properties.Equivalent(Properties);

        /// <summary>
        /// Generates a StructProperty (with the specified name) from the specified Guid
        /// </summary>
        /// <param name="tfcGuid"></param>
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

    [DebuggerDisplay("IntProperty | {Name.Instanced} = {Value}")]
    public sealed class IntProperty : Property, IComparable, INotifyPropertyChanged
    {
        public override PropertyType PropType => PropertyType.IntProperty;

        public int Value { get; set; }

        public IntProperty(EndianReader stream, NameReference? name = null) : base(name)
        {
            ValueOffset = (int)stream.Position;
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

        public override IntProperty DeepClone() => (IntProperty) MemberwiseClone();
        public override bool Equivalent(Property other) => other is IntProperty intProperty && base.Equivalent(intProperty) && intProperty.Value == Value;

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
#pragma warning disable
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore
    }

    [DebuggerDisplay("FloatProperty | {Name.Instanced} = {Value}")]
    public sealed class FloatProperty : Property, IComparable, INotifyPropertyChanged
    {
        public override PropertyType PropType => PropertyType.FloatProperty;
        private readonly int _originalData; //This is used because -0 and 0 have different byte patterns, and to reserialize the same, we must write back the correct one.

        private float _value;
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

        public FloatProperty(EndianReader stream, NameReference? name = null) : base(name)
        {
            ValueOffset = (int)stream.Position;
            _originalData = stream.ReadInt32();
            Value = BitConverter.Int32BitsToSingle(_originalData);
        }

        public FloatProperty(float val, NameReference? name = null) : base(name)
        {
            _originalData = BitConverter.SingleToInt32Bits(val);
            Value = val;
        }

        public FloatProperty() { }

        public override void WriteTo(EndianWriter stream, IMEPackage pcc, bool valueOnly = false)
        {
            // Check for NEGATIVE ZERO. Yes that is a thing.
            // Some values in ME games seem to be -0 (00 00 00 80)
            // CLR makes -0 = 0 so we must check the backing bytes
            // or we will re-serialize this wrong. This check only
            // matters when the value has not changed from the original.

            // ReSharper disable once CompareOfFloatsByEqualityOperator
            bool isNegativeZero = Value == 0 && BitConverter.Int32BitsToSingle(_originalData) == Value && _originalData != 0;
            if (!valueOnly)
            {
                if (isNegativeZero)
                {
                    stream.WritePropHeader(pcc, Name, PropertyType.FloatProperty, 4, StaticArrayIndex);
                    stream.Write(_originalData);
                }
                else
                {
                    stream.WriteFloatProperty(pcc, Name, Value, StaticArrayIndex);
                }
            }
            else
            {
                // Negative zero. We must use exact check
                if (isNegativeZero)
                {
                    stream.Write(_originalData);
                }
                else
                {
                    stream.WriteFloat(Value);
                }
            }
        }

        public override FloatProperty DeepClone() => (FloatProperty) MemberwiseClone();
        public override bool Equivalent(Property other) => other is FloatProperty floatProperty && base.Equivalent(floatProperty) && floatProperty.Value == Value;

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

        public event PropertyChangedEventHandler PropertyChanged;
    }

    [DebuggerDisplay("ObjectProperty | {Name.Instanced} = {Value}")]
    public sealed class ObjectProperty : Property, IComparable, INotifyPropertyChanged
    {
        /// <summary>
        /// Resolves this object property to its corresponding entry from the package parameter
        /// </summary>
        /// <param name="package"></param>
        /// <returns></returns>
        public IEntry ResolveToEntry(IMEPackage package) => package.GetEntry(Value);

        public override PropertyType PropType => PropertyType.ObjectProperty;

        //We use ObjectProperty to represent InterfaceProperty and ComponentProperty too. This stores the "real" type.
        public PropertyType InternalPropType = PropertyType.ObjectProperty;

        public int Value { get; set; }

        public ObjectProperty(EndianReader stream, NameReference? name = null) : base(name)
        {
            ValueOffset = (int)stream.Position;
            Value = stream.ReadInt32();
        }

        public ObjectProperty(int val, NameReference? name = null) : base(name)
        {
            Value = val;
        }

        public ObjectProperty(IEntry referencedEntry, NameReference? name = null) : base(name)
        {
            Value = referencedEntry?.UIndex ?? 0;
        }

        public ObjectProperty() { }

        public override void WriteTo(EndianWriter stream, IMEPackage pcc, bool valueOnly = false)
        {
            if (!valueOnly)
            {
                stream.WriteObjectProperty(pcc, Name, Value, StaticArrayIndex, InternalPropType);
            }
            else
            {
                stream.WriteInt32(Value);
            }
        }

        public override ObjectProperty DeepClone() => (ObjectProperty)MemberwiseClone();
        public override bool Equivalent(Property other) => other is ObjectProperty objectProperty && base.Equivalent(objectProperty) && objectProperty.Value == Value;

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

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

#pragma warning disable
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore
    }

    [DebuggerDisplay("NameProperty | {Name.Instanced} = {Value}")]
    public sealed class NameProperty : Property, INotifyPropertyChanged
    {
        public override PropertyType PropType => PropertyType.NameProperty;

        public NameReference Value { get; set; }

        public NameProperty(NameReference value, NameReference? propertyName = null) : base(propertyName)
        {
            Value = value;
        }

        public NameProperty(EndianReader stream, IMEPackage pcc, NameReference? propertyName = null) : base(propertyName)
        {
            ValueOffset = (int)stream.Position;
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

        public override NameProperty DeepClone() => (NameProperty)MemberwiseClone();
        public override bool Equivalent(Property other) => other is NameProperty nameProperty && base.Equivalent(nameProperty) && nameProperty.Value == Value;

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
#pragma warning disable
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore
    }

    [DebuggerDisplay("BoolProperty | {Name.Instanced} = {Value}")]
    public sealed class BoolProperty : Property, INotifyPropertyChanged
    {
        public override PropertyType PropType => PropertyType.BoolProperty;

        public bool Value { get; set; }

        public BoolProperty(EndianReader stream, IMEPackage pcc, NameReference? name = null, bool isArrayContained = false) : base(name)
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

        public override BoolProperty DeepClone() => (BoolProperty)MemberwiseClone();
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

    [DebuggerDisplay("ByteProperty | {Name.Instanced} = {Value}")]
    public sealed class ByteProperty : Property, INotifyPropertyChanged
    {
        public override PropertyType PropType => PropertyType.ByteProperty;

        public byte Value { get; set; }

        public ByteProperty(byte val, NameReference? name = null) : base(name)
        {
            Value = val;
        }

        public ByteProperty(EndianReader stream, NameReference? name = null) : base(name)
        {
            ValueOffset = (int)stream.Position;
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

        public override ByteProperty DeepClone() => (ByteProperty)MemberwiseClone();
        public override bool Equivalent(Property other) => other is ByteProperty byteProperty && base.Equivalent(byteProperty) && byteProperty.Value == Value;
#pragma warning disable
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore
    }

    public sealed class BioMask4Property : Property, INotifyPropertyChanged
    {
        public override PropertyType PropType => PropertyType.BioMask4Property;

        public byte Value { get; set; }

        public BioMask4Property(byte val, NameReference? name = null) : base(name)
        {
            Value = val;
        }

        public BioMask4Property(EndianReader stream, NameReference? name = null) : base(name)
        {
            ValueOffset = (int)stream.Position;
            Value = stream.ReadByte();
        }

        public override void WriteTo(EndianWriter stream, IMEPackage pcc, bool valueOnly = false)
        {
            if (!valueOnly)
            {
                stream.WritePropHeader(pcc, Name, PropType, 1, StaticArrayIndex);
            }
            stream.WriteByte(Value);
        }

        public override BioMask4Property DeepClone() => (BioMask4Property)MemberwiseClone();
        public override bool Equivalent(Property other) => other is BioMask4Property bioMask4Property && base.Equivalent(bioMask4Property) && bioMask4Property.Value == Value;
#pragma warning disable
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore
    }

    [DebuggerDisplay("EnumProperty | {Name.Instanced} = {Value.Instanced}")]
    public sealed class EnumProperty : Property, INotifyPropertyChanged
    {
        public override PropertyType PropType => PropertyType.ByteProperty;

        public NameReference EnumType { get; }
        public NameReference Value { get; set; }

        public EnumProperty(EndianReader stream, IMEPackage pcc, NameReference enumType, NameReference? name = null) : base(name)
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
                var eNameNumber = stream.ReadInt32();
                Value = new NameReference(eName, eNameNumber);
            }

        }

        public EnumProperty(NameReference value, NameReference enumType, MEGame meGame, NameReference? name = null) : base(name)
        {
            EnumType = enumType;
            NameReference enumVal = value;
            Value = enumVal;
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
            if (!GlobalUnrealObjectInfo.IsValidEnum(meGame, enumType))
            {
                throw new Exception($"{enumType} is not a valid enum type in {meGame}!");
            }
            Value = "None";
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

        public override EnumProperty DeepClone() => (EnumProperty)MemberwiseClone();
        public override bool Equivalent(Property other) => other is EnumProperty enumProperty && base.Equivalent(enumProperty) && enumProperty.EnumType == EnumType && enumProperty.Value == Value;
#pragma warning disable
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore
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

    public sealed class ImmutableByteArrayProperty : ArrayPropertyBase
    {
        public byte[] bytes;
        public ImmutableByteArrayProperty(long startOffset, int count, EndianReader stream, NameReference? name) : base(name)
        {
            ValueOffset = (int)startOffset;
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
                    Stream m = MemoryManager.GetMemoryStream();
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

        public override ImmutableByteArrayProperty DeepClone()
        {
            var clone = (ImmutableByteArrayProperty)MemberwiseClone();
            clone.bytes = bytes.ArrayClone();
            return clone;
        }

        public override bool Equivalent(Property other) => other is ImmutableByteArrayProperty arrayProperty && base.Equivalent(arrayProperty) && bytes.AsSpan().SequenceEqual(arrayProperty.bytes);

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

    [DebuggerDisplay("ArrayProperty<{typeof(T).Name,nq}> | {Name.Instanced}, Length = {Values.Count}")]
    public sealed class ArrayProperty<T> : ArrayPropertyBase, IList<T> where T : Property
    {
        public List<T> Values { get; set; }
        public override IReadOnlyList<Property> Properties => Values;

        public ArrayProperty(long startOffset, List<T> values, NameReference name) : this(values, name)
        {
            ValueOffset = (int)startOffset;
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
                stream.WriteInt32(Values.Count);
                foreach (var prop in Values)
                {
                    prop.WriteTo(stream, pcc, true);
                }
            }
        }

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

        public override bool Equivalent(Property other)
        {
            if (other is ArrayProperty<T> arrayProperty && base.Equivalent(arrayProperty) && arrayProperty.Count == Count)
            {
                for (int i = 0; i < Count; i++)
                {
                    if (!Values[i].Equivalent(arrayProperty))
                    {
                        return false;
                    }
                }
                return true;
            }
            return false;
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

    [DebuggerDisplay("StrProperty | {Name.Instanced} = {Value}")]
    public sealed class StrProperty : Property
    {
        public override PropertyType PropType => PropertyType.StrProperty;

        public string Value { get; set; }

        public StrProperty(EndianReader stream, NameReference? name = null) : base(name)
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
                stream.WriteUnrealString(Value, pcc.Game);
            }
        }

        public override StrProperty DeepClone() => (StrProperty)MemberwiseClone();
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

    [DebuggerDisplay("StringRefProperty | {Name.Instanced} = ${Value}")]
    public sealed class StringRefProperty : Property, INotifyPropertyChanged
    {
        public override PropertyType PropType => PropertyType.StringRefProperty;

        public int Value { get; set; }

        public StringRefProperty(EndianReader stream, NameReference? name = null) : base(name)
        {
            ValueOffset = (int)stream.Position;
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

        public override StringRefProperty DeepClone() => (StringRefProperty)MemberwiseClone();
        public override bool Equivalent(Property other) => other is StringRefProperty stringRefProperty && base.Equivalent(stringRefProperty) && stringRefProperty.Value == Value;

#pragma warning disable
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore
    }

    public sealed class DelegateProperty : Property, INotifyPropertyChanged
    {
        public override PropertyType PropType => PropertyType.DelegateProperty;

        public ScriptDelegate Value { get; set; }

        public DelegateProperty(EndianReader stream, IMEPackage pcc, NameReference? name = null) : base(name)
        {
            ValueOffset = (int)stream.Position;
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

        public override DelegateProperty DeepClone() => (DelegateProperty)MemberwiseClone();
        public override bool Equivalent(Property other)
        {
            return other is DelegateProperty delegateProperty && base.Equivalent(delegateProperty) && delegateProperty.Value == Value;
        }
#pragma warning disable
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore
    }

    public sealed class UnknownProperty : Property
    {
        public override PropertyType PropType => PropertyType.Unknown;

        public byte[] raw;
        public readonly string TypeName;

        public UnknownProperty(EndianReader stream, int size, string typeName = null, NameReference? name = null) : base(name)
        {
            ValueOffset = (int)stream.Position;
            TypeName = typeName ?? "Unknown";
            raw = stream.ReadBytes(size);
#if AZURE
            Assert.Fail("Encountered an unknownproperty!");
#endif
            LECLog.Warning($@"Initializing an UnknownProperty object! Position: 0x{stream.Position - size:X8}");
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

        public override UnknownProperty DeepClone()
        {
            var clone = (UnknownProperty)MemberwiseClone();
            clone.raw = raw.ArrayClone();
            return clone;
        }

        public override bool Equivalent(Property other) => false;
    }
}
