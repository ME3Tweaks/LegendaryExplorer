using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using KFreonLib.MEDirectories;
using Newtonsoft.Json;

namespace ME3LibWV
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
        
        public static byte[] getDefaultClassValue(PCCPackage pcc, string className, bool fullProps = false)
        {
            if (Structs.ContainsKey(className))
            {
                bool isImmutable = ImmutableStructs.Contains(className);
                ClassInfo info = Structs[className];
                PCCPackage importPCC = new PCCPackage(Path.Combine(ME3Directory.gamePath, @"BIOGame\" + info.pccPath));
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
                    string propName = importPCC.GetName(p.Name);
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
                importPCC.Source.Close();
                return m.ToArray();
            }
            else if (Classes.ContainsKey(className))
            {
                ClassInfo info = Structs[className];
                PCCPackage importPCC = new PCCPackage(Path.Combine(ME3Directory.gamePath, @"BIOGame\" + info.pccPath));
                PCCPackage.ExportEntry entry = pcc.Exports[info.exportIndex + 1];
                List<PropertyReader.Property> Props = PropertyReader.getPropList(importPCC, entry);
                MemoryStream m = new MemoryStream(entry.Datasize - 4);
                foreach (PropertyReader.Property p in Props)
                {
                    if (!info.properties.ContainsKey(importPCC.GetName(p.Name)))
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
    }
}
