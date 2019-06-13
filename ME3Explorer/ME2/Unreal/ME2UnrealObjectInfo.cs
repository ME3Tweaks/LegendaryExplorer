using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ME3Explorer;
using ME3Explorer.Packages;
using ME3Explorer.Properties;
using ME3Explorer.Unreal;
using Newtonsoft.Json;

namespace ME2Explorer.Unreal
{
    public static class ME2UnrealObjectInfo
    {
        public static Dictionary<string, ClassInfo> Classes = new Dictionary<string, ClassInfo>();
        public static Dictionary<string, ClassInfo> Structs = new Dictionary<string, ClassInfo>();
        public static Dictionary<string, List<NameReference>> Enums = new Dictionary<string, List<NameReference>>();

        private static readonly string jsonPath = Path.Combine(ME3Explorer.App.ExecFolder, "ME2ObjectInfo.json");

        public static void loadfromJSON()
        {

            try
            {
                if (File.Exists(jsonPath))
                {
                    string raw = File.ReadAllText(jsonPath);
                    var blob = JsonConvert.DeserializeAnonymousType(raw, new { Classes, Structs, Enums });
                    Classes = blob.Classes;
                    Structs = blob.Structs;
                    Enums = blob.Enums;
                }
            }
            catch
            {
            }
        }

        public static string getEnumTypefromProp(string className, string propName, bool inStruct = false, ClassInfo nonVanillaClassInfo = null)
        {
            PropertyInfo p = getPropertyInfo(className, propName, inStruct, nonVanillaClassInfo);
            if (p == null && !inStruct)
            {
                p = getPropertyInfo(className, propName, true, nonVanillaClassInfo);
            }
            return p?.reference;
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
                    if (Enums.ContainsKey(propInfo.reference))
                    {
                        return Enums[propInfo.reference];
                    }
                }
                //look in structs
                else
                {
                    foreach (PropertyInfo p in info.properties.Values())
                    {
                        if (p.type == PropertyType.StructProperty || p.type == PropertyType.ArrayProperty)
                        {
                            List<NameReference> vals = getEnumfromProp(p.reference, propName, true);
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

        public static ArrayType getArrayType(string className, string propName, bool inStruct = false, IExportEntry export = null)
        {
            PropertyInfo p = getPropertyInfo(className, propName, inStruct, containingExport: export) 
                          ?? getPropertyInfo(className, propName, !inStruct, containingExport: export);
            if (p == null && export != null)
            {
                if (export.ClassName != "Class" && export.idxClass > 0)
                {
                    export = export.FileRef.Exports[export.idxClass - 1]; //make sure you get actual class
                }
                if (export.ClassName == "Class")
                {
                    ClassInfo currentInfo = generateClassInfo(export);
                    currentInfo.baseClass = export.ClassParent;
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
                if (p.reference == "NameProperty")
                {
                    return ArrayType.Name;
                }
                if (Enums.ContainsKey(p.reference))
                {
                    return ArrayType.Enum;
                }
                if (p.reference == "BoolProperty")
                {
                    return ArrayType.Bool;
                }
                if (p.reference == "ByteProperty")
                {
                    return ArrayType.Byte;
                }
                if (p.reference == "StrProperty")
                {
                    return ArrayType.String;
                }
                if (p.reference == "FloatProperty")
                {
                    return ArrayType.Float;
                }
                if (p.reference == "IntProperty")
                {
                    return ArrayType.Int;
                }
                if (Structs.ContainsKey(p.reference))
                {
                    return ArrayType.Struct;
                }

                return ArrayType.Object;
            }
#if DEBUG
            ArrayTypeLookupJustFailed = true;
#endif
            Debug.WriteLine("ME2 Array type lookup failed due to no info provided, defaulting to int");
            if (Settings.Default.PropertyParsingME2UnknownArrayAsObject) return ArrayType.Object;
            return ArrayType.Int;
        }

        public static PropertyInfo getPropertyInfo(string className, string propName, bool inStruct = false, ClassInfo nonVanillaClassInfo = null, bool reSearch = true, IExportEntry containingExport = null)
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
                    if ((p.type == PropertyType.StructProperty || p.type == PropertyType.ArrayProperty) && reSearch)
                    {
                        PropertyInfo val = getPropertyInfo(p.reference, propName, true, nonVanillaClassInfo, reSearch: false);
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
                    if (containingExport != null && containingExport.idxClassParent > 0)
                    {
                        //Class parent is in this file. Generate class parent info and attempt refetch
                        IExportEntry parentExport = containingExport.FileRef.getUExport(containingExport.idxClassParent);
                        return getPropertyInfo(parentExport.ClassParent, propName, inStruct, generateClassInfo(parentExport), reSearch: true, parentExport);
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

        public static bool inheritsFrom(IEntry entry, string baseClass)
        {
            string className = entry.ClassName;
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
            "Quat", "Matrix", "IntPoint", "ActorReference", "PolyReference", "AimTransform", "NavReference", "FontCharacter", "CovPosInfo",
            "CoverReference", "CoverInfo", "CoverSlot", "BioRwBox", "BioMask4Property", "RwVector2", "RwVector3", "RwVector4", "BioRwBox44" };

        public static bool isImmutableStruct(string structName)
        {
            return ImmutableStructs.Contains(structName);
        }

        public static PropertyCollection getDefaultStructValue(string className, bool stripTransients)
        {
            bool isImmutable = UnrealObjectInfo.isImmutable(className, MEGame.ME2);
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
                            if (stripTransients && propInfo.transient)
                            {
                                continue;
                            }
                            if (getDefaultProperty(propName, propInfo, stripTransients, isImmutable) is UProperty uProp)
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

                    string filepath = Path.Combine(ME2Directory.gamePath, "BioGame", info.pccPath);
                    if (File.Exists(info.pccPath))
                    {
                        filepath = info.pccPath; //Used for dynamic lookup
                    }
                    if (File.Exists(filepath))
                    {
                        using (ME2Package importPCC = MEPackageHandler.OpenME2Package(filepath))
                        {
                            var exportToRead = importPCC.getUExport(info.exportIndex);
                            byte[] buff = exportToRead.Data.Skip(0x30).ToArray();
                            PropertyCollection defaults = PropertyCollection.ReadProps(importPCC, new MemoryStream(buff), className);
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

        private static UProperty getDefaultProperty(string propName, PropertyInfo propInfo, bool stripTransients = true, bool isImmutable = false)
        {
            switch (propInfo.type)
            {
                case PropertyType.IntProperty:
                    return new IntProperty(0, propName);
                case PropertyType.FloatProperty:
                    return new FloatProperty(0f, propName);
                case PropertyType.ObjectProperty:
                case PropertyType.DelegateProperty:
                    return new ObjectProperty(0, propName);
                case PropertyType.NameProperty:
                    return new NameProperty() { Value = "None", Name = propName };
                case PropertyType.BoolProperty:
                    return new BoolProperty(false, propName);
                case PropertyType.ByteProperty when propInfo.IsEnumProp():
                    return new EnumProperty(propInfo.reference, MEGame.ME2, propName);
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
                            return new ArrayProperty<ObjectProperty>(ArrayType.Object, propName);
                        case ArrayType.Name:
                            return new ArrayProperty<NameProperty>(ArrayType.Name, propName);
                        case ArrayType.Enum:
                            return new ArrayProperty<EnumProperty>(ArrayType.Enum, propName);
                        case ArrayType.Struct:
                            return new ArrayProperty<StructProperty>(ArrayType.Struct, propName);
                        case ArrayType.Bool:
                            return new ArrayProperty<BoolProperty>(ArrayType.Bool, propName);
                        case ArrayType.String:
                            return new ArrayProperty<StrProperty>(ArrayType.String, propName);
                        case ArrayType.Float:
                            return new ArrayProperty<FloatProperty>(ArrayType.Float, propName);
                        case ArrayType.Int:
                            return new ArrayProperty<IntProperty>(ArrayType.Int, propName);
                        case ArrayType.Byte:
                            return new ArrayProperty<ByteProperty>(ArrayType.Byte, propName);
                        default:
                            return null;
                    }
                case PropertyType.StructProperty:
                    return new StructProperty(propInfo.reference, getDefaultStructValue(propInfo.reference, stripTransients), propName, isImmutable);
                case PropertyType.None:
                case PropertyType.Unknown:
                default:
                    return null;
            }
        }

        #region Generating
        //call this method to regenerate ME2ObjectInfo.json
        //Takes a long time (10 minutes maybe?). Application will be completely unresponsive during that time.
        public static void generateInfo()
        {
            var NewClasses = new Dictionary<string, ClassInfo>();
            var NewStructs = new Dictionary<string, ClassInfo>();
            var NewEnums = new Dictionary<string, List<NameReference>>();

            string path = ME2Directory.gamePath;
            string[] files = Directory.GetFiles(path, "*.pcc", SearchOption.AllDirectories);
            foreach (string file in files)
            {
                if (file.ToLower().EndsWith(".pcc")/* && file.Contains("Engine")*/)
                {
                    using (ME2Package pcc = MEPackageHandler.OpenME2Package(file))
                    {
                        for (int j = 1; j <= pcc.ExportCount; j++)
                        {
                            IExportEntry exportEntry = pcc.getUExport(j);
                            if (exportEntry.ClassName == "Enum")
                            {
                                generateEnumValues(exportEntry, NewEnums);
                            }
                            else if (exportEntry.ClassName == "Class")
                            {
                                string objectName = exportEntry.ObjectName;
                                if (!NewClasses.ContainsKey(objectName))
                                {
                                    NewClasses.Add(objectName, generateClassInfo(exportEntry));
                                }
                            }
                            else if (exportEntry.ClassName == "ScriptStruct")
                            {
                                string objectName = exportEntry.ObjectName;
                                if (!NewStructs.ContainsKey(objectName))
                                {
                                    NewStructs.Add(objectName, generateClassInfo(exportEntry, isStruct: true));

                                }
                            }
                        }
                        //Debug.WriteLine("Releasing " + pcc.FileName);
                    }
                }
            }

            //CUSTOM ADDITIONS
            ClassInfo info = new ClassInfo
            {
                baseClass = "Texture2D",
                exportIndex = 0,
                pccPath = "ME3Explorer_CustomNativeAdditions"
            };
            try
            {
                NewClasses.Add("LightMapTexture2D", info);
            }
            catch (Exception)
            {

            }

            //SFXPhysicalMaterialDecals missing items
            ClassInfo sfxpmd = NewClasses["SFXPhysicalMaterialDecals"];
            string[] decalComponentArrays = { "HeavyPistol", "AutoPistol", "HandCannon", "SMG", "Shotgun", "HeavyShotgun", "FlakGun", "AssaultRifle", "Needler", "Machinegun", "SniperRifle", "AntiMatRifle", "MassCannon", "ParticleBeam" };
            foreach (string decal in decalComponentArrays)
            {
                sfxpmd.properties.Add(decal, new PropertyInfo
                {
                    type = PropertyType.ArrayProperty,
                    reference = "DecalComponent"
                });
            }

            ClassInfo sfxweapon = NewClasses["SFXWeapon"];
            sfxweapon.properties.Add("InstantHitDamageTypes", new PropertyInfo
            {
                type = PropertyType.ArrayProperty,
                reference = "Class"
            });
            File.WriteAllText(jsonPath, JsonConvert.SerializeObject(new { Classes = NewClasses, Structs = NewStructs, Enums = NewEnums }, Formatting.Indented));
            MessageBox.Show("Done");
        }

        public static ClassInfo generateClassInfo(IExportEntry export, bool isStruct = false)
        {
            IMEPackage pcc = export.FileRef;
            ClassInfo info = new ClassInfo
            {
                baseClass = export.ClassParent,
                exportIndex = export.UIndex
            };
            if (pcc.FileName.Contains("BioGame"))
            {
                info.pccPath = new string(pcc.FileName.Skip(pcc.FileName.LastIndexOf("BioGame") + 8).ToArray());
            }
            else
            {
                info.pccPath = pcc.FileName; //used for dynamic resolution of files outside the game directory.
            }

            int nextExport = BitConverter.ToInt32(export.Data, isStruct ? 0x18 : 0x10);
            while (nextExport > 0)
            {
                var entry = pcc.getUExport(nextExport);
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

        private static void generateEnumValues(IExportEntry export, Dictionary<string, List<NameReference>> NewEnums = null)
        {
            var enumTable = NewEnums ?? Enums;
            string enumName = export.ObjectName;
            if (!enumTable.ContainsKey(enumName))
            {
                var values = new List<NameReference>();
                byte[] buff = export.Data;
                int count = BitConverter.ToInt32(buff, 20);
                for (int i = 0; i < count; i++)
                {
                    int enumValIndex = 24 + i * 8;
                    values.Add(new NameReference(export.FileRef.Names[BitConverter.ToInt32(buff, enumValIndex)], BitConverter.ToInt32(buff, enumValIndex + 4)));
                }

                enumTable.Add(enumName, values);
            }
        }

        private static PropertyInfo getProperty(IExportEntry entry)
        {
            IMEPackage pcc = entry.FileRef;
            PropertyInfo p = new PropertyInfo();
            switch (entry.ClassName)
            {
                case "IntProperty":
                    p.type = PropertyType.IntProperty;
                    break;
                case "StringRefProperty":
                    p.type = PropertyType.StringRefProperty;
                    break;
                case "FloatProperty":
                    p.type = PropertyType.FloatProperty;
                    break;
                case "BoolProperty":
                    p.type = PropertyType.BoolProperty;
                    break;
                case "StrProperty":
                    p.type = PropertyType.StrProperty;
                    break;
                case "NameProperty":
                    p.type = PropertyType.NameProperty;
                    break;
                case "DelegateProperty":
                    p.type = PropertyType.DelegateProperty;
                    break;
                case "ClassProperty":
                case "ObjectProperty":
                case "ComponentProperty":
                    p.type = PropertyType.ObjectProperty;
                    p.reference = pcc.getObjectName(BitConverter.ToInt32(entry.Data, entry.Data.Length - 4));
                    break;
                case "StructProperty":
                    p.type = PropertyType.StructProperty;
                    p.reference = pcc.getObjectName(BitConverter.ToInt32(entry.Data, entry.Data.Length - 4));
                    break;
                case "BioMask4Property":
                case "ByteProperty":
                    p.type = PropertyType.ByteProperty;
                    p.reference = pcc.getObjectName(BitConverter.ToInt32(entry.Data, entry.Data.Length - 4));
                    break;
                case "ArrayProperty":
                    p.type = PropertyType.ArrayProperty;
                    PropertyInfo arrayTypeProp = getProperty(pcc.getUExport(BitConverter.ToInt32(entry.Data, 44)));
                    if (arrayTypeProp != null)
                    {
                        switch (arrayTypeProp.type)
                        {
                            case PropertyType.ObjectProperty:
                            case PropertyType.StructProperty:
                            case PropertyType.ArrayProperty:
                                p.reference = arrayTypeProp.reference;
                                break;
                            case PropertyType.ByteProperty:
                                //if (arrayTypeProp.reference == "")
                                if (arrayTypeProp.reference == "Class")
                                    p.reference = arrayTypeProp.type.ToString();
                                else
                                    p.reference = arrayTypeProp.reference;
                                break;
                            case PropertyType.IntProperty:
                            case PropertyType.FloatProperty:
                            case PropertyType.NameProperty:
                            case PropertyType.BoolProperty:
                            case PropertyType.StrProperty:
                            case PropertyType.StringRefProperty:
                            case PropertyType.DelegateProperty:
                                p.reference = arrayTypeProp.type.ToString();
                                break;
                            case PropertyType.None:
                            case PropertyType.Unknown:
                            default:
                                Debugger.Break();
                                p = null;
                                break;
                        }
                    }
                    else
                    {
                        p = null;
                    }
                    break;
                case "InterfaceProperty":
                default:
                    p = null;
                    break;
            }
            if (p != null && ((UnrealFlags.EPropertyFlags)BitConverter.ToUInt64(entry.Data, 24)).HasFlag(UnrealFlags.EPropertyFlags.Transient))
            {
                //Transient
                p.transient = true;
            }
            return p;
        }
        #endregion
    }
}
