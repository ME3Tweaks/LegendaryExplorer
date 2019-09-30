using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ME1Explorer.Unreal;
using ME2Explorer.Unreal;
using ME3Explorer.Packages;
using ME3Explorer.Properties;
using ME3Explorer.Unreal.BinaryConverters;
using Newtonsoft.Json;

namespace ME3Explorer.Unreal
{
    public static class UnrealObjectInfo
    {
        internal const string Me3ExplorerCustomNativeAdditionsName = "ME3Explorer_CustomNativeAdditions";

        public static List<ClassInfo> GetNonAbstractDerivedClassesOf(string baseClassName, MEGame game) =>
            GetClasses(game).Values.Where(info => info.ClassName != baseClassName && !info.isAbstract && IsOrInheritsFrom(info.ClassName, baseClassName, game)).ToList();

        public static bool IsImmutable(string structType, MEGame game) =>
            game switch 
            {
                MEGame.ME1 => ME1UnrealObjectInfo.IsImmutableStruct(structType),
                MEGame.ME2 => ME2UnrealObjectInfo.IsImmutableStruct(structType),
                MEGame.ME3 => ME3UnrealObjectInfo.IsImmutableStruct(structType),
                MEGame.UDK => ME3UnrealObjectInfo.IsImmutableStruct(structType),
                _ => false,
            };

        public static bool IsOrInheritsFrom(this ClassInfo info, string baseClass, MEGame game) => IsOrInheritsFrom(info.ClassName, baseClass, game);
        public static bool IsOrInheritsFrom(this IEntry entry, string baseClass) => IsOrInheritsFrom(entry.ClassName, baseClass, entry.FileRef.Game);
        public static bool IsOrInheritsFrom(string className, string baseClass, MEGame game) =>
            game switch
            {
                MEGame.ME1 => ME1UnrealObjectInfo.InheritsFrom(className, baseClass),
                MEGame.ME2 => ME2UnrealObjectInfo.InheritsFrom(className, baseClass),
                MEGame.ME3 => ME3UnrealObjectInfo.InheritsFrom(className, baseClass),
                MEGame.UDK => ME3UnrealObjectInfo.InheritsFrom(className, baseClass),
                _ => false
            };

        public static string GetEnumType(MEGame game, string propName, string typeName, ClassInfo nonVanillaClassInfo = null) =>
            game switch
            {
                MEGame.ME1 => ME1UnrealObjectInfo.getEnumTypefromProp(typeName, propName, nonVanillaClassInfo: nonVanillaClassInfo),
                MEGame.ME2 => ME2UnrealObjectInfo.getEnumTypefromProp(typeName, propName, nonVanillaClassInfo: nonVanillaClassInfo),
                MEGame.ME3 => ME3UnrealObjectInfo.getEnumTypefromProp(typeName, propName),
                MEGame.UDK => ME3UnrealObjectInfo.getEnumTypefromProp(typeName, propName) ?? UDKUnrealObjectInfo.getEnumTypefromProp(typeName, propName),
                _ => null
            };

        public static List<NameReference> GetEnumValues(MEGame game, string enumName, bool includeNone = false) =>
            game switch
            {
                MEGame.ME1 => ME1UnrealObjectInfo.getEnumValues(enumName, includeNone),
                MEGame.ME2 => ME2UnrealObjectInfo.getEnumValues(enumName, includeNone),
                MEGame.ME3 => ME3UnrealObjectInfo.getEnumValues(enumName, includeNone),
                MEGame.UDK => ME3UnrealObjectInfo.getEnumValues(enumName, includeNone),
                _ => null
            };

        /// <summary>
        /// Recursively gets class defaults, traveling up inheritiance chain, but stopping at <paramref name="notIncludingClass"></paramref>
        /// </summary>
        /// <param name="game"></param>
        /// <param name="className"></param>
        /// <param name="notIncludingClass"></param>
        /// <returns></returns>
        public static PropertyCollection getClassDefaults(MEGame game, string className, string notIncludingClass = null)
        {
            Dictionary<string, ClassInfo> classes = GetClasses(game);
            if (classes.TryGetValue(className, out ClassInfo info))
            {
                try
                {
                    PropertyCollection props = new PropertyCollection();
                    while (info != null && info.ClassName != notIncludingClass)
                    {
                        string filepath = Path.Combine(MEDirectories.BioGamePath(game), info.pccPath);
                        if (File.Exists(info.pccPath))
                        {
                            filepath = info.pccPath; //Used for dynamic lookup
                        }
                        if (game == MEGame.ME1 && !File.Exists(filepath))
                        {
                            filepath = Path.Combine(ME1Directory.gamePath, info.pccPath); //for files from ME1 DLC
                        }
                        if (File.Exists(filepath))
                        {
                            using IMEPackage importPCC = MEPackageHandler.OpenMEPackage(filepath);
                            ExportEntry classExport = importPCC.GetUExport(info.exportIndex);
                            UClass classBin = ObjectBinary.From<UClass>(classExport);
                            ExportEntry defaults = importPCC.GetUExport(classBin.Defaults);

                            foreach (var prop in defaults.GetProperties())
                            {
                                if (!props.ContainsNamedProp(prop.Name))
                                {
                                    props.Add(prop);
                                }
                            }
                        }

                        classes.TryGetValue(info.baseClass, out info);
                    }
                    return props;
                }
                catch
                {
                    return null;
                }
            }
            return null;
        }

        /// <summary>
        /// Gets the type of an array
        /// </summary>
        /// <param name="game">What game we are looking info for</param>
        /// <param name="propName">Name of the array property</param>
        /// <param name="className">Name of the class that should contain the information. If contained in a struct, this will be the name of the struct type</param>
        /// <param name="parsingEntry">Entry that is being parsed. Used for dynamic lookup if it's not in the DB</param>
        /// <returns></returns>
        public static ArrayType GetArrayType(MEGame game, string propName, string className, IEntry parsingEntry = null)
        {
            switch (game)
            {
                case MEGame.ME1:
                    return ME1UnrealObjectInfo.getArrayType(className, propName, export: parsingEntry as ExportEntry);
                case MEGame.ME2:
                    var res2 = ME2UnrealObjectInfo.getArrayType(className, propName, export: parsingEntry as ExportEntry);
#if DEBUG
                    //For debugging only!
                    if (res2 == ArrayType.Int && ME2UnrealObjectInfo.ArrayTypeLookupJustFailed)
                    {
                        ME2UnrealObjectInfo.ArrayTypeLookupJustFailed = false;
                        Debug.WriteLine("[ME2] Array type lookup failed for " + propName + " in class " + className + " in export " + parsingEntry.FileRef.GetEntryString(parsingEntry.UIndex));
                    }
#endif
                    return res2;
                case MEGame.ME3:
                case MEGame.UDK:
                    var res = ME3UnrealObjectInfo.getArrayType(className, propName, export: parsingEntry as ExportEntry);
#if DEBUG
                    //For debugging only!
                    if (res == ArrayType.Int && ME3UnrealObjectInfo.ArrayTypeLookupJustFailed)
                    {
                        if (game == MEGame.UDK)
                        {
                            var ures = UDKUnrealObjectInfo.getArrayType(className, propName: propName, export: parsingEntry as ExportEntry);
                            if (ures == ArrayType.Int && UDKUnrealObjectInfo.ArrayTypeLookupJustFailed)
                            {
                                Debug.WriteLine("[UDK] Array type lookup failed for " + propName + " in class " + className + " in export " + parsingEntry.FileRef.GetEntryString(parsingEntry.UIndex));
                                UDKUnrealObjectInfo.ArrayTypeLookupJustFailed = false;
                            }
                            else
                            {
                                return ures;
                            }
                        }
                        Debug.WriteLine("[ME3] Array type lookup failed for " + propName + " in class " + className + " in export " + parsingEntry.FileRef.GetEntryString(parsingEntry.UIndex));
                        ME3UnrealObjectInfo.ArrayTypeLookupJustFailed = false;
                    }
#endif
                    return res;
            }
            return ArrayType.Int;
        }

        /// <summary>
        /// Gets property information for a property by name & containing class or struct name
        /// </summary>
        /// <param name="game">Game to lookup informatino from</param>
        /// <param name="propname">Name of property information to look up</param>
        /// <param name="containingClassOrStructName">Name of containing class or struct name</param>
        /// <param name="nonVanillaClassInfo">Dynamically built property info</param>
        /// <returns></returns>
        public static PropertyInfo GetPropertyInfo(MEGame game, string propname, string containingClassOrStructName, ClassInfo nonVanillaClassInfo = null)
        {
            bool inStruct = false;
            PropertyInfo p = null;
            switch (game)
            {
                case MEGame.ME1:
                    p = ME1UnrealObjectInfo.getPropertyInfo(containingClassOrStructName, propname, inStruct, nonVanillaClassInfo);
                    break;
                case MEGame.ME2:
                    p = ME2UnrealObjectInfo.getPropertyInfo(containingClassOrStructName, propname, inStruct, nonVanillaClassInfo);
                    break;
                case MEGame.ME3:
                case MEGame.UDK:
                    p = ME3UnrealObjectInfo.getPropertyInfo(containingClassOrStructName, propname, inStruct, nonVanillaClassInfo);
                    if (p == null && game == MEGame.UDK)
                    {
                        p = UDKUnrealObjectInfo.getPropertyInfo(containingClassOrStructName, propname, inStruct, nonVanillaClassInfo);
                    }
                    break;
            }
            if (p == null)
            {
                inStruct = true;
                switch (game)
                {
                    case MEGame.ME1:
                        p = ME1UnrealObjectInfo.getPropertyInfo(containingClassOrStructName, propname, inStruct);
                        break;
                    case MEGame.ME2:
                        p = ME2UnrealObjectInfo.getPropertyInfo(containingClassOrStructName, propname, inStruct);
                        break;
                    case MEGame.ME3:
                        p = ME3UnrealObjectInfo.getPropertyInfo(containingClassOrStructName, propname, inStruct);
                        break;
                    case MEGame.UDK:
                        p = ME3UnrealObjectInfo.getPropertyInfo(containingClassOrStructName, propname, inStruct);
                        if (p == null && game == MEGame.UDK)
                        {
                            p = UDKUnrealObjectInfo.getPropertyInfo(containingClassOrStructName, propname, inStruct, nonVanillaClassInfo);
                        }
                        break;
                }
            }
            return p;
        }

        /// <summary>
        /// Gets the default values for a struct
        /// </summary>
        /// <param name="game">Game to pull info from</param>
        /// <param name="typeName">Struct type name</param>
        /// <param name="stripTransients">Strip transients from the struct</param>
        /// <returns></returns>
        internal static PropertyCollection getDefaultStructValue(MEGame game, string typeName, bool stripTransients)
        {
            switch (game)
            {
                case MEGame.ME1:
                    return ME1UnrealObjectInfo.getDefaultStructValue(typeName, stripTransients);
                case MEGame.ME2:
                    return ME2UnrealObjectInfo.getDefaultStructValue(typeName, stripTransients);
                case MEGame.ME3:
                case MEGame.UDK:
                    return ME3UnrealObjectInfo.getDefaultStructValue(typeName, stripTransients);
            }
            return null;
        }

        public static OrderedMultiValueDictionary<string, PropertyInfo> GetAllProperties(MEGame game, string typeName)
        {
            var props = new OrderedMultiValueDictionary<string, PropertyInfo>();
            ClassInfo info = GetClassOrStructInfo(game, typeName);
            while (info != null)
            {
                props.AddRange(info.properties);
                info = GetClassOrStructInfo(game, info.baseClass);
            }

            return props;
        }

        public static ClassInfo GetClassOrStructInfo(MEGame game, string typeName)
        {
            ClassInfo result = null;
            switch (game)
            {
                case MEGame.ME1:
                    _ = ME1UnrealObjectInfo.Classes.TryGetValue(typeName, out result) || ME1UnrealObjectInfo.Structs.TryGetValue(typeName, out result);
                    break;
                case MEGame.ME2:
                    _ = ME2UnrealObjectInfo.Classes.TryGetValue(typeName, out result) || ME2UnrealObjectInfo.Structs.TryGetValue(typeName, out result);
                    break;
                case MEGame.ME3:
                    _ = ME3UnrealObjectInfo.Classes.TryGetValue(typeName, out result) || ME3UnrealObjectInfo.Structs.TryGetValue(typeName, out result);
                    break;
                case MEGame.UDK:
                    _ = UDKUnrealObjectInfo.Classes.TryGetValue(typeName, out result) || UDKUnrealObjectInfo.Structs.TryGetValue(typeName, out result)
                     || ME3UnrealObjectInfo.Classes.TryGetValue(typeName, out result) || ME3UnrealObjectInfo.Structs.TryGetValue(typeName, out result);
                    break;
            }

            return result;
        }

        public static Dictionary<string, ClassInfo> GetClasses(MEGame game)
        {
            switch (game)
            {
                case MEGame.ME1:
                    return ME1UnrealObjectInfo.Classes;
                case MEGame.ME2:
                    return ME2UnrealObjectInfo.Classes;
                case MEGame.ME3:
                    return ME3UnrealObjectInfo.Classes;
                default:
                    return null;
            }
        }

        public static Dictionary<string, ClassInfo> GetStructs(MEGame game)
        {
            switch (game)
            {
                case MEGame.ME1:
                    return ME1UnrealObjectInfo.Structs;
                case MEGame.ME2:
                    return ME2UnrealObjectInfo.Structs;
                case MEGame.ME3:
                    return ME3UnrealObjectInfo.Structs;
                default:
                    return null;
            }
        }

        public static Dictionary<string, List<NameReference>> GetEnums(MEGame game)
        {
            switch (game)
            {
                case MEGame.ME1:
                    return ME1UnrealObjectInfo.Enums;
                case MEGame.ME2:
                    return ME2UnrealObjectInfo.Enums;
                case MEGame.ME3:
                    return ME3UnrealObjectInfo.Enums;
                default:
                    return null;
            }
        }

        public static ClassInfo generateClassInfo(ExportEntry export, bool isStruct = false)
        {
            switch (export.FileRef.Game)
            {
                case MEGame.ME1:
                    return ME1UnrealObjectInfo.generateClassInfo(export, isStruct);
                case MEGame.ME2:
                    return ME2UnrealObjectInfo.generateClassInfo(export, isStruct);
                case MEGame.ME3:
                case MEGame.UDK:
                    return ME3UnrealObjectInfo.generateClassInfo(export, isStruct);
            }

            return null;
        }

        public static UProperty getDefaultProperty(MEGame game, string propName, PropertyInfo propInfo, bool stripTransients = true, bool isImmutable = false)
        {
            switch (game)
            {
                case MEGame.ME1:
                    return ME1UnrealObjectInfo.getDefaultProperty(propName, propInfo, stripTransients, isImmutable);
                case MEGame.ME2:
                    return ME2UnrealObjectInfo.getDefaultProperty(propName, propInfo, stripTransients, isImmutable);
                case MEGame.ME3:
                case MEGame.UDK:
                    return ME3UnrealObjectInfo.getDefaultProperty(propName, propInfo, stripTransients, isImmutable);
            }

            return null;
        }
    }

    public static class ME3UnrealObjectInfo
    {
        public class SequenceObjectInfo
        {
            public List<string> inputLinks;

            public SequenceObjectInfo()
            {
                inputLinks = new List<string>();
            }
        }

        public static Dictionary<string, ClassInfo> Classes = new Dictionary<string, ClassInfo>();
        public static Dictionary<string, ClassInfo> Structs = new Dictionary<string, ClassInfo>();
        public static Dictionary<string, SequenceObjectInfo> SequenceObjects = new Dictionary<string, SequenceObjectInfo>();
        public static Dictionary<string, List<NameReference>> Enums = new Dictionary<string, List<NameReference>>();

        private static readonly string[] ImmutableStructs = { "Vector", "Color", "LinearColor", "TwoVectors", "Vector4", "Vector2D", "Rotator", "Guid", "Plane", "Box",
            "Quat", "Matrix", "IntPoint", "ActorReference", "ActorReference", "ActorReference", "PolyReference", "AimTransform", "AimTransform", "AimOffsetProfile", "FontCharacter",
            "CoverReference", "CoverInfo", "CoverSlot", "BioRwBox", "BioMask4Property", "RwVector2", "RwVector3", "RwVector4", "RwPlane", "RwQuat", "BioRwBox44" };

        private static readonly string jsonPath = Path.Combine(App.ExecFolder, "ME3ObjectInfo.json");

        public static bool IsImmutableStruct(string structName)
        {
            return ImmutableStructs.Contains(structName);
        }

        public static void loadfromJSON()
        {

            try
            {
                if (File.Exists(jsonPath))
                {
                    string raw = File.ReadAllText(jsonPath);
                    var blob = JsonConvert.DeserializeAnonymousType(raw, new { SequenceObjects, Classes, Structs, Enums });
                    SequenceObjects = blob.SequenceObjects;
                    Classes = blob.Classes;
                    Structs = blob.Structs;
                    Enums = blob.Enums;
                    foreach ((string className, ClassInfo classInfo) in Classes)
                    {
                        classInfo.ClassName = className;
                    }
                    foreach ((string className, ClassInfo classInfo) in Structs)
                    {
                        classInfo.ClassName = className;
                    }
                }
            }
            catch (Exception e)
            {
            }
        }

        public static SequenceObjectInfo getSequenceObjectInfo(string objectName)
        {
            if (objectName.StartsWith("Default__"))
            {
                objectName = objectName.Substring(9);
            }
            if (SequenceObjects.ContainsKey(objectName))
            {
                if (SequenceObjects[objectName].inputLinks != null && SequenceObjects[objectName].inputLinks.Count > 0)
                {
                    return SequenceObjects[objectName];
                }

                return getSequenceObjectInfo(Classes[objectName].baseClass);
            }
            return null;
        }

        public static string getEnumTypefromProp(string className, string propName)
        {
            PropertyInfo p = getPropertyInfo(className, propName);
            if (p == null)
            {
                p = getPropertyInfo(className, propName, true);
            }
            return p?.Reference;
        }

        public static List<NameReference> getEnumValues(string enumName, bool includeNone = false)
        {
            if (Enums.ContainsKey(enumName))
            {
                var values = new List<NameReference>(Enums[enumName]);
                if (includeNone)
                {
                    values.Insert(0, "None");
                }
                return values;
            }
            return null;
        }

        public static ArrayType getArrayType(string className, string propName, ExportEntry export = null)
        {
            PropertyInfo p = getPropertyInfo(className, propName, false, containingExport: export);
            if (p == null)
            {
                p = getPropertyInfo(className, propName, true, containingExport: export);
            }
            if (p == null && export != null)
            {
                if (!export.IsClass && export.Class is ExportEntry classExport)
                {
                    export = classExport;
                }
                if (export.IsClass)
                {
                    ClassInfo currentInfo = generateClassInfo(export);
                    currentInfo.baseClass = export.SuperClassName;
                    p = getPropertyInfo(className, propName, false, currentInfo, containingExport: export);
                    if (p == null)
                    {
                        p = getPropertyInfo(className, propName, true, currentInfo, containingExport: export);
                    }
                }
            }
            return getArrayType(p);
        }

#if DEBUG
        public static bool ArrayTypeLookupJustFailed;
#endif

        public static ArrayType getArrayType(PropertyInfo p)
        {
            if (p != null)
            {
                if (p.Reference == "NameProperty")
                {
                    return ArrayType.Name;
                }

                if (Enums.ContainsKey(p.Reference))
                {
                    return ArrayType.Enum;
                }

                if (p.Reference == "BoolProperty")
                {
                    return ArrayType.Bool;
                }

                if (p.Reference == "ByteProperty")
                {
                    return ArrayType.Byte;
                }

                if (p.Reference == "StrProperty")
                {
                    return ArrayType.String;
                }

                if (p.Reference == "FloatProperty")
                {
                    return ArrayType.Float;
                }

                if (p.Reference == "IntProperty")
                {
                    return ArrayType.Int;
                }

                if (Structs.ContainsKey(p.Reference))
                {
                    return ArrayType.Struct;
                }

                return ArrayType.Object;
            }
#if DEBUG
            ArrayTypeLookupJustFailed = true;
#endif
            Debug.WriteLine("ME3 Array type lookup failed due to no info provided, defaulting to int");
            return Settings.Default.PropertyParsingME3UnknownArrayAsObject ? ArrayType.Object : ArrayType.Int;
        }

        public static PropertyInfo getPropertyInfo(string className, string propName, bool inStruct = false, ClassInfo nonVanillaClassInfo = null, bool reSearch = true, ExportEntry containingExport = null)
        {
            if (className.StartsWith("Default__"))
            {
                className = className.Substring(9);
            }
            Dictionary<string, ClassInfo> temp = inStruct ? Structs : Classes;
            bool infoExists = temp.TryGetValue(className, out ClassInfo info);
            if (!infoExists && nonVanillaClassInfo != null)
            {
                info = nonVanillaClassInfo;
                infoExists = true;
            }
            if (infoExists) //|| (temp = !inStruct ? Structs : Classes).ContainsKey(className))
            {
                //look in class properties
                if (info.properties.TryGetValue(propName, out var propInfo))
                {
                    return propInfo;
                }
                //look in structs

                if (inStruct)
                {
                    foreach (PropertyInfo p in info.properties.Values())
                    {
                        if ((p.Type == PropertyType.StructProperty || p.Type == PropertyType.ArrayProperty) && reSearch)
                        {
                            PropertyInfo val = getPropertyInfo(p.Reference, propName, true, nonVanillaClassInfo);
                            if (val != null)
                            {
                                return val;
                            }
                        }
                    }
                }
                //look in base class
                if (temp.ContainsKey(info.baseClass))
                {
                    PropertyInfo val = getPropertyInfo(info.baseClass, propName, inStruct, nonVanillaClassInfo);
                    if (val != null)
                    {
                        return val;
                    }
                }
                else
                {
                    //Baseclass may be modified as well...
                    if (containingExport?.SuperClass is ExportEntry parentExport)
                    {
                        //Class parent is in this file. Generate class parent info and attempt refetch
                        return getPropertyInfo(parentExport.SuperClassName, propName, inStruct, generateClassInfo(parentExport), reSearch: true, parentExport);
                    }
                }
            }

            //if (reSearch)
            //{
            //    PropertyInfo reAttempt = getPropertyInfo(className, propName, !inStruct, nonVanillaClassInfo, reSearch: false);
            //    return reAttempt; //will be null if not found.
            //}
            return null;
        }

        public static PropertyCollection getDefaultStructValue(string structName, bool stripTransients)
        {
            bool isImmutable = UnrealObjectInfo.IsImmutable(structName, MEGame.ME3);
            if (Structs.TryGetValue(structName, out ClassInfo info))
            {
                try
                {
                    PropertyCollection props = new PropertyCollection();
                    while (info != null)
                    {
                        foreach ((string propName, PropertyInfo propInfo) in info.properties)
                        {
                            if (stripTransients && propInfo.Transient)
                            {
                                continue;
                            }
                            if (getDefaultProperty(propName, propInfo, stripTransients, isImmutable) is UProperty uProp)
                            {
                                props.Add(uProp);
                            }
                        }
                        string filepath = Path.Combine(ME3Directory.gamePath, "BIOGame", info.pccPath);
                        if (File.Exists(info.pccPath))
                        {
                            filepath = info.pccPath; //Used for dynamic lookup
                        }
                        if (File.Exists(filepath))
                        {
                            using (IMEPackage importPCC = MEPackageHandler.OpenME3Package(filepath))
                            {
                                var exportToRead = importPCC.GetUExport(info.exportIndex);
                                byte[] buff = exportToRead.Data.Skip(0x24).ToArray();
                                PropertyCollection defaults = PropertyCollection.ReadProps(exportToRead, new MemoryStream(buff), structName);
                                foreach (var prop in defaults)
                                {
                                    props.TryReplaceProp(prop);
                                }
                            }
                        }

                        Structs.TryGetValue(info.baseClass, out info);
                    }
                    props.Add(new NoneProperty());
                    
                    return props;
                }
                catch
                {
                    return null;
                }
            }
            return null;
        }

        public static UProperty getDefaultProperty(string propName, PropertyInfo propInfo, bool stripTransients = true, bool isImmutable = false)
        {
            switch (propInfo.Type)
            {
                case PropertyType.IntProperty:
                    return new IntProperty(0, propName);
                case PropertyType.FloatProperty:
                    return new FloatProperty(0f, propName);
                case PropertyType.DelegateProperty:
                    return new DelegateProperty(0, "None");
                case PropertyType.ObjectProperty:
                    return new ObjectProperty(0, propName);
                case PropertyType.NameProperty:
                    return new NameProperty("None", propName);
                case PropertyType.BoolProperty:
                    return new BoolProperty(false, propName);
                case PropertyType.ByteProperty when propInfo.IsEnumProp():
                    return new EnumProperty(propInfo.Reference, MEGame.ME3, propName);
                case PropertyType.ByteProperty:
                    return new ByteProperty(0, propName);
                case PropertyType.StrProperty:
                    return new StrProperty("", propName);
                case PropertyType.StringRefProperty:
                    return new StringRefProperty(propName);
                case PropertyType.BioMask4Property:
                    return new BioMask4Property(0, propName);
                case PropertyType.ArrayProperty:
                    switch (getArrayType(propInfo))
                    {
                        case ArrayType.Object:
                            return new ArrayProperty<ObjectProperty>(propName);
                        case ArrayType.Name:
                            return new ArrayProperty<NameProperty>(propName);
                        case ArrayType.Enum:
                            return new ArrayProperty<EnumProperty>(propName);
                        case ArrayType.Struct:
                            return new ArrayProperty<StructProperty>(propName);
                        case ArrayType.Bool:
                            return new ArrayProperty<BoolProperty>(propName);
                        case ArrayType.String:
                            return new ArrayProperty<StrProperty>(propName);
                        case ArrayType.Float:
                            return new ArrayProperty<FloatProperty>(propName);
                        case ArrayType.Int:
                            return new ArrayProperty<IntProperty>(propName);
                        case ArrayType.Byte:
                            return new ArrayProperty<ByteProperty>(propName);
                        default:
                            return null;
                    }
                case PropertyType.StructProperty:
                    isImmutable = isImmutable || UnrealObjectInfo.IsImmutable(propInfo.Reference, MEGame.ME3);
                    return new StructProperty(propInfo.Reference, getDefaultStructValue(propInfo.Reference, stripTransients), propName, isImmutable);
                case PropertyType.None:
                case PropertyType.Unknown:
                default:
                    return null;
            }
        }

        public static bool InheritsFrom(string className, string baseClass)
        {
            while (Classes.ContainsKey(className))
            {
                if (className == baseClass)
                {
                    return true;
                }
                className = Classes[className].baseClass;
            }
            return false;
        }

        #region Generating
        //call this method to regenerate ME3ObjectInfo.json
        //Takes a long time (~5 minutes maybe?). Application will be completely unresponsive during that time.
        public static void generateInfo()
        {
            var NewClasses = new Dictionary<string, ClassInfo>();
            var NewStructs = new Dictionary<string, ClassInfo>();
            var NewEnums = new Dictionary<string, List<NameReference>>();
            var newSequenceObjects = new Dictionary<string, SequenceObjectInfo>();

            foreach (string filePath in MELoadedFiles.GetOfficialFiles(MEGame.ME3))
            {
                if (Path.GetExtension(filePath) == ".pcc")
                {
                    using IMEPackage pcc = MEPackageHandler.OpenME3Package(filePath);
                    for (int i = 1; i <= pcc.ExportCount; i++)
                    {
                        ExportEntry exportEntry = pcc.GetUExport(i);
                        if (exportEntry.ClassName == "Enum")
                        {
                            generateEnumValues(exportEntry, NewEnums);
                        }
                        else if (exportEntry.ClassName == "Class")
                        {
                            string objectName = exportEntry.ObjectName.Name;
                            if (!NewClasses.ContainsKey(objectName))
                            {
                                NewClasses.Add(objectName, generateClassInfo(exportEntry));
                            }
                            if ((objectName.Contains("SeqAct") || objectName.Contains("SeqCond") || objectName.Contains("SequenceLatentAction") ||
                                 objectName == "SequenceOp" || objectName == "SequenceAction" || objectName == "SequenceCondition") && !newSequenceObjects.ContainsKey(objectName))
                            {
                                newSequenceObjects.Add(objectName, generateSequenceObjectInfo(i, pcc));
                            }
                        }
                        else if (exportEntry.ClassName == "ScriptStruct")
                        {
                            string objectName = exportEntry.ObjectName.Name;
                            if (!NewStructs.ContainsKey(objectName))
                            {
                                NewStructs.Add(objectName, generateClassInfo(exportEntry, isStruct: true));
                            }
                        }
                    }
                }
                // System.Diagnostics.Debug.WriteLine($"{i} of {length} processed");
            }


            #region CUSTOM ADDITIONS
            //Custom additions
            //Custom additions are tweaks and additional classes either not automatically able to be determined
            //or by new classes designed in the modding scene that must be present in order for parsing to work properly

            //Kinkojiro - New Class - SFXSeqAct_AttachToSocket
            NewClasses["SFXSeqAct_AttachToSocket"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = UnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 0,
                properties =
                {
                    new KeyValuePair<string, PropertyInfo>("PSC2Component", new PropertyInfo(PropertyType.ObjectProperty, "ParticleSystemComponent")),
                    new KeyValuePair<string, PropertyInfo>("PSC1Component", new PropertyInfo(PropertyType.ObjectProperty, "ParticleSystemComponent")),
                    new KeyValuePair<string, PropertyInfo>("SkMeshComponent", new PropertyInfo(PropertyType.ObjectProperty, "SkeletalMeshComponent")),
                    new KeyValuePair<string, PropertyInfo>("TargetPawn", new PropertyInfo(PropertyType.ObjectProperty, "Actor")),
                    new KeyValuePair<string, PropertyInfo>("AttachSocketName", new PropertyInfo(PropertyType.NameProperty, "ParticleSystemComponent"))
                }
            };

            //Kinkojiro - New Class - BioSeqAct_ShowMedals
            //Sequence object for showing the medals UI
            NewClasses["BioSeqAct_ShowMedals"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = UnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 0,
                properties =
                {
                    new KeyValuePair<string, PropertyInfo>("bFromMainMenu", new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<string, PropertyInfo>("m_oGuiReferenced", new PropertyInfo(PropertyType.ObjectProperty, "GFxMovieInfo"))
                }
            };

            //Kinkojiro - New Class - SFXSeqAct_SetFaceFX
            NewClasses["SFXSeqAct_SetFaceFX"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = UnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 0,
                properties =
                {
                    new KeyValuePair<string, PropertyInfo>("m_aoTargets", new PropertyInfo(PropertyType.ArrayProperty, "Actor")),
                    new KeyValuePair<string, PropertyInfo>("m_pDefaultFaceFXAsset", new PropertyInfo(PropertyType.ObjectProperty, "FaceFXAsset"))
                }
            };

            NewClasses["LightMapTexture2D"] = new ClassInfo
            {
                baseClass = "Texture2D",
                exportIndex = 0,
                pccPath = UnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName
            };

            NewClasses["Package"] = new ClassInfo
            {
                baseClass = "Object",
                exportIndex = 0,
                pccPath = UnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName
            };

            NewClasses["StaticMesh"] = new ClassInfo
            {
                baseClass = "Object",
                exportIndex = 0,
                pccPath = UnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                properties =
                {
                    new KeyValuePair<string, PropertyInfo>("UseSimpleRigidBodyCollision", new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<string, PropertyInfo>("UseSimpleLineCollision", new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<string, PropertyInfo>("UseSimpleBoxCollision", new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<string, PropertyInfo>("bUsedForInstancing", new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<string, PropertyInfo>("ForceDoubleSidedShadowVolumes", new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<string, PropertyInfo>("UseFullPrecisionUVs", new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<string, PropertyInfo>("BodySetup", new PropertyInfo(PropertyType.ObjectProperty, "RB_BodySetup")),
                    new KeyValuePair<string, PropertyInfo>("LODDistanceRatio", new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<string, PropertyInfo>("LightMapCoordinateIndex", new PropertyInfo(PropertyType.IntProperty)),
                    new KeyValuePair<string, PropertyInfo>("LightMapResolution", new PropertyInfo(PropertyType.IntProperty)),
                }
            };

            NewClasses["FracturedStaticMesh"] = new ClassInfo
            {
                baseClass = "StaticMesh",
                exportIndex = 0,
                pccPath = UnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                properties =
                {
                    new KeyValuePair<string, PropertyInfo>("LoseChunkOutsideMaterial", new PropertyInfo(PropertyType.ObjectProperty, "MaterialInterface")),
                    new KeyValuePair<string, PropertyInfo>("bSpawnPhysicsChunks", new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<string, PropertyInfo>("bCompositeChunksExplodeOnImpact", new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<string, PropertyInfo>("ExplosionVelScale", new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<string, PropertyInfo>("FragmentMinHealth", new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<string, PropertyInfo>("FragmentDestroyEffects", new PropertyInfo(PropertyType.ArrayProperty, "ParticleSystem")),
                    new KeyValuePair<string, PropertyInfo>("FragmentMaxHealth", new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<string, PropertyInfo>("bAlwaysBreakOffIsolatedIslands", new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<string, PropertyInfo>("DynamicOutsideMaterial", new PropertyInfo(PropertyType.ObjectProperty, "MaterialInterface")),
                    new KeyValuePair<string, PropertyInfo>("ChunkLinVel", new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<string, PropertyInfo>("ChunkAngVel", new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<string, PropertyInfo>("ChunkLinHorizontalScale", new PropertyInfo(PropertyType.FloatProperty)),
                }
            };

            #endregion

            File.WriteAllText(jsonPath,
                JsonConvert.SerializeObject(new { SequenceObjects = newSequenceObjects, Classes = NewClasses, Structs = NewStructs, Enums = NewEnums }, Formatting.Indented));
            MessageBox.Show("Done");
        }

        private static SequenceObjectInfo generateSequenceObjectInfo(int i, IMEPackage pcc)
        {
            SequenceObjectInfo info = new SequenceObjectInfo();
            //+1 to get the Default__ instance
            var inLinks = pcc.GetUExport(i + 1).GetProperty<ArrayProperty<StructProperty>>("InputLinks");
            if (inLinks != null)
            {
                foreach (var seqOpInputLink in inLinks)
                {
                    info.inputLinks.Add(seqOpInputLink.GetProp<StrProperty>("LinkDesc").Value);
                }
            }
            return info;
        }

        public static ClassInfo generateClassInfo(ExportEntry export, bool isStruct = false)
        {
            IMEPackage pcc = export.FileRef;
            ClassInfo info = new ClassInfo
            {
                baseClass = export.SuperClassName,
                exportIndex = export.UIndex,
                ClassName = export.ObjectName
            };
            if (!isStruct)
            {
                BinaryConverters.UClass classBinary = BinaryConverters.ObjectBinary.From<BinaryConverters.UClass>(export);
                info.isAbstract = classBinary.ClassFlags.HasFlag(UnrealFlags.EClassFlags.Abstract);
            }
            if (pcc.FilePath.Contains("BIOGame"))
            {
                info.pccPath = new string(pcc.FilePath.Skip(pcc.FilePath.LastIndexOf("BIOGame") + 8).ToArray());
            }
            else
            {
                info.pccPath = pcc.FilePath; //used for dynamic resolution of files outside the game directory.
            }
            int nextExport = BitConverter.ToInt32(export.Data, isStruct ? 0x14 : 0xC);
            while (nextExport > 0)
            {
                var entry = pcc.GetUExport(nextExport);
                if (entry.ClassName != "ScriptStruct" && entry.ClassName != "Enum"
                    && entry.ClassName != "Function" && entry.ClassName != "Const" && entry.ClassName != "State")
                {
                    if (!info.properties.ContainsKey(entry.ObjectName.Name))
                    {
                        PropertyInfo p = getProperty(entry);
                        if (p != null)
                        {
                            info.properties.Add(entry.ObjectName.Name, p);
                        }
                    }
                }
                nextExport = BitConverter.ToInt32(entry.Data, 0x10);
            }
            return info;
        }

        private static void generateEnumValues(ExportEntry export, Dictionary<string, List<NameReference>> NewEnums = null)
        {
            var enumTable = NewEnums ?? Enums;
            string enumName = export.ObjectName.Name;
            if (!enumTable.ContainsKey(enumName))
            {
                var values = new List<NameReference>();
                byte[] buff = export.Data;
                //subtract 1 so that we don't get the MAX value, which is an implementation detail
                int count = BitConverter.ToInt32(buff, 20) - 1;
                for (int i = 0; i < count; i++)
                {
                    int enumValIndex = 24 + i * 8;
                    values.Add(new NameReference(export.FileRef.Names[BitConverter.ToInt32(buff, enumValIndex)], BitConverter.ToInt32(buff, enumValIndex + 4)));
                }
                enumTable.Add(enumName, values);
            }
        }

        private static PropertyInfo getProperty(ExportEntry entry)
        {
            IMEPackage pcc = entry.FileRef;

            string reference = null;
            PropertyType type;
            switch (entry.ClassName)
            {
                case "IntProperty":
                    type = PropertyType.IntProperty;
                    break;
                case "StringRefProperty":
                    type = PropertyType.StringRefProperty;
                    break;
                case "FloatProperty":
                    type = PropertyType.FloatProperty;
                    break;
                case "BoolProperty":
                    type = PropertyType.BoolProperty;
                    break;
                case "StrProperty":
                    type = PropertyType.StrProperty;
                    break;
                case "NameProperty":
                    type = PropertyType.NameProperty;
                    break;
                case "DelegateProperty":
                    type = PropertyType.DelegateProperty;
                    break;
                case "ObjectProperty":
                case "ClassProperty":
                case "ComponentProperty":
                    type = PropertyType.ObjectProperty;
                    reference = pcc.getObjectName(BitConverter.ToInt32(entry.Data, entry.Data.Length - 4));
                    break;
                case "StructProperty":
                    type = PropertyType.StructProperty;
                    reference = pcc.getObjectName(BitConverter.ToInt32(entry.Data, entry.Data.Length - 4));
                    break;
                case "BioMask4Property":
                case "ByteProperty":
                    type = PropertyType.ByteProperty;
                    reference = pcc.getObjectName(BitConverter.ToInt32(entry.Data, entry.Data.Length - 4));
                    break;
                case "ArrayProperty":
                    type = PropertyType.ArrayProperty;
                    PropertyInfo arrayTypeProp = getProperty(pcc.GetUExport(BitConverter.ToInt32(entry.Data, 44)));
                    if (arrayTypeProp != null)
                    {
                        switch (arrayTypeProp.Type)
                        {
                            case PropertyType.ObjectProperty:
                            case PropertyType.StructProperty:
                            case PropertyType.ArrayProperty:
                                reference = arrayTypeProp.Reference;
                                break;
                            case PropertyType.ByteProperty:
                                if (arrayTypeProp.Reference == "Class")
                                    reference = arrayTypeProp.Type.ToString();
                                else
                                    reference = arrayTypeProp.Reference;
                                break;
                            case PropertyType.IntProperty:
                            case PropertyType.FloatProperty:
                            case PropertyType.NameProperty:
                            case PropertyType.BoolProperty:
                            case PropertyType.StrProperty:
                            case PropertyType.StringRefProperty:
                            case PropertyType.DelegateProperty:
                                reference = arrayTypeProp.Type.ToString();
                                break;
                            case PropertyType.None:
                            case PropertyType.Unknown:
                            default:
                                Debugger.Break();
                                return null;
                        }
                    }
                    else
                    {
                        return null;
                    }
                    break;
                case "InterfaceProperty":
                default:
                    return null;
            }

            bool transient = ((UnrealFlags.EPropertyFlags)BitConverter.ToUInt64(entry.Data, 24)).HasFlag(UnrealFlags.EPropertyFlags.Transient);
            return new PropertyInfo(type, reference, transient);
        }
        #endregion

        #region CodeGen
        public static void GenerateCode()
        {
            GenerateEnums();
            GenerateStructs();
            GenerateClasses();
        }
        private static void GenerateClasses()
        {
            using (var fileStream = new FileStream(Path.Combine(App.ExecFolder, "ME3Classes.cs"), FileMode.Create))
            using (var writer = new CodeWriter(fileStream))
            {
                writer.WriteLine("using ME3Explorer.Unreal.ME3Enums;");
                writer.WriteLine("using ME3Explorer.Unreal.ME3Structs;");
                writer.WriteLine("using NameReference = ME3Explorer.Unreal.NameReference;");
                writer.WriteLine();
                writer.WriteBlock("namespace ME3Explorer.Unreal.ME3Classes", () =>
                {
                    writer.WriteBlock("public class Level", () =>
                    {
                        writer.WriteLine("public float ShadowmapTotalSize;");
                        writer.WriteLine("public float LightmapTotalSize;");
                    });
                    foreach ((string className, ClassInfo info) in Classes)
                    {
                        writer.WriteBlock($"public class {className}{(info.baseClass != "Class" ? $" : {info.baseClass}" : "")}", () =>
                        {
                            foreach ((string propName, PropertyInfo propInfo) in Enumerable.Reverse(info.properties))
                            {
                                if (propInfo.Transient || propInfo.Type == PropertyType.None)
                                {
                                    continue;
                                }
                                if (propName.Contains(":") || propName == className)
                                {
                                    writer.WriteLine($"public {CSharpTypeFromUnrealType(propInfo)} _{propName.Replace(":", "")};");
                                }
                                else
                                {
                                    writer.WriteLine($"public {CSharpTypeFromUnrealType(propInfo)} {propName};");
                                }
                            }
                        });
                    }
                });
            }
        }

        private static void GenerateStructs()
        {
            using (var fileStream = new FileStream(Path.Combine(App.ExecFolder, "ME3Structs.cs"), FileMode.Create))
            using (var writer = new CodeWriter(fileStream))
            {
                writer.WriteLine("using ME3Explorer.Unreal.ME3Enums;");
                writer.WriteLine("using ME3Explorer.Unreal.ME3Classes;");
                writer.WriteLine("using NameReference = ME3Explorer.Unreal.NameReference;");
                writer.WriteLine();
                writer.WriteBlock("namespace ME3Explorer.Unreal.ME3Structs", () =>
                {
                    foreach ((string structName, ClassInfo info) in Structs)
                    {
                        writer.WriteBlock($"public class {structName}{(info.baseClass != "Class" ? $" : {info.baseClass}" : "")}", () =>
                        {
                            foreach ((string propName, PropertyInfo propInfo) in Enumerable.Reverse(info.properties))
                            {
                                if (propInfo.Transient || propInfo.Type == PropertyType.None)
                                {
                                    continue;
                                }
                                writer.WriteLine($"public {CSharpTypeFromUnrealType(propInfo)} {propName.Replace(":", "")};");
                            }
                        });
                    }
                });
            }
        }

        private static void GenerateEnums()
        {
            using (var fileStream = new FileStream(Path.Combine(App.ExecFolder, "ME3Enums.cs"), FileMode.Create))
            using (var writer = new CodeWriter(fileStream))
            {
                writer.WriteBlock("namespace ME3Explorer.Unreal.ME3Enums", () =>
                {
                    foreach ((string enumName, List<NameReference> values) in Enums)
                    {
                        writer.WriteBlock($"public enum {enumName}", () =>
                        {
                            foreach (NameReference val in values)
                            {
                                writer.WriteLine($"{val.Instanced},");
                            }
                        });
                    }
                });
            }
        }
        static string CSharpTypeFromUnrealType(PropertyInfo propInfo)
        {
            switch (propInfo.Type)
            {
                case PropertyType.StructProperty:
                    return propInfo.Reference;
                case PropertyType.IntProperty:
                    return "int";
                case PropertyType.FloatProperty:
                    return "float";
                case PropertyType.DelegateProperty:
                    return nameof(ScriptDelegate);
                case PropertyType.ObjectProperty:
                    return "int";
                case PropertyType.NameProperty:
                    return nameof(NameReference);
                case PropertyType.BoolProperty:
                    return "bool";
                case PropertyType.BioMask4Property:
                    return "byte";
                case PropertyType.ByteProperty when propInfo.IsEnumProp():
                    return propInfo.Reference;
                case PropertyType.ByteProperty:
                    return "byte";
                case PropertyType.ArrayProperty:
                    {
                        string type;
                        if (Enum.TryParse(propInfo.Reference, out PropertyType arrayType))
                        {
                            type = CSharpTypeFromUnrealType(new PropertyInfo(arrayType));
                        }
                        else if (Classes.ContainsKey(propInfo.Reference))
                        {
                            //ObjectProperty
                            type = "int";
                        }
                        else
                        {
                            type = propInfo.Reference;
                        }

                        return $"{type}[]";
                    }
                case PropertyType.StrProperty:
                    return "string";
                case PropertyType.StringRefProperty:
                    return "int";
                case PropertyType.None:
                case PropertyType.Unknown:
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        #endregion
    }
}
