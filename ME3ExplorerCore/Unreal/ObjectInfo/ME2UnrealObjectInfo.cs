using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.MEDirectories;
using ME3ExplorerCore.Packages;
using ME3ExplorerCore.Unreal.BinaryConverters;
using ME3ExplorerCore.Unreal.ObjectInfo;
using Newtonsoft.Json;

namespace ME3ExplorerCore.Unreal
{
    public static class ME2UnrealObjectInfo
    {
#if AZURE
        /// <summary>
        /// Full path to where mini files are stored (Core.u, Engine.pcc, for example) to enable dynamic lookup of property info like struct defaults
        /// </summary>
        public static string MiniGameFilesPath { get; set; }
#endif
        public static Dictionary<string, ClassInfo> Classes = new Dictionary<string, ClassInfo>();
        public static Dictionary<string, ClassInfo> Structs = new Dictionary<string, ClassInfo>();
        public static Dictionary<string, List<NameReference>> Enums = new Dictionary<string, List<NameReference>>();
        public static Dictionary<string, SequenceObjectInfo> SequenceObjects = new Dictionary<string, SequenceObjectInfo>();

        public static bool IsLoaded;
        public static void loadfromJSON()
        {
            if (!IsLoaded)
            {
                try
                {
                    var infoText = ObjectInfoLoader.LoadEmbeddedJSONText(MEGame.ME2);
                    if (infoText != null)
                    {
                        var blob = JsonConvert.DeserializeAnonymousType(infoText, new { Classes, Structs, Enums });
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
                        IsLoaded = true;
                    }
                }
                catch (Exception)
                {
                    return;
                }
            }
        }

        public static SequenceObjectInfo getSequenceObjectInfo(string className)
        {
            return SequenceObjects.TryGetValue(className, out SequenceObjectInfo seqInfo) ? seqInfo : null;
        }

        public static string getEnumTypefromProp(string className, string propName, bool inStruct = false, ClassInfo nonVanillaClassInfo = null)
        {
            PropertyInfo p = getPropertyInfo(className, propName, inStruct, nonVanillaClassInfo);
            if (p == null && !inStruct)
            {
                p = getPropertyInfo(className, propName, true, nonVanillaClassInfo);
            }
            return p?.Reference;
        }

        public static List<NameReference> getEnumfromProp(string className, string propName, bool inStruct = false)
        {
            Dictionary<string, ClassInfo> temp = inStruct ? Structs : Classes;
            if (temp.ContainsKey(className))
            {
                ClassInfo info = temp[className];
                //look in class properties
                if (info.properties.TryGetValue(propName, out var propInfo))
                {
                    if (Enums.ContainsKey(propInfo.Reference))
                    {
                        return Enums[propInfo.Reference];
                    }
                }
                //look in structs
                else
                {
                    foreach (PropertyInfo p in info.properties.Values())
                    {
                        if (p.Type == PropertyType.StructProperty || p.Type == PropertyType.ArrayProperty)
                        {
                            List<NameReference> vals = getEnumfromProp(p.Reference, propName, true);
                            if (vals != null)
                            {
                                return vals;
                            }
                        }
                    }
                }
                //look in base class
                if (temp.ContainsKey(info.baseClass))
                {
                    List<NameReference> vals = getEnumfromProp(info.baseClass, propName, inStruct);
                    if (vals != null)
                    {
                        return vals;
                    }
                }
            }
            return null;
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

        public static ArrayType getArrayType(string className, string propName, bool inStruct = false, ExportEntry export = null)
        {
            PropertyInfo p = getPropertyInfo(className, propName, inStruct, containingExport: export)
                          ?? getPropertyInfo(className, propName, !inStruct, containingExport: export);
            if (p == null && export != null)
            {
                if (export.Class is ExportEntry classExport)
                {
                    export = classExport;
                }
                if (export.IsClass)
                {
                    ClassInfo currentInfo = generateClassInfo(export);
                    currentInfo.baseClass = export.SuperClassName;
                    p = getPropertyInfo(className, propName, inStruct, currentInfo, containingExport: export)
                     ?? getPropertyInfo(className, propName, !inStruct, currentInfo, containingExport: export);
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
                if (p.Reference == "StringRefProperty")
                {
                    return ArrayType.StringRef;
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
            Debug.WriteLine("ME2 Array type lookup failed due to no info provided, defaulting to int");

            // TODO: UNKNOWN ARRAY TYPES AS INT
            // MAYBE COMPILE FLAG?
            // SETTINGS WITH CONDITIONAL COMP FLAG?
            //if (Settings.Default.PropertyParsingME2UnknownArrayAsObject) return ArrayType.Object;
            return ArrayType.Int;
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

                foreach (PropertyInfo p in info.properties.Values())
                {
                    if ((p.Type == PropertyType.StructProperty || p.Type == PropertyType.ArrayProperty) && reSearch)
                    {
                        PropertyInfo val = getPropertyInfo(p.Reference, propName, true, nonVanillaClassInfo, reSearch: false);
                        if (val != null)
                        {
                            return val;
                        }
                    }
                }
                //look in base class
                if (temp.ContainsKey(info.baseClass))
                {
                    PropertyInfo val = getPropertyInfo(info.baseClass, propName, inStruct, nonVanillaClassInfo, reSearch: true);
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

        private static readonly string[] ImmutableStructs = { "Vector", "Color", "LinearColor", "TwoVectors", "Vector4", "Vector2D", "Rotator", "Guid", "Plane", "Box",
            "Quat", "Matrix", "IntPoint", "ActorReference", "PolyReference", "NavReference", "FontCharacter", "CovPosInfo",
            "CoverReference", "CoverInfo", "CoverSlot", "BioRwBox", "BioMask4Property", "RwVector2", "RwVector3", "RwVector4", "BioRwBox44" };

        public static bool IsImmutableStruct(string structName)
        {
            return ImmutableStructs.Contains(structName);
        }

        public static PropertyCollection getDefaultStructValue(string className, bool stripTransients)
        {
            bool isImmutable = UnrealObjectInfo.IsImmutable(className, MEGame.ME2);
            if (Structs.ContainsKey(className))
            {
                ClassInfo info = Structs[className];
                try
                {
                    PropertyCollection structProps = new PropertyCollection();
                    ClassInfo tempInfo = info;
                    while (tempInfo != null)
                    {
                        foreach ((string propName, PropertyInfo propInfo) in tempInfo.properties)
                        {
                            if (stripTransients && propInfo.Transient)
                            {
                                continue;
                            }
                            if (getDefaultProperty(propName, propInfo, stripTransients, isImmutable) is Property uProp)
                            {
                                structProps.Add(uProp);
                            }
                        }
                        if (!Structs.TryGetValue(tempInfo.baseClass, out tempInfo))
                        {
                            tempInfo = null;
                        }
                    }
                    structProps.Add(new NoneProperty());

                    string filepath = null;
                    if (ME2Directory.BioGamePath != null)
                    {
                        filepath = Path.Combine(ME2Directory.BioGamePath, info.pccPath);
                    }

                    Stream loadStream = null;
                    if (File.Exists(info.pccPath))
                    {
                        filepath = info.pccPath;
                        loadStream = new MemoryStream(File.ReadAllBytes(info.pccPath));
                    }
                    else if (info.pccPath == UnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName)
                    {
                        filepath = "GAMERESOURCES_ME2";
                        loadStream = Utilities.LoadFileFromCompressedResource("GameResources.zip", CoreLib.CustomResourceFileName(MEGame.ME2));
                    }
                    else if (filepath != null && File.Exists(filepath))
                    {
                        loadStream = new MemoryStream(File.ReadAllBytes(filepath));
                    }
#if AZURE
                    else if (MiniGameFilesPath != null && File.Exists(Path.Combine(MiniGameFilesPath, info.pccPath)))
                    {
                        // Load from test minigame folder. This is only really useful on azure where we don't have access to 
                        // games
                        filepath = Path.Combine(MiniGameFilesPath, info.pccPath);
                        loadStream = new MemoryStream(File.ReadAllBytes(filepath));
                    }
#endif

                    if (loadStream != null)
                    {
                        using (IMEPackage importPCC = MEPackageHandler.OpenMEPackageFromStream(loadStream, filepath, useSharedPackageCache: true))
                        {
                            var exportToRead = importPCC.GetUExport(info.exportIndex);
                            byte[] buff = exportToRead.Data.Skip(0x30).ToArray();
                            PropertyCollection defaults = PropertyCollection.ReadProps(exportToRead, new MemoryStream(buff), className);
                            foreach (var prop in defaults)
                            {
                                structProps.TryReplaceProp(prop);
                            }
                        }
                    }
                    return structProps;
                }
                catch
                {
                    return null;
                }
            }
            return null;
        }

        public static Property getDefaultProperty(string propName, PropertyInfo propInfo, bool stripTransients = true, bool isImmutable = false)
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
                    return new EnumProperty(propInfo.Reference, MEGame.ME2, propName);
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
                            return new ImmutableByteArrayProperty(propName);
                        default:
                            return null;
                    }
                case PropertyType.StructProperty:
                    isImmutable = isImmutable || UnrealObjectInfo.IsImmutable(propInfo.Reference, MEGame.ME2);
                    return new StructProperty(propInfo.Reference, getDefaultStructValue(propInfo.Reference, stripTransients), propName, isImmutable);
                case PropertyType.None:
                case PropertyType.Unknown:
                default:
                    return null;
            }
        }

        #region Generating
        //call this method to regenerate ME2ObjectInfo.json
        //Takes a long time (10 minutes maybe?). Application will be completely unresponsive during that time.
        public static void generateInfo(string outpath)
        {
            var NewClasses = new Dictionary<string, ClassInfo>();
            var NewStructs = new Dictionary<string, ClassInfo>();
            var NewEnums = new Dictionary<string, List<NameReference>>();
            var NewSequenceObjects = new Dictionary<string, SequenceObjectInfo>();

            foreach (string filePath in MELoadedFiles.GetOfficialFiles(MEGame.ME2))
            {
                if (Path.GetExtension(filePath) == ".pcc")
                {
                    using IMEPackage pcc = MEPackageHandler.OpenME2Package(filePath);
                    for (int j = 1; j <= pcc.ExportCount; j++)
                    {
                        ExportEntry exportEntry = pcc.GetUExport(j);
                        string className = exportEntry.ClassName;
                        if (className == "Enum")
                        {
                            generateEnumValues(exportEntry, NewEnums);
                        }
                        else if (className == "Class")
                        {
                            string objectName = exportEntry.ObjectName;
                            if (!NewClasses.ContainsKey(objectName))
                            {
                                NewClasses.Add(objectName, generateClassInfo(exportEntry));
                            }
                        }
                        else if (className == "ScriptStruct")
                        {
                            string objectName = exportEntry.ObjectName;
                            if (!NewStructs.ContainsKey(objectName))
                            {
                                NewStructs.Add(objectName, generateClassInfo(exportEntry, isStruct: true));

                            }
                        }
                        else if (exportEntry.IsA("SequenceObject"))
                        {
                            if (!NewSequenceObjects.TryGetValue(className, out SequenceObjectInfo seqObjInfo))
                            {
                                seqObjInfo = new SequenceObjectInfo();
                                NewSequenceObjects.Add(className, seqObjInfo);
                            }

                            int objInstanceVersion = exportEntry.GetProperty<IntProperty>("ObjInstanceVersion");
                            if (objInstanceVersion > seqObjInfo.ObjInstanceVersion)
                            {
                                seqObjInfo.ObjInstanceVersion = objInstanceVersion;
                            }
                        }
                    }
                    //Debug.WriteLine("Releasing " + pcc.FileName);
                }
            }

            //CUSTOM ADDITIONS
            try
            {
                NewClasses.Add("LightMapTexture2D", new ClassInfo
                {
                    baseClass = "Texture2D",
                    exportIndex = 0,
                    pccPath = UnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName
                });

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
                        new KeyValuePair<string, PropertyInfo>("UseFullPrecisionUVs", new PropertyInfo(PropertyType.BoolProperty)),
                        new KeyValuePair<string, PropertyInfo>("BodySetup", new PropertyInfo(PropertyType.ObjectProperty, "RB_BodySetup")),
                        new KeyValuePair<string, PropertyInfo>("LODDistanceRatio", new PropertyInfo(PropertyType.FloatProperty)),
                        new KeyValuePair<string, PropertyInfo>("LightMapCoordinateIndex", new PropertyInfo(PropertyType.IntProperty)),
                        new KeyValuePair<string, PropertyInfo>("LightMapResolution", new PropertyInfo(PropertyType.IntProperty)),
                    }
                };
            }
            catch (Exception)
            {

            }

            //SFXPhysicalMaterialDecals missing items
            ClassInfo sfxpmd = NewClasses["SFXPhysicalMaterialDecals"];
            string[] decalComponentArrays = { "HeavyPistol", "AutoPistol", "HandCannon", "SMG", "Shotgun", "HeavyShotgun", "FlakGun", "AssaultRifle", "Needler", "Machinegun", "SniperRifle", "AntiMatRifle", "MassCannon", "ParticleBeam" };
            foreach (string decal in decalComponentArrays)
            {
                sfxpmd.properties.Add(decal, new PropertyInfo(PropertyType.ArrayProperty, "DecalComponent"));
            }

            NewClasses["SFXWeapon"].properties.Add("InstantHitDamageTypes", new PropertyInfo(PropertyType.ArrayProperty, "Class"));

            File.WriteAllText("ME2ObjectInfo.json", JsonConvert.SerializeObject(new { SequenceObjects = NewSequenceObjects, Classes = NewClasses, Structs = NewStructs, Enums = NewEnums }, Formatting.Indented));
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
                UClass classBinary = ObjectBinary.From<UClass>(export);
                info.isAbstract = classBinary.ClassFlags.HasFlag(UnrealFlags.EClassFlags.Abstract);
            }
            if (pcc.FilePath.Contains("BioGame"))
            {
                info.pccPath = new string(pcc.FilePath.Skip(pcc.FilePath.LastIndexOf("BioGame") + 8).ToArray());
            }
            else
            {
                info.pccPath = pcc.FilePath; //used for dynamic resolution of files outside the game directory.
            }

            int nextExport = BitConverter.ToInt32(export.Data, isStruct ? 0x18 : 0x10);
            while (nextExport > 0)
            {
                var entry = pcc.GetUExport(nextExport);
                if (entry.ClassName != "ScriptStruct" && entry.ClassName != "Enum"
                    && entry.ClassName != "Function" && entry.ClassName != "Const" && entry.ClassName != "State")
                {
                    if (!info.properties.ContainsKey(entry.ObjectName))
                    {
                        PropertyInfo p = getProperty(entry);
                        if (p != null)
                        {
                            info.properties.Add(entry.ObjectName, p);
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
            string enumName = export.ObjectName;
            if (!enumTable.ContainsKey(enumName))
            {
                var values = new List<NameReference>();
                byte[] buff = export.Data;
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
                case "ClassProperty":
                case "ObjectProperty":
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
                                //if (arrayTypeProp.reference == "")
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
    }
}
