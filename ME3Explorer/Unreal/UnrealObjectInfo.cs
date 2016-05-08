using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using KFreonLib.MEDirectories;
using Newtonsoft.Json;

namespace ME3Explorer.Unreal
{
    public static class UnrealObjectInfo
    {
        public enum ArrayType
        {
            Object,
            Name,
            Enum,
            Struct,
            Bool,
            String,
            Float,
            Int,
            Byte,
        }

        public class PropertyInfo
        {
            public PropertyReader.Type type;
            public string reference;
        }

        //public struct NameReference
        //{
        //    string name;
        //    int index;
        //}

        //public class DefaultValue
        //{
        //    int intValue;
        //    float floatValue;
        //    NameReference nameValue;
        //    bool boolValue;
        //    byte byteValue;
        //    string stringValue;
        //    Dictionary<string, DefaultValue> structValue;
        //    List<NameReference> nameArrayValue;
        //    List<string> stringArrayValue;
        //    List<bool> boolArrayValue;
        //    List<Dictionary<string, DefaultValue>> structArrayValue;
        //}

        public class ClassInfo
        {
            public Dictionary<string, PropertyInfo> properties;
            public string baseClass;
            //Relative to BIOGame
            public string pccPath;
            public int exportIndex;

            public ClassInfo()
            {
                properties = new Dictionary<string, PropertyInfo>();
            }
        }

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
        public static Dictionary<string, List<string>> Enums = new Dictionary<string, List<string>>();

        public static string[] ImmutableStructs = { "Vector", "Color", "LinearColor", "TwoVectors", "Vector4", "Vector2D", "Rotator", "Guid", "Plane", "Box",
            "Quat", "Matrix", "IntPoint", "ActorReference", "ActorReference", "ActorReference", "PolyReference", "AimTransform", "AimTransform", "NavReference",
            "CoverReference", "CoverInfo", "CoverSlot", "BioRwBox", "BioMask4Property", "RwVector2", "RwVector3", "RwVector4", "BioRwBox44" };

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
        #endregion

        public static void loadfromJSON()
        {
            string path = Application.StartupPath + "//exec//ME3ObjectInfo.json";

            try
            {
                if (File.Exists(path))
                {
                    string raw = File.ReadAllText(path);
                    var blob  = JsonConvert.DeserializeAnonymousType(raw, new { SequenceObjects, Classes, Structs, Enums });
                    SequenceObjects = blob.SequenceObjects;
                    Classes = blob.Classes;
                    Structs = blob.Structs;
                    Enums = blob.Enums;
                }
            }
            catch (Exception)
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
                else
                {
                    return getSequenceObjectInfo(Classes[objectName].baseClass);
                }
            }
            return null;
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
        
        public static ArrayType getArrayType(string className, string propName, bool inStruct = false)
        {
            PropertyInfo p = getPropertyInfo(className, propName, inStruct);
            if (p == null)
            {
                p = getPropertyInfo(className, propName, !inStruct);
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

        public static PropertyInfo getPropertyInfo(string className, string propName, bool inStruct = false)
        {
            if (className.StartsWith("Default__"))
            {
                className = className.Substring(9);
            }
            Dictionary<string, ClassInfo> temp = inStruct ? Structs : Classes;
            if (temp.ContainsKey(className)) //|| (temp = !inStruct ? Structs : Classes).ContainsKey(className))
            {
                ClassInfo info = temp[className];
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
                        if (p.type == PropertyReader.Type.StructProperty || p.type == PropertyReader.Type.ArrayProperty)
                        {
                            PropertyInfo val = getPropertyInfo(p.reference, propName, true);
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
                    PropertyInfo val = getPropertyInfo(info.baseClass, propName, inStruct);
                    if (val != null)
                    {
                        return val;
                    }
                }
            }
            return null;
        }
        
        public static byte[] getDefaultClassValue(PCCObject pcc, string className, bool fullProps = false)
        {
            if (Structs.ContainsKey(className))
            {
                bool isImmutable = ImmutableStructs.Contains(className);
                ClassInfo info = Structs[className];
                PCCObject importPCC = new PCCObject(Path.Combine(ME3Directory.gamePath, @"BIOGame\" + info.pccPath));
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
                    buff = importPCC.Exports[info.exportIndex].Data.Skip(0x24).ToArray();
                }
                List<PropertyReader.Property> Props = PropertyReader.ReadProp(importPCC, buff, 0);
                MemoryStream m = new MemoryStream();
                foreach (PropertyReader.Property p in Props)
                {
                    string propName = importPCC.getNameEntry(p.Name);
                    //check if property is transient, if so, skip (neither of the structs that inherit have transient props)
                    if (info.properties.ContainsKey(propName) || propName == "None" || info.baseClass != "Class")
                    {
                        if (isImmutable && !fullProps)
                        {
                            PropertyReader.ImportImmutableProperty(pcc, importPCC, p, className, m, true);
                        }
                        else
                        {
                            PropertyReader.ImportProperty(pcc, importPCC, p, className, m, true);
                        }
                    }
                }
                return m.ToArray();
            }
            else if (Classes.ContainsKey(className))
            {
                ClassInfo info = Structs[className];
                PCCObject importPCC = new PCCObject(Path.Combine(ME3Directory.gamePath, @"BIOGame\" + info.pccPath));
                PCCObject.ExportEntry entry = pcc.Exports[info.exportIndex + 1];
                List<PropertyReader.Property> Props = PropertyReader.getPropList(importPCC, entry);
                MemoryStream m = new MemoryStream(entry.DataSize - 4);
                foreach (PropertyReader.Property p in Props)
                {
                    if (!info.properties.ContainsKey(importPCC.getNameEntry(p.Name)))
                    {
                        //property is transient
                        continue;
                    }
                    PropertyReader.ImportProperty(pcc, importPCC, p, className, m);
                }
                return m.ToArray();
            }
            return null;
        }

        public static bool inheritsFrom(this PCCObject.IEntry entry, string baseClass)
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

        #region Generating
        //call this method to regenerate ME3ObjectInfo.json
        //Takes a long time (~5 minutes maybe?). Application will be completely unresponsive during that time.
        public static void generateInfo()
        {
            PCCObject pcc;
            string path = ME3Directory.gamePath;
            string[] files = Directory.GetFiles(path, "*.pcc", SearchOption.AllDirectories);
            string objectName;
            int length = files.Length;
            for (int i = 0; i < length; i++)
            {
                if (files[i].ToLower().EndsWith(".pcc"))
                {
                    pcc = new PCCObject(files[i]);
                    for (int j = 0; j < pcc.Exports.Count; j++)
                    {
                        if (pcc.Exports[j].ClassName == "Enum")
                        {
                            generateEnumValues(j, pcc);
                        }
                        else if (pcc.Exports[j].ClassName == "Class")
                        {
                            objectName = pcc.Exports[j].ObjectName;
                            if (!Classes.ContainsKey(objectName))
                            {
                                Classes.Add(objectName, generateClassInfo(j, pcc));
                            }
                            if ((objectName.Contains("SeqAct") || objectName.Contains("SeqCond") ||  objectName.Contains("SequenceLatentAction") ||
                                objectName == "SequenceOp" || objectName == "SequenceAction" || objectName == "SequenceCondition") && !SequenceObjects.ContainsKey(objectName))
                            {
                                SequenceObjects.Add(objectName, generateSequenceObjectInfo(j, pcc));
                            }
                        }
                        else if (pcc.Exports[j].ClassName == "ScriptStruct")
                        {
                            objectName = pcc.Exports[j].ObjectName;
                            if (!Structs.ContainsKey(objectName))
                            {
                                Structs.Add(objectName, generateClassInfo(j, pcc));
                            }
                        }
                    }
                }
                System.Diagnostics.Debug.WriteLine($"{i} of {length} processed");
            }
            File.WriteAllText(Application.StartupPath + "//exec//ME3ObjectInfo.json",
                JsonConvert.SerializeObject(new { SequenceObjects = SequenceObjects, Classes = Classes, Structs = Structs, Enums = Enums }));
            MessageBox.Show("Done");
        }

        private static SequenceObjectInfo generateSequenceObjectInfo(int i, PCCObject pcc)
        {
            SequenceObjectInfo info = new SequenceObjectInfo();
            PropertyReader.Property inputLinks = PropertyReader.getPropOrNull(pcc, pcc.Exports[i + 1], "InputLinks");
            if (inputLinks != null)
            {
                int pos = 28;
                byte[] global = inputLinks.raw;
                int count = BitConverter.ToInt32(global, 24);
                for (int j = 0; j < count; j++)
                {
                    List<PropertyReader.Property> p2 = PropertyReader.ReadProp(pcc, global, pos);

                    info.inputLinks.Add(p2[0].Value.StringValue);
                    for (int k = 0; k < p2.Count(); k++)
                        pos += p2[k].raw.Length;
                }
            }
            return info;
        }

        private static ClassInfo generateClassInfo(int index, PCCObject pcc)
        {
            ClassInfo info = new ClassInfo();
            info.baseClass = pcc.Exports[index].ClassParent;
            info.exportIndex = index;
            info.pccPath = new string(pcc.pccFileName.Skip(pcc.pccFileName.LastIndexOf("BIOGame") + 8).ToArray());
            foreach (PCCObject.ExportEntry entry in pcc.Exports)
            {
                if (entry.idxLink - 1 == index && entry.ClassName != "ScriptStruct" && entry.ClassName != "Enum"
                    && entry.ClassName != "Function" && entry.ClassName != "Const" && entry.ClassName != "State")
                {
                    //Skip if property is transient (only used during execution, will never be in game files)
                    if ((BitConverter.ToUInt64(entry.Data, 24) & 0x0000000000002000) == 0 && !info.properties.ContainsKey(entry.ObjectName))
                    {
                        PropertyInfo p = getProperty(pcc, entry);
                        if (p != null)
                        {
                            info.properties.Add(entry.ObjectName, p);
                        }
                    }
                }
            }
            return info;
        }

        private static void generateEnumValues(int index, PCCObject pcc)
        {
            string enumName = pcc.Exports[index].ObjectName;
            if (!Enums.ContainsKey(enumName))
            {
                List<string> values = new List<string>();
                byte[] buff = pcc.Exports[index].Data;
                //subtract 1 so that we don't get the MAX value, which is an implementation detail
                int count = BitConverter.ToInt32(buff, 20) - 1;
                for (int i = 0; i < count; i++)
                {
                    values.Add(pcc.Names[BitConverter.ToInt32(buff, 24 + i * 8)]);
                }
                Enums.Add(enumName, values);
            }
        }

        private static PropertyInfo getProperty(PCCObject pcc, PCCObject.ExportEntry entry)
        {
            PropertyInfo p = new PropertyInfo();
            switch (entry.ClassName)
            {
                case "IntProperty":
                    p.type = PropertyReader.Type.IntProperty;
                    break;
                case "StringRefProperty":
                    p.type = PropertyReader.Type.StringRefProperty;
                    break;
                case "FloatProperty":
                    p.type = PropertyReader.Type.FloatProperty;
                    break;
                case "BoolProperty":
                    p.type = PropertyReader.Type.BoolProperty;
                    break;
                case "StrProperty":
                    p.type = PropertyReader.Type.StrProperty;
                    break;
                case "NameProperty":
                    p.type = PropertyReader.Type.NameProperty;
                    break;
                case "DelegateProperty":
                    p.type = PropertyReader.Type.DelegateProperty;
                    break;
                case "ObjectProperty":
                case "ClassProperty":
                case "ComponentProperty":
                    p.type = PropertyReader.Type.ObjectProperty;
                    p.reference = pcc.getObjectName(BitConverter.ToInt32(entry.Data, entry.Data.Length - 4));
                    break;
                case "StructProperty":
                    p.type = PropertyReader.Type.StructProperty;
                    p.reference = pcc.getObjectName(BitConverter.ToInt32(entry.Data, entry.Data.Length - 4));
                    break;
                case "BioMask4Property":
                case "ByteProperty":
                    p.type = PropertyReader.Type.ByteProperty;
                    p.reference = pcc.getObjectName(BitConverter.ToInt32(entry.Data, entry.Data.Length - 4));
                    break;
                case "ArrayProperty":
                    p.type = PropertyReader.Type.ArrayProperty;
                    PropertyInfo arrayTypeProp = getProperty(pcc, pcc.Exports[BitConverter.ToInt32(entry.Data, 44) - 1]);
                    if (arrayTypeProp != null)
                    {
                        switch (arrayTypeProp.type)
                        {
                            case PropertyReader.Type.ObjectProperty:
                            case PropertyReader.Type.StructProperty:
                            case PropertyReader.Type.ArrayProperty:
                                p.reference = arrayTypeProp.reference;
                                break;
                            case PropertyReader.Type.ByteProperty:
                                if (arrayTypeProp.reference == "")
                                    p.reference = arrayTypeProp.type.ToString();
                                else
                                    p.reference = arrayTypeProp.reference;
                                break;
                            case PropertyReader.Type.IntProperty:
                            case PropertyReader.Type.FloatProperty:
                            case PropertyReader.Type.NameProperty:
                            case PropertyReader.Type.BoolProperty:
                            case PropertyReader.Type.StrProperty:
                            case PropertyReader.Type.StringRefProperty:
                            case PropertyReader.Type.DelegateProperty:
                                p.reference = arrayTypeProp.type.ToString();
                                break;
                            case PropertyReader.Type.None:
                            case PropertyReader.Type.Unknown:
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

            return p;
        }
        #endregion
    }
}
