using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Explorer.Packages;
using Newtonsoft.Json;

namespace ME3Explorer.Unreal
{
    public static class UDKUnrealObjectInfo
    {

        //public class SequenceObjectInfo
        //{
        //    public List<string> inputLinks;

        //    public SequenceObjectInfo()
        //    {
        //        inputLinks = new List<string>();
        //    }
        //}
        public static bool IsLoaded = false;
        public static Dictionary<string, ClassInfo> Classes = new Dictionary<string, ClassInfo>();
        public static Dictionary<string, ClassInfo> Structs = new Dictionary<string, ClassInfo>();
        //public static Dictionary<string, SequenceObjectInfo> SequenceObjects = new Dictionary<string, SequenceObjectInfo>();
        public static Dictionary<string, List<NameReference>> Enums = new Dictionary<string, List<NameReference>>();

        private static readonly string[] ImmutableStructs = { "Vector", "Color", "LinearColor", "TwoVectors", "Vector4", "Vector2D", "Rotator", "Guid", "Plane", "Box",
            "Quat", "Matrix", "IntPoint", "ActorReference", "ActorReference", "ActorReference", "PolyReference", "AimTransform", "AimTransform", "AimOffsetProfile", "FontCharacter",
            "CoverReference", "CoverInfo", "CoverSlot", "RwVector2", "RwVector3", "RwVector4" };

        private static readonly string jsonPath = Path.Combine(App.ExecFolder, "UDKObjectInfo.json");

       /* public static bool isImmutableStruct(string structName)
        {
            return ImmutableStructs.Contains(structName);
        }

        #region struct default values
        private static byte[] CoverReferenceDefault = { 
            //SlotIdx
            0x78, 0x45, 0, 0, 0, 0, 0, 0, 0xB6, 0x29, 0, 0, 0, 0, 0, 0, 0x04, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            //Direction
            0x28, 0x1B, 0, 0, 0, 0, 0, 0, 0xB6, 0x29, 0, 0, 0, 0, 0, 0, 0x04, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            //Guid
            0xC7, 0x26, 0, 0, 0, 0, 0, 0, 0x17, 0x48, 0, 0, 0, 0, 0, 0, 0x10, 0, 0, 0, 0, 0, 0, 0, 0xC7, 0x26, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            //Actor
            0xF7, 0, 0, 0, 0, 0, 0, 0, 0x62, 0x34, 0, 0, 0, 0, 0, 0, 0x04, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            //None
            0x73, 0x33, 0, 0, 0, 0, 0, 0 };

        private static byte[] PlaneDefault = { 
            //X
            0x09, 0x03, 0, 0, 0, 0, 0, 0, 0x15, 0x01, 0, 0, 0, 0, 0, 0, 0x04, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            //Y
            0x0D, 0x03, 0, 0, 0, 0, 0, 0, 0x15, 0x01, 0, 0, 0, 0, 0, 0, 0x04, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            //Z
            0x12, 0x03, 0, 0, 0, 0, 0, 0, 0x15, 0x01, 0, 0, 0, 0, 0, 0, 0x04, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            //W
            0x04, 0x03, 0, 0, 0, 0, 0, 0, 0x15, 0x01, 0, 0, 0, 0, 0, 0, 0x04, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            //None
            0xF0, 0x01, 0, 0, 0, 0, 0, 0 };
        #endregion*/

        public static void loadfromJSON()
        {
            try
            {
                if (File.Exists(jsonPath))
                {
                    string raw = File.ReadAllText(jsonPath);
                    var blob = JsonConvert.DeserializeAnonymousType(raw, new { Classes, Structs, Enums });
                    //SequenceObjects = blob.SequenceObjects;
                    Classes = blob.Classes;
                    Structs = blob.Structs;
                    Enums = blob.Enums;
                    IsLoaded = true;
                }
            }
            catch (Exception)
            {
                return;
            }
        }

        /*public static SequenceObjectInfo getSequenceObjectInfo(string objectName)
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
                else
                {
                    return getSequenceObjectInfo(Classes[objectName].baseClass);
                }
            }
            return null;
        }
        */
        public static string getEnumTypefromProp(string className, string propName)
        {
            PropertyInfo p = getPropertyInfo(className, propName, false);
            if (p == null)
            {
                p = getPropertyInfo(className, propName, true);
            }
            return p?.reference;
        }/*

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
        }*/

        public static ArrayType getArrayType(string className, string propName, ExportEntry export = null)
        {
            if (!IsLoaded) loadfromJSON();
            PropertyInfo p = getPropertyInfo(className, propName, false, containingExport: export);
            if (p == null)
            {
                p = getPropertyInfo(className, propName, true, containingExport: export);
            }
            if (p == null && export != null)
            {
                if (export.ClassName != "Class" && export.idxClass > 0)
                {
                    export = export.FileRef.getUExport(export.idxClass); //make sure you get actual class
                }
                if (export.ClassName == "Class")
                {
                    ClassInfo currentInfo = generateClassInfo(export);
                    currentInfo.baseClass = export.ClassParent;
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
            if (!IsLoaded) loadfromJSON();
            if (p != null)
            {
                if (p.reference == "NameProperty")
                {
                    return ArrayType.Name;
                }
                else if (Enums.ContainsKey(p.reference))
                {
                    return ArrayType.Enum;
                }
                else if (p.reference == "BoolProperty")
                {
                    return ArrayType.Bool;
                }
                else if (p.reference == "ByteProperty")
                {
                    return ArrayType.Byte;
                }
                else if (p.reference == "StrProperty")
                {
                    return ArrayType.String;
                }
                else if (p.reference == "FloatProperty")
                {
                    return ArrayType.Float;
                }
                else if (p.reference == "IntProperty")
                {
                    return ArrayType.Int;
                }
                else if (Structs.ContainsKey(p.reference))
                {
                    return ArrayType.Struct;
                }
                else
                {
                    return ArrayType.Object;
                }
            }
            else
            {
#if DEBUG
                ArrayTypeLookupJustFailed = true;
#endif
                Debug.WriteLine("UDK Array type lookup failed due to no info provided, defaulting to int");
                if (ME3Explorer.Properties.Settings.Default.PropertyParsingME3UnknownArrayAsObject) return ArrayType.Object;
                return ArrayType.Int;
            }
        }

        public static PropertyInfo getPropertyInfo(string className, string propName, bool inStruct = false, ClassInfo nonVanillaClassInfo = null, bool reSearch = true, ExportEntry containingExport = null)
        {
            if (!IsLoaded) loadfromJSON();
            if (className.StartsWith("Default__"))
            {
                className = className.Substring(9);
            }
            Dictionary<string, ClassInfo> temp = inStruct ? Structs : Classes;
            ClassInfo info;
            bool infoExists = temp.TryGetValue(className, out info);
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
                else
                {
                    foreach (PropertyInfo p in info.properties.Values())
                    {
                        if ((p.type == PropertyType.StructProperty || p.type == PropertyType.ArrayProperty) && reSearch)
                        {
                            PropertyInfo val = getPropertyInfo(p.reference, propName, true, nonVanillaClassInfo, reSearch: true);
                            if (val != null)
                            {
                                return val;
                            }
                        }
                    }
                }
                //look in base class
                if (info.baseClass != null && temp.ContainsKey(info.baseClass))
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
                        ExportEntry parentExport = containingExport.FileRef.getUExport(containingExport.idxClassParent);
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
            if (!IsLoaded) loadfromJSON();
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

        #region Generating
        //call this method to regenerate UDKObjectInfo.json
        //Takes a long time (~5 minutes maybe?). Application will be completely unresponsive during that time.

        /*
        public static void generateInfo()
        {
            Classes = new Dictionary<string, ClassInfo>();
            Structs = new Dictionary<string, ClassInfo>();
            Enums = new Dictionary<string, List<NameReference>>();

            string path = ME3Directory.gamePath;
            string[] files = Directory.GetFiles(Path.Combine(path, "BIOGame"), "*.pcc", SearchOption.AllDirectories);
            string objectName;
            int length = files.Length;
            for (int i = 0; i < length; i++)
            {
                if (files[i].ToLower().EndsWith(".pcc"))
                {
                    using (ME3Package pcc = MEPackageHandler.OpenME3Package(files[i]))
                    {
                        IReadOnlyList<ExportEntry> Exports = pcc.Exports;
                        for (int j = 0; j < Exports.Count; j++)
                        {
                            ExportEntry exportEntry = Exports[j];
                            if (exportEntry.ClassName == "Enum")
                            {
                                generateEnumValues(j, pcc);
                            }
                            else if (exportEntry.ClassName == "Class")
                            {
                                objectName = exportEntry.ObjectName;
                                if (!Classes.ContainsKey(objectName))
                                {
                                    Classes.Add(objectName, generateClassInfo(j, pcc));
                                }
                                if ((objectName.Contains("SeqAct") || objectName.Contains("SeqCond") || objectName.Contains("SequenceLatentAction") ||
                                    objectName == "SequenceOp" || objectName == "SequenceAction" || objectName == "SequenceCondition") && !SequenceObjects.ContainsKey(objectName))
                                {
                                    SequenceObjects.Add(objectName, generateSequenceObjectInfo(j, pcc));
                                }
                            }
                            else if (exportEntry.ClassName == "ScriptStruct")
                            {
                                objectName = exportEntry.ObjectName;
                                if (!Structs.ContainsKey(objectName))
                                {
                                    Structs.Add(objectName, generateClassInfo(j, pcc));
                                }
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
            Classes["SFXSeqAct_AttachToSocket"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = "ME3Explorer_CustomNativeAdditions",
                exportIndex = 0,
                properties =
                {
                    ["PSC2Component"] = new PropertyInfo
                    {
                        type = PropertyType.ObjectProperty, reference = "ParticleSystemComponent"
                    },
                    ["PSC1Component"] = new PropertyInfo
                    {
                        type = PropertyType.ObjectProperty, reference = "ParticleSystemComponent"
                    },
                    ["SkMeshComponent"] = new PropertyInfo
                    {
                        type = PropertyType.ObjectProperty, reference = "SkeletalMeshComponent"
                    },
                    ["TargetPawn"] = new PropertyInfo
                    {
                        type = PropertyType.ObjectProperty, reference = "Actor"
                    },
                    ["AttachSocketName"] = new PropertyInfo
                    {
                        type = PropertyType.NameProperty
                    }
                }
            };

            //Kinkojiro - New Class - BioSeqAct_ShowMedals
            //Sequence object for showing the medals UI
            Classes["BioSeqAct_ShowMedals"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = "ME3Explorer_CustomNativeAdditions",
                exportIndex = 0,
                properties =
                {
                    ["bFromMainMenu"] = new PropertyInfo
                    {
                        type = PropertyType.BoolProperty,
                    },
                    ["m_oGuiReferenced"] = new PropertyInfo
                    {
                        type = PropertyType.ObjectProperty, reference = "GFxMovieInfo"
                    }
                }
            };

            //Kinkojiro - New Class - SFXSeqAct_SetFaceFX
            Classes["SFXSeqAct_SetFaceFX"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = "ME3Explorer_CustomNativeAdditions",
                exportIndex = 0,
                properties =
                {
                    ["m_aoTargets"] = new PropertyInfo
                    {
                        type = PropertyType.ArrayProperty, reference = "Actor"
                    },
                    ["m_pDefaultFaceFXAsset"] = new PropertyInfo
                    {
                        type = PropertyType.ObjectProperty, reference = "FaceFXAsset"
                    }
                }
            };

            #endregion

            File.WriteAllText(jsonPath,
                JsonConvert.SerializeObject(new { SequenceObjects, Classes, Structs, Enums }, Formatting.Indented));
            MessageBox.Show("Done");
        }

        private static SequenceObjectInfo generateSequenceObjectInfo(int i, ME3Package pcc)
        {
            SequenceObjectInfo info = new SequenceObjectInfo();
            var inLinks = pcc.Exports[i + 1].GetProperty<ArrayProperty<StructProperty>>("InputLinks");
            if (inLinks != null)
            {
                foreach (var seqOpInputLink in inLinks)
                {
                    info.inputLinks.Add(seqOpInputLink.GetProp<StrProperty>("LinkDesc").Value);
                }
            }
            return info;
        }
        */
        private static ClassInfo generateClassInfo(int index, UDKPackage pcc)
        {
            ClassInfo info = new ClassInfo
            {
                baseClass = pcc.Exports[index].ClassParent,
                exportIndex = index
            };
            if (pcc.FilePath.Contains("BIOGame"))
            {
                info.pccPath = new string(pcc.FilePath.Skip(pcc.FilePath.LastIndexOf("BIOGame") + 8).ToArray());
            }
            else
            {
                info.pccPath = pcc.FilePath; //used for dynamic resolution of files outside the game directory.
            }

            foreach (ExportEntry entry in pcc.Exports)
            {
                if (entry.idxLink - 1 == index && entry.ClassName != "ScriptStruct" && entry.ClassName != "Enum"
                    && entry.ClassName != "Function" && entry.ClassName != "Const" && entry.ClassName != "State")
                {
                    //Skip if property is transient (only used during execution, will never be in game files)
                    if (/*(BitConverter.ToUInt64(entry.Data, 24) & 0x0000000000002000) == 0 &&*/ !info.properties.ContainsKey(entry.ObjectName))
                    {
                        PropertyInfo p = getProperty(pcc, entry);
                        if (p != null)
                        {
                            info.properties.Add(entry.ObjectName, p);
                        }
                    }
                    //else
                    //{
                    //    //Debug.WriteLine("Skipping property due to flag: " + entry.ObjectName);
                    //}
                }
            }
            return info;
        }

                        /*

        private static void generateEnumValues(int index, ME3Package pcc)
        {
            string enumName = pcc.Exports[index].ObjectName;
            if (!Enums.ContainsKey(enumName))
            {
                var values = new List<NameReference>();
                byte[] buff = pcc.Exports[index].Data;
                //subtract 1 so that we don't get the MAX value, which is an implementation detail
                int count = BitConverter.ToInt32(buff, 20) - 1;
                for (int i = 0; i < count; i++)
                {
                    int enumValIndex = 24 + i * 8;
                    values.Add(new NameReference(pcc.Names[BitConverter.ToInt32(buff, enumValIndex)], BitConverter.ToInt32(buff, enumValIndex + 4)));
                }
                Enums.Add(enumName, values);
            }
        }
        */
        private static PropertyInfo getProperty(UDKPackage pcc, ExportEntry entry)
        {
            if (!IsLoaded) loadfromJSON();
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
                case "ObjectProperty":
                case "ClassProperty":
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
                    PropertyInfo arrayTypeProp = getProperty(pcc, pcc.Exports[BitConverter.ToInt32(entry.Data, 44) - 1]);
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
                                System.Diagnostics.Debugger.Break();
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
            if (p != null && (BitConverter.ToUInt64(entry.Data, 24) & 0x0000000000002000) != 0)
            {
                //Transient
                p.transient = true;
            }
            return p;
        }
        
        internal static ClassInfo generateClassInfo(ExportEntry export)
        {
            if (!IsLoaded) loadfromJSON();
            return generateClassInfo(export.Index, export.FileRef as UDKPackage);
        }
        /*
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
                writer.WriteLine("using Unreal.ME3Enums;");
                writer.WriteLine("using Unreal.ME3Structs;");
                writer.WriteLine("using NameReference = ME3Explorer.Unreal.NameReference;");
                writer.WriteLine();
                writer.WriteBlock("namespace Unreal.ME3Classes", () =>
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
                            foreach ((string propName, PropertyInfo propInfo) in info.properties.Reverse())
                            {
                                if (propInfo.transient || propInfo.type == PropertyType.None)
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
                writer.WriteLine("using Unreal.ME3Enums;");
                writer.WriteLine("using Unreal.ME3Classes;");
                writer.WriteLine("using NameReference = ME3Explorer.Unreal.NameReference;");
                writer.WriteLine();
                writer.WriteBlock("namespace Unreal.ME3Structs", () =>
                {
                    foreach ((string structName, ClassInfo info) in Structs)
                    {
                        writer.WriteBlock($"public class {structName}{(info.baseClass != "Class" ? $" : {info.baseClass}" : "")}", () =>
                        {
                            foreach ((string propName, PropertyInfo propInfo) in info.properties.Reverse())
                            {
                                if (propInfo.transient || propInfo.type == PropertyType.None)
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
                writer.WriteBlock("namespace Unreal.ME3Enums", () =>
                {
                    foreach ((string enumName, List<NameReference> values) in Enums)
                    {
                        writer.WriteBlock($"public enum {enumName}", () =>
                        {
                            foreach (NameReference val in values)
                            {
                                writer.WriteLine($"{val.InstancedString},");
                            }
                        });
                    }
                });
            }
        }
        static string CSharpTypeFromUnrealType(PropertyInfo propInfo)
        {
            switch (propInfo.type)
            {
                case PropertyType.StructProperty:
                    return propInfo.reference;
                case PropertyType.IntProperty:
                    return "int";
                case PropertyType.FloatProperty:
                    return "float";
                case PropertyType.DelegateProperty:
                case PropertyType.ObjectProperty:
                    return "int";
                case PropertyType.NameProperty:
                    return nameof(NameReference);
                case PropertyType.BoolProperty:
                    return "bool";
                case PropertyType.BioMask4Property:
                    return "byte";
                case PropertyType.ByteProperty when propInfo.reference != null && propInfo.reference != "Class" && propInfo.reference != "Object":
                    return propInfo.reference;
                case PropertyType.ByteProperty:
                    return "byte";
                case PropertyType.ArrayProperty:
                    {
                        string type;
                        if (Enum.TryParse(propInfo.reference, out PropertyType arrayType))
                        {
                            type = CSharpTypeFromUnrealType(new PropertyInfo { type = arrayType });
                        }
                        else if (Classes.ContainsKey(propInfo.reference))
                        {
                            //ObjectProperty
                            type = "int";
                        }
                        else
                        {
                            type = propInfo.reference;
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
        */
        #endregion
    }
}
