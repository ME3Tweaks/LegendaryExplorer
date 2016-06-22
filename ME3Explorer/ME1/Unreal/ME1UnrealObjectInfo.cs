using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Windows.Forms;
using System.Threading.Tasks;
using ME1Explorer.Unreal;
using KFreonLib.MEDirectories;
using Newtonsoft.Json;
using ME3Explorer.Packages;
using ME3Explorer.Unreal;

namespace ME1Explorer.Unreal
{
    public static class ME1UnrealObjectInfo
    {
        public static Dictionary<string, ClassInfo> Classes = new Dictionary<string, ClassInfo>();
        public static Dictionary<string, ClassInfo> Structs = new Dictionary<string, ClassInfo>();
        public static Dictionary<string, List<string>> Enums = new Dictionary<string, List<string>>();

        public static void loadfromJSON()
        {
            string path = Application.StartupPath + "//exec//ME1ObjectInfo.json";

            try
            {
                if (File.Exists(path))
                {
                    string raw = File.ReadAllText(path);
                    var blob  = JsonConvert.DeserializeAnonymousType(raw, new { Classes, Structs, Enums });
                    Classes = blob.Classes;
                    Structs = blob.Structs;
                    Enums = blob.Enums;
                }
            }
            catch (Exception)
            {
            }
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
                        if (p.type == PropertyReader.Type.StructProperty || p.type == PropertyReader.Type.ArrayProperty)
                        {
                            List<string> vals = getEnumfromProp(p.reference, propName, true);
                            if(vals != null)
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

        public static List<string> getEnumValues(string enumName)
        {
            if (Enums.ContainsKey(enumName))
            {
                return Enums[enumName];
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

        public static bool inheritsFrom(this ME1ExportEntry entry, string baseClass)
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
        //call this method to regenerate ME1ObjectInfo.json
        //Takes a long time (10 to 20 minutes maybe?). Application will be completely unresponsive during that time.
        public static void generateInfo()
        {
            ME1Package pcc;
            string path = ME1Directory.gamePath;
            string[] files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
            string objectName;
            for (int i = 0; i < files.Length; i++)
            {
                if (Path.GetExtension(files[i]) == ".upk" || Path.GetExtension(files[i]) == ".sfm" || Path.GetExtension(files[i]) == ".u")
                {
                    pcc = new ME1Package(files[i]);
                    for (int j = 0; j < pcc.Exports.Count; j++)
                    {
                        if (pcc.Exports[j].ClassName == "Enum")

                        {
                            generateEnumValues(j, pcc);
                        }
                        else if (pcc.Exports[j].ClassName == "Class")
                        {
                            objectName = pcc.Exports[j].ObjectName;
                            if (!Classes.ContainsKey(pcc.Exports[j].ObjectName))
                            {
                                Classes.Add(objectName, generateClassInfo(j, pcc));
                            }
                        }
                        else if (pcc.Exports[j].ClassName == "ScriptStruct")
                        {
                            objectName = pcc.Exports[j].ObjectName;
                            if (!Structs.ContainsKey(pcc.Exports[j].ObjectName))
                            {
                                Structs.Add(objectName, generateClassInfo(j, pcc));
                            }
                        }
                    }
                }
            }
            File.WriteAllText(Application.StartupPath + "//exec//ME1ObjectInfo.json", JsonConvert.SerializeObject(new { Classes = Classes, Structs = Structs, Enums = Enums }));
            MessageBox.Show("Done");
        }

        private static ClassInfo generateClassInfo(int index, ME1Package pcc)
        {
            ClassInfo info = new ClassInfo();
            info.baseClass = pcc.Exports[index].ClassParent;
            foreach (ME1ExportEntry entry in pcc.Exports)
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

        private static void generateEnumValues(int index, ME1Package pcc)
        {
            string enumName = pcc.Exports[index].ObjectName;
            if (!Enums.ContainsKey(enumName))
            {
                List<string> values = new List<string>();
                byte[] buff = pcc.Exports[index].Data;
                int count = BitConverter.ToInt32(buff, 20);
                for (int i = 0; i < count; i++)
                {
                    values.Add(pcc.Names[BitConverter.ToInt32(buff, 24 + i * 8)]);
                }
                Enums.Add(enumName, values);
            }
        }

        private static PropertyInfo getProperty(ME1Package pcc, ME1ExportEntry entry)
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
                case "ClassProperty":
                case "InterfaceProperty":
                case "ComponentProperty":
                default:
                    p = null;
                    break;
            }

            return p;
        } 
        #endregion
    }
}
