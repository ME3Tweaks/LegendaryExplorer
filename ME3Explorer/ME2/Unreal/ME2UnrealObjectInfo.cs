using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Windows.Forms;
using System.Threading.Tasks;
using ME2Explorer.Unreal;
using KFreonLib.MEDirectories;
using Newtonsoft.Json;
using ME3Explorer.Packages;
using ME3Explorer.Unreal;
using System.Diagnostics;

namespace ME2Explorer.Unreal
{
    public static class ME2UnrealObjectInfo
    {
        public static Dictionary<string, ClassInfo> Classes = new Dictionary<string, ClassInfo>();
        public static Dictionary<string, ClassInfo> Structs = new Dictionary<string, ClassInfo>();
        public static Dictionary<string, List<string>> Enums = new Dictionary<string, List<string>>();

        public static void loadfromJSON()
        {
            string path = Application.StartupPath + "//exec//ME2ObjectInfo.json";

            try
            {
                if (File.Exists(path))
                {
                    string raw = File.ReadAllText(path);
                    var blob = JsonConvert.DeserializeAnonymousType(raw, new { Classes, Structs, Enums });
                    Classes = blob.Classes;
                    Structs = blob.Structs;
                    Enums = blob.Enums;
                }
            }
            catch
            {
                return;
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

        public static List<string> getEnumfromProp(string className, string propName, bool inStruct = false)
        {
            Dictionary<string, ClassInfo> temp = inStruct ? Structs : Classes;
            if (temp.ContainsKey(className))
            {
                ClassInfo info = temp[className];
                //look in class properties
                if (info.properties.ContainsKey(propName))
                {
                    PropertyInfo p = info.properties[propName];
                    if (Enums.ContainsKey(p.reference))
                    {
                        return Enums[p.reference];
                    }
                }
                //look in structs
                else
                {
                    foreach (PropertyInfo p in info.properties.Values)
                    {
                        if (p.type == PropertyType.StructProperty || p.type == PropertyType.ArrayProperty)
                        {
                            List<string> vals = getEnumfromProp(p.reference, propName, true);
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
                    List<string> vals = getEnumfromProp(info.baseClass, propName, inStruct);
                    if (vals != null)
                    {
                        return vals;
                    }
                }
            }
            return null;
        }

        internal static ClassInfo generateClassInfo(IExportEntry export)
        {
            return generateClassInfo(export.Index, export.FileRef as ME2Package);
        }

        public static List<string> getEnumValues(string enumName, bool includeNone = false)
        {
            if (Enums.ContainsKey(enumName))
            {
                List<string> values = new List<string>(Enums[enumName]);
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
            PropertyInfo p = getPropertyInfo(className, propName, inStruct);
            if (p == null)
            {
                p = getPropertyInfo(className, propName, !inStruct);
            }
            if (p == null && export != null && export.ClassName != "Class" && export.idxClass > 0)
            {
                export = export.FileRef.Exports[export.idxClass - 1]; //make sure you get actual class
                ClassInfo currentInfo;
                switch (export.FileRef.Game)
                {
                    case MEGame.ME1:
                        currentInfo = ME1Explorer.Unreal.ME1UnrealObjectInfo.generateClassInfo(export);
                        break;
                    case MEGame.ME2:
                        currentInfo = ME2Explorer.Unreal.ME2UnrealObjectInfo.generateClassInfo(export);
                        break;
                    case MEGame.ME3:
                    default:
                        currentInfo = ME3UnrealObjectInfo.generateClassInfo(export);
                        break;
                }
                currentInfo.baseClass = export.ClassParent;
                p = getPropertyInfo(className, propName, inStruct, currentInfo);
                if (p == null)
                {
                    p = getPropertyInfo(className, propName, !inStruct, currentInfo);
                }
            }
            return getArrayType(p);
        }

        public static ArrayType getArrayType(PropertyInfo p)
        {
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
                return ArrayType.Int;
            }
        }

        public static PropertyInfo getPropertyInfo(string className, string propName, bool inStruct = false, ClassInfo nonVanillaClassInfo = null, bool reSearch = true)
        {
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
                if (info.properties.ContainsKey(propName))
                {
                    return info.properties[propName];
                }
                //look in structs
                else
                {
                    foreach (PropertyInfo p in info.properties.Values)
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
            }

            //if (reSearch)
            //{
            //    PropertyInfo reAttempt = getPropertyInfo(className, propName, !inStruct, nonVanillaClassInfo, reSearch: false);
            //    return reAttempt; //will be null if not found.
            //}
            return null;
        }

        public static bool inheritsFrom(ME2ExportEntry entry, string baseClass)
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

        private static string[] ImmutableStructs = { "Vector", "Color", "LinearColor", "TwoVectors", "Vector4", "Vector2D", "Rotator", "Guid", "Plane", "Box",
            "Quat", "Matrix", "IntPoint", "ActorReference", "PolyReference", "AimTransform", "NavReference", "FontCharacter", "CovPosInfo",
            "CoverReference", "CoverInfo", "CoverSlot", "BioRwBox", "BioMask4Property", "RwVector2", "RwVector3", "RwVector4", "BioRwBox44" };

        public static bool isImmutableStruct(string structName)
        {
            return ImmutableStructs.Contains(structName);
        }

        #region struct default values
        private readonly static byte[] CoverReferenceDefault = { 
            // >> CoverReference
            //CoverReference: Direction
            0x99, 0x1A, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0xE6, 0x2D, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x04, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,


            //CoverReference: SlotIdx
            0x2C, 0x49, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0xE6, 0x2D, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x04, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,


            // >> NavReference
            //NavReference: Nav (ObjectProperty)
            0xD4, 0x36, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x4A, 0x39, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x04, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0xC9, 0x2A, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,

            //NavReference: Guid (GUID)
            0xC9, 0x4B, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x10, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0xC9, 0x2A, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, //A
            0x00, 0x00, 0x00, 0x00, //B
            0x00, 0x00, 0x00, 0x00, //C
            0x00, 0x00, 0x00, 0x00, //D

             //None
            0x08, 0x83, 0, 0, 0, 0, 0, 0 };

        /*
            //SlotIdx
            0x78, 0x45, 0, 0, 0, 0, 0, 0, 0xB6, 0x29, 0, 0, 0, 0, 0, 0, 0x04, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            //Direction
            0x28, 0x1B, 0, 0, 0, 0, 0, 0, 0xB6, 0x29, 0, 0, 0, 0, 0, 0, 0x04, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            //Guid
            0xC7, 0x26, 0, 0, 0, 0, 0, 0, 0x17, 0x48, 0, 0, 0, 0, 0, 0, 0x10, 0, 0, 0, 0, 0, 0, 0, 0xC7, 0x26, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            //Actor
            0xF7, 0, 0, 0, 0, 0, 0, 0, 0x62, 0x34, 0, 0, 0, 0, 0, 0, 0x04, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
*/

        private static byte[] PlaneDefault = { 
            //TODO: THIS IS COPIED FROM ME3. REBUILD BYTES AS THEY WOULD APPEAR IN THE ENGINE.PCC WITH CORRECT NAME INDICES
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
        #endregion


        public static PropertyCollection getDefaultStructValue(string className, bool stripTransients = true)
        {
            if (Structs.ContainsKey(className))
            {
                bool immutable = UnrealObjectInfo.isImmutable(className, MEGame.ME2);
                ClassInfo info = Structs[className];
                try
                {
                    if (info.pccPath != "ME3Explorer_CustomNativeAdditions")
                    {
                        string filepath = (Path.Combine(ME2Directory.gamePath, @"BioGame\" + info.pccPath));
                        if (File.Exists(info.pccPath))
                        {
                            filepath = info.pccPath; //Used for dynamic lookup
                        }
                        using (ME2Package importPCC = MEPackageHandler.OpenME2Package(filepath))
                        {
                            byte[] buff;
                            //Plane and CoverReference inherit from other structs, meaning they don't have default values (who knows why)
                            //thus, I have hardcoded what those default values should be 
                            if (className == "Plane")
                            {
                                buff = PlaneDefault;
                            }
                            else if (className == "CoverReference")
                            {
                                buff = CoverReferenceDefault;
                            }
                            else
                            {
                                var exportToRead = importPCC.Exports[info.exportIndex];
                                buff = exportToRead.Data.Skip(0x30).ToArray();
                            }
                            PropertyCollection props = PropertyCollection.ReadProps(importPCC, new MemoryStream(buff), className);
                            if (stripTransients)
                            {
                                List<UProperty> toRemove = new List<UProperty>();
                                foreach (var prop in props)
                                {
                                    //remove transient props
                                    if (info.properties.TryGetValue(prop.Name, out PropertyInfo propInfo))
                                    {
                                        if (propInfo.transient)
                                        {
                                            toRemove.Add(prop);
                                        }
                                    }
                                    //if (!info.properties.ContainsKey(prop.Name) && info.baseClass == "Class")
                                    //{
                                    //    toRemove.Add(prop);
                                    //}
                                }
                                foreach (var prop in toRemove)
                                {
                                    Debug.WriteLine("ME2: Get Default Struct value (" + className + ") - removing transient prop: " + prop.Name);
                                    props.Remove(prop);
                                }
                            }
                            return props;
                        }
                    }
                }
                catch
                {
                    return null;
                }
            }
            return null;
        }

        #region Generating
        //call this method to regenerate ME2ObjectInfo.json
        //Takes a long time (10 minutes maybe?). Application will be completely unresponsive during that time.
        public static void generateInfo()
        {
            Dictionary<string, ClassInfo> NewClasses = new Dictionary<string, ClassInfo>();
            Dictionary<string, ClassInfo> NewStructs = new Dictionary<string, ClassInfo>();
            Dictionary<string, List<string>> NewEnums = new Dictionary<string, List<string>>();

            ME2Package pcc;
            string path = ME2Directory.gamePath;
            string[] files = Directory.GetFiles(path, "*.pcc", SearchOption.AllDirectories);
            string objectName;
            for (int i = 0; i < files.Length; i++)
            {
                if (files[i].ToLower().EndsWith(".pcc")/* && files[i].Contains("Engine")*/)
                {
                    pcc = MEPackageHandler.OpenME2Package(files[i]);
                    IReadOnlyList<IExportEntry> Exports = pcc.Exports;
                    IExportEntry exportEntry;
                    for (int j = 0; j < Exports.Count; j++)
                    {
                        exportEntry = Exports[j];
                        if (exportEntry.ClassName == "Enum")
                        {
                            generateEnumValues(j, pcc, NewEnums);
                        }
                        else if (exportEntry.ClassName == "Class")
                        {
                            objectName = exportEntry.ObjectName;
                            if (!NewClasses.ContainsKey(exportEntry.ObjectName))
                            {
                                NewClasses.Add(objectName, generateClassInfo(j, pcc));
                            }
                        }
                        else if (exportEntry.ClassName == "ScriptStruct")
                        {
                            objectName = exportEntry.ObjectName;
                            //if (objectName == "CoverSlot") { Debugger.Break(); }
                            if (!NewStructs.ContainsKey(exportEntry.ObjectName))
                            {
                                NewStructs.Add(objectName, generateClassInfo(j, pcc));
                            }
                        }
                    }
                    //System.Diagnostics.Debug.WriteLine("Releasing " + pcc.FileName);
                    pcc.Release();
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
            catch (Exception e)
            {

            }
            File.WriteAllText(Application.StartupPath + "//exec//ME2ObjectInfo.json", JsonConvert.SerializeObject(new { Classes = NewClasses, Structs = NewStructs, Enums = NewEnums }, Formatting.Indented));
            MessageBox.Show("Done");
        }

        private static ClassInfo generateClassInfo(int index, ME2Package pcc)
        {
            ClassInfo info = new ClassInfo
            {
                baseClass = pcc.Exports[index].ClassParent,
                exportIndex = index
            };
            if (pcc.FileName.Contains("BioGame"))
            {
                info.pccPath = new string(pcc.FileName.Skip(pcc.FileName.LastIndexOf("BioGame") + 8).ToArray());
            }
            else
            {
                info.pccPath = pcc.FileName; //used for dynamic resolution of files outside the game directory.
            }

            foreach (IExportEntry entry in pcc.Exports)
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

        private static void generateEnumValues(int index, ME2Package pcc, Dictionary<string, List<string>> NewEnums = null)
        {
            var enumTable = NewEnums ?? Enums;
            string enumName = pcc.Exports[index].ObjectName;
            if (!enumTable.ContainsKey(enumName))
            {
                List<string> values = new List<string>();
                byte[] buff = pcc.Exports[index].Data;
                int count = BitConverter.ToInt32(buff, 20);
                for (int i = 0; i < count; i++)
                {
                    values.Add(pcc.Names[BitConverter.ToInt32(buff, 24 + i * 8)]);
                }

                enumTable.Add(enumName, values);
            }
        }

        private static PropertyInfo getProperty(ME2Package pcc, IExportEntry entry)
        {
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
                case "ClassProperty":
                case "InterfaceProperty":
                case "ComponentProperty":
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
        #endregion
    }
}
