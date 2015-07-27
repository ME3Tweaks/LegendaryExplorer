using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Runtime.Serialization;
using ME1Explorer.Unreal;

namespace ME1Explorer.Unreal
{
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public struct ObjectProp
    {
        private string _name;
        private int _nameindex;
        [DesignOnly(true)]
        public string name
        {
            get { return _name; }
            set { _name = value; }
        }

        public int nameindex
        {
            get { return _nameindex; }
            set { _nameindex = value; }
        }
    }

    [TypeConverter(typeof(ExpandableObjectConverter))]
    public struct NameProp
    {
        private string _name;
        private int _nameindex;
        [DesignOnly(true)]
        public string name
        {
            get { return _name; }
            set { _name = value; }
        }

        public int nameindex
        {
            get { return _nameindex; }
            set { _nameindex = value; }
        }
    }

    [TypeConverter(typeof(ExpandableObjectConverter))]
    public struct StructProp
    {
        private string _name;
        private int _nameindex;
        private int[] _data;
        [DesignOnly(true)]
        public string name
        {
            get { return _name; }
            set { _name = value; }
        }

        public int[] data
        {
            get { return _data; }
            set { _data = value; }
        }

        public int nameindex
        {
            get { return _nameindex; }
            set { _nameindex = value; }
        }
    }

    public static class SaltPropertyReader
    {
        public enum Type
        {
            Unknown = -1,
            None = 0,
            StructProperty = 1,
            IntProperty = 2,
            FloatProperty = 3,
            ObjectProperty = 4,
            NameProperty = 5,
            BoolProperty = 6,
            ByteProperty = 7,
            ArrayProperty = 8,
            StrProperty = 9,
            StringRefProperty = 10,
            DelegateProperty = 11
        }


        public class Property
        {
            public string Name;
            public Type TypeVal;
            public int Size;
            public int i;
            public int offsetval;
            public int offend;
            public PropertyValue Value;
            public byte[] raw;
            //types
            //0 = None
            //1 = StructProperty
            //2 = IntProperty
            //3 = FloatProperty
            //4 = ObjectProperty
            //5 = NameProperty
            //6 = BoolProperty
            //7 = ByteProperty
            //8 = ArrayProperty
            //9 = StrProperty
            //10= StringRefProperty
            public Property() { }

        }

        public struct PropertyValue
        {
            public int len;
            public string StringValue;
            public int IntValue;
            public float FloatValue;
            public List<PropertyValue> Array;
        }

        public static List<Property> getPropList(ME1Explorer.PCCObject pcc, byte[] raw)
        {
            Application.DoEvents();
            int start = detectStart(pcc, raw);
            return ReadProp(pcc, raw, start);
        }

        public static string TypeToString(int type)
        {
            switch (type)
            {
                case 1: return "Struct Property";
                case 2: return "Integer Property";
                case 3: return "Float Property";
                case 4: return "Object Property";
                case 5: return "Name Property";
                case 6: return "Bool Property";
                case 7: return "Byte Property";
                case 8: return "Array Property";
                case 9: return "String Property";
                case 10: return "String Ref Property";
                default: return "Unkown/None";
            }
        }

        public static string PropertyToText(Property p, ME1Explorer.PCCObject pcc)
        {
            string s = "";
            s = "Name: " + p.Name;
            s += ", Type: " + TypeToString((int)p.TypeVal);
            s += ", Size: " + p.Size + ",";
            switch (p.TypeVal)
            {
                case Type.StructProperty:
                    s += " \"" + pcc.GetName(p.Value.IntValue) + "\" with " + p.Size + " bytes";
                    break;
                case Type.IntProperty:
                case Type.ObjectProperty:
                case Type.StringRefProperty:
                    s += " Value: " + p.Value.IntValue.ToString();
                    break;
                case Type.BoolProperty:
                    s += " Value: " + (p.raw[24] == 1);
                    break;
                case Type.FloatProperty:
                    s += " Value: " + p.Value.FloatValue;
                    break;
                case Type.NameProperty:
                    s += " " + p.Value.StringValue;
                    break;
                case Type.ByteProperty:
                    s += " Value: \"" + p.Value.StringValue + "\", with int: " + p.Value.IntValue;
                    break;
                case Type.ArrayProperty:
                    s += " bytes"; //Value: " + p.Value.Array.Count.ToString() + " Elements";
                    break;
                case Type.StrProperty:
                    if (p.Value.StringValue.Length == 0)
                        break;
                    s += " Value: " + p.Value.StringValue;
                    break;
            }
            return s;
        }

        public static ME1Explorer.Unreal.CustomProperty PropertyToGrid(Property p, ME1Explorer.PCCObject pcc)
        {
            string cat = p.TypeVal.ToString();
            ME1Explorer.Unreal.CustomProperty pg;
            switch (p.TypeVal)
            {
                case Type.BoolProperty:
                    pg = new ME1Explorer.Unreal.CustomProperty(p.Name, cat, (p.Value.IntValue == 1), typeof(bool), false, true);
                    break;
                case Type.FloatProperty:
                    byte[] buff = BitConverter.GetBytes(p.Value.IntValue);
                    float f = BitConverter.ToSingle(buff, 0);
                    pg = new ME1Explorer.Unreal.CustomProperty(p.Name, cat, f, typeof(float), false, true);
                    break;
                case Type.ByteProperty:
                case Type.NameProperty:
                    NameProp pp = new NameProp();
                    pp.name = pcc.GetName(p.Value.IntValue);
                    pp.nameindex = p.Value.IntValue;
                    pg = new ME1Explorer.Unreal.CustomProperty(p.Name, cat, pp, typeof(NameProp), false, true);
                    break;
                case Type.ObjectProperty:
                    ObjectProp ppo = new ObjectProp();
                    ppo.name = pcc.getObjectName(p.Value.IntValue);
                    //ppo.name = pcc.GetName(pcc.Exports[p.Value.IntValue].name);
                    ppo.nameindex = p.Value.IntValue;
                    pg = new ME1Explorer.Unreal.CustomProperty(p.Name, cat, ppo, typeof(ObjectProp), false, true);
                    break;
                case Type.StructProperty:
                    StructProp ppp = new StructProp();
                    ppp.name = pcc.GetName(p.Value.IntValue);
                    ppp.nameindex = p.Value.IntValue;
                    byte[] buf = new byte[p.Value.Array.Count()];
                    for (int i = 0; i < p.Value.Array.Count(); i++)
                        buf[i] = (byte)p.Value.Array[i].IntValue;
                    List<int> buf2 = new List<int>();
                    for (int i = 0; i < p.Value.Array.Count() / 4; i++)
                        buf2.Add(BitConverter.ToInt32(buf, i * 4));
                    ppp.data = buf2.ToArray();
                    pg = new ME1Explorer.Unreal.CustomProperty(p.Name, cat, ppp, typeof(StructProp), false, true);
                    break;
                default:
                    pg = new ME1Explorer.Unreal.CustomProperty(p.Name, cat, p.Value.IntValue, typeof(int), false, true);
                    break;
            }
            return pg;
        }

        public static List<Property> ReadProp(ME1Explorer.PCCObject pcc, byte[] raw, int start)
        {
            Property p;
            PropertyValue v;
            int sname;
            List<Property> result = new List<Property>();
            int pos = start;
            if (raw.Length - pos < 8)
                return result;
            int name = (int)BitConverter.ToInt64(raw, pos);
            if (!pcc.isName(name))
                return result;
            string t = pcc.GetName(name);
            if (t == "None")
            {
                p = new Property();
                p.Name = t;
                p.TypeVal = Type.None;
                p.i = 0;
                p.offsetval = pos;
                p.Size = 20;
                p.Value = new PropertyValue();
                p.raw = BitConverter.GetBytes((Int64)name);
                p.offend = pos + 20;
                result.Add(p);
                return result;
            }
            int type = (int)BitConverter.ToInt64(raw, pos + 8);
            int size = BitConverter.ToInt32(raw, pos + 16);
            int idx = BitConverter.ToInt32(raw, pos + 20); //Unused
            if (!pcc.isName(type) || size < 0 || size >= raw.Length)
                return result;
            string tp = pcc.GetName(type);
            switch (tp)
            {
                case "BoolProperty":
                    p = new Property();
                    p.TypeVal = Type.BoolProperty;
                    p.Name = t;
                    p.Size = size;
                    p.i = 0;
                    v = new PropertyValue();
                    v.IntValue = BitConverter.ToInt32(raw, pos + 16); //Guess. I haven't seen a true boolproperty yet
                    pos += 28;
                    p.Value = v;
                    break;
                case "NameProperty":
                    p = new Property();
                    p.TypeVal = Type.NameProperty;
                    p.Name = t;
                    p.Size = size;
                    p.i = 0;
                    //pos += 32;
                    v = new PropertyValue();
                    //int tempInt = BitConverter.ToInt32(raw, pos + 24);
                    v.StringValue = pcc.GetName(BitConverter.ToInt32(raw, pos + 24));
                    p.Value = v;
                    pos += 32;
                    //throw new NullReferenceException();
                    break;
                case "IntProperty":
                    p = new Property();
                    p.TypeVal = Type.IntProperty;
                    v = new PropertyValue();
                    v.IntValue = BitConverter.ToInt32(raw, pos + 24);
                    p.Name = t;
                    p.Size = size;
                    p.i = 0;
                    p.offsetval = pos + 24;
                    pos += 28;
                    p.Value = v;
                    break;
                case "DelegateProperty":
                    p = new Property();
                    p.Name = t;
                    p.Size = size;
                    p.TypeVal = Type.DelegateProperty;
                    p.i = 0;
                    p.offsetval = pos + 24;
                    v = new PropertyValue();
                    v.IntValue = BitConverter.ToInt32(raw, pos + 28);
                    v.len = size;
                    v.Array = new List<PropertyValue>();
                    pos += 24;
                    for (int i = 0; i < size; i++)
                    {
                        PropertyValue v2 = new PropertyValue();
                        if (pos < raw.Length)
                            v2.IntValue = raw[pos];
                        v.Array.Add(v2);
                        pos++;
                    }
                    p.Value = v;
                    break;
                case "ArrayProperty":
                    int count = (int)BitConverter.ToInt64(raw, pos + 24);
                    p = new Property();
                    p.Name = t;
                    p.Size = size;
                    p.TypeVal = Type.ArrayProperty;
                    p.i = 0;
                    p.offsetval = pos + 24;
                    v = new PropertyValue();
                    v.IntValue = type;
                    v.len = size - 4;
                    count = v.len;//TODO can be other objects too
                    v.Array = new List<PropertyValue>();
                    pos += 28;
                    for (int i = 0; i < count; i++)
                    {
                        PropertyValue v2 = new PropertyValue();
                        if (pos < raw.Length)
                            v2.IntValue = raw[pos];
                        v.Array.Add(v2);
                        pos++;
                    }
                    p.Value = v;
                    break;
                case "StrProperty":
                    count = (int)BitConverter.ToInt64(raw, pos + 24);
                    p = new Property();
                    p.Name = t;
                    p.Size = size;
                    p.TypeVal = Type.StrProperty;
                    p.i = 0;
                    p.offsetval = pos + 24;
                    //count *= -1;
                    v = new PropertyValue();
                    v.IntValue = type;
                    v.len = count;
                    pos += 28;
                    string s = "";

                    for (int i = 0; i < count - 1; i++)
                    {
                        s += (char)raw[pos];
                        pos++;
                    }
                    pos++;
                    v.StringValue = s;
                    p.Value = v;
                    break;
                case "StructProperty":
                    sname = (int)BitConverter.ToInt64(raw, pos + 24);
                    p = new Property();
                    p.Name = t;
                    p.Size = size;
                    p.TypeVal = Type.StructProperty;
                    p.i = 0;
                    p.offsetval = pos + 24;
                    v = new PropertyValue();
                    v.IntValue = sname;
                    v.len = size;
                    v.Array = new List<PropertyValue>();
                    pos += 32;
                    for (int i = 0; i < size; i += 4)
                    {
                        PropertyValue v2 = new PropertyValue();
                        //if (pos < raw.Length)
                        //    v2.IntValue = raw[pos];
                        //v.Array.Add(v2);
                        //pos++;
                        if (pos < raw.Length)
                            v2.IntValue = BitConverter.ToInt32(raw, pos);
                        v.Array.Add(v2);
                        pos += 4;
                    }
                    p.Value = v;
                    break;
                case "ByteProperty":
                    //sname = (int)BitConverter.ToInt64(raw, pos + 24);
                    sname = BitConverter.ToInt32(raw, pos + 24);
                    p = new Property();
                    p.Name = t;
                    p.Size = size;
                    p.TypeVal = Type.ByteProperty;
                    p.i = 0;
                    p.offsetval = pos + 32;
                    v = new PropertyValue();
                    v.StringValue = pcc.GetName(sname);
                    v.IntValue = BitConverter.ToInt32(raw, pos + 28); 
                    v.len = size;
                    pos += 32;
                    //v.IntValue = (int)BitConverter.ToInt64(raw, pos);
                    //pos += size;
                    p.Value = v;
                    break;
                case "FloatProperty":
                    sname = (int)BitConverter.ToInt64(raw, pos + 24);
                    p = new Property();
                    p.Name = t;
                    p.Size = size;
                    p.TypeVal = Type.FloatProperty;
                    p.i = 0;
                    p.offsetval = 24;
                    v = new PropertyValue();
                    v.FloatValue = BitConverter.ToSingle(raw, pos + 24);
                    v.len = size;
                    pos += 28;
                    p.Value = v;
                    break;
                default:
                    p = new Property();
                    p.Name = t;
                    p.TypeVal = getType(pcc, type);
                    p.i = 0;
                    p.Size = size;
                    p.offsetval = pos + 24;
                    p.Value = ReadValue(pcc, raw, pos + 24, type);
                    pos += p.Value.len + 24;
                    break;
            }
            p.raw = new byte[pos - start];
            p.offend = pos;
            if (pos < raw.Length)
                for (int i = 0; i < pos - start; i++)
                    p.raw[i] = raw[start + i];
            result.Add(p);
            if (pos != start) result.AddRange(ReadProp(pcc, raw, pos));
            return result;
        }

        private static Type getType(ME1Explorer.PCCObject pcc, int type)
        {
            switch (pcc.GetName(type))
            {
                case "None": return Type.None;
                case "StructProperty": return Type.StructProperty;
                case "IntProperty": return Type.IntProperty;
                case "FloatProperty": return Type.FloatProperty;
                case "ObjectProperty": return Type.ObjectProperty;
                case "NameProperty": return Type.NameProperty;
                case "BoolProperty": return Type.BoolProperty;
                case "ByteProperty": return Type.ByteProperty;
                case "ArrayProperty": return Type.ArrayProperty;
                case "DelegateProperty": return Type.DelegateProperty;
                case "StrProperty": return Type.StrProperty;
                case "StringRefProperty": return Type.StringRefProperty;
                default:
                    return Type.Unknown;
            }
        }

        private static PropertyValue ReadValue(ME1Explorer.PCCObject pcc, byte[] raw, int start, int type)
        {
            PropertyValue v = new PropertyValue();
            switch (pcc.GetName(type))
            {
                case "IntProperty":
                case "FloatProperty":
                case "ObjectProperty":
                case "StringRefProperty":
                    v.IntValue = BitConverter.ToInt32(raw, start);
                    v.len = 4;
                    break;
                case "NameProperty":
                    v.IntValue = BitConverter.ToInt32(raw, start);
                    v.len = 8;
                    break;
                case "BoolProperty":
                    if (start < raw.Length)
                        v.IntValue = raw[start];
                    v.len = 1;
                    break;
            }
            return v;
        }

        public static int detectStart(ME1Explorer.PCCObject pcc, byte[] raw)
        {
            int result = 8;
            int test1 = BitConverter.ToInt32(raw, 4);
            if (test1 < 0)
                result = 30;
            else
            {
                int test2 = BitConverter.ToInt32(raw, 8);
                if (pcc.isName(test1) && test2 == 0)
                    result = 4;
                if (pcc.isName(test1) && pcc.isName(test2) && test2 != 0)
                    result = 8;
            }
            return result;
        }
    }

    public static class PropertyReader
    {
        public enum Type
        {
            Unknown = -1,
            None = 0,
            StructProperty = 1,
            IntProperty = 2,
            FloatProperty = 3,
            ObjectProperty = 4,
            NameProperty = 5,
            BoolProperty = 6,
            ByteProperty = 7,
            ArrayProperty = 8,
            StrProperty = 9,
            StringRefProperty = 10,
            DelegateProperty = 11
        }

        public class Property
        {
            public int Name;
            public Type TypeVal;
            public int Size;
            public int i;
            public int offsetval;
            public int offend;
            public PropertyValue Value;
            public byte[] raw;
            //types
            //0 = None
            //1 = StructProperty
            //2 = IntProperty
            //3 = FloatProperty
            //4 = ObjectProperty
            //5 = NameProperty
            //6 = BoolProperty
            //7 = ByteProperty
            //8 = ArrayProperty
            //9 = StrProperty
            //10= StringRefProperty
        }        
        
        public struct PropertyValue
        {
            public int len;
            public string StringValue;
            public int IntValue;
            public List<PropertyValue> Array;
        }

        public static List<Property> getPropList(PCCObject pcc, byte[] raw)
        {
            Application.DoEvents();
            int start = detectStart(pcc, raw);
            return ReadProp(pcc,raw,start);
        }

        public static string TypeToString(int type)
        {
            switch (type)
            {
                case 1: return "Struct Property";
                case 2: return "Integer Property";
                case 3: return "Float Property";
                case 4: return "Object Property";
                case 5: return "Name Property";
                case 6: return "Bool Property";
                case 7: return "Byte Property";
                case 8: return "Array Property";
                case 9: return "String Property";
                case 10: return "String Ref Property";
                default: return "Unkown/None";
            }
        }

        public static string PropertyToText(Property p,PCCObject pcc)
        {
            string s = "";
            s = "Name: " + pcc.Names[p.Name];
            s += " Type: " + TypeToString((int)p.TypeVal);
            s += " Size: " + p.Value.len.ToString();
            switch (p.TypeVal)
            {
                case Type.StructProperty:
                    s += " \"" + pcc.GetName (p.Value.IntValue) + "\" with " + p.Value.Array.Count.ToString() + " bytes";
                    break;
                case Type.IntProperty:   
                case Type.BoolProperty:
                case Type.StringRefProperty :
                    s += " Value: " + p.Value.IntValue.ToString();
                    break;
                case Type.ObjectProperty:
                    s += " Value: " + p.Value.IntValue.ToString() + " ";
                    int v = p.Value.IntValue;
                    if (v == 0)
                        s += "None";
                    else
                        s += pcc.GetClass(v);
                    break;
                case Type.FloatProperty:
                    byte[] buff = BitConverter.GetBytes(p.Value.IntValue);
                    float f = BitConverter.ToSingle(buff,0);
                    s += " Value: " + f.ToString();
                    break;
                case Type.NameProperty:
                    s += " " + pcc.Names[p.Value.IntValue];
                    break;
                case Type.ByteProperty:
                    s += " Value: \"" + p.Value.StringValue + "\" with \"" + pcc.GetName(p.Value.IntValue) + "\"";
                    break;
                case Type.ArrayProperty:
                    s += " bytes"; //Value: " + p.Value.Array.Count.ToString() + " Elements";
                    break;
                case Type.StrProperty:
                    if (p.Value.StringValue.Length == 0)
                        break;
                    s += " Value: " + p.Value.StringValue.Substring(0,p.Value.StringValue.Length - 1);
                    break;
            }
            return s;
        }

        public static CustomProperty PropertyToGrid(Property p, PCCObject pcc)
        {
            string cat = p.TypeVal.ToString();
            CustomProperty pg;
            switch(p.TypeVal)
            {
                case Type.BoolProperty :
                    pg = new CustomProperty(pcc.Names[p.Name], cat, (p.Value.IntValue == 1), typeof(bool), false, true);
                    break;
                case Type.FloatProperty:
                    byte[] buff = BitConverter.GetBytes(p.Value.IntValue);
                    float f = BitConverter.ToSingle(buff, 0);
                    pg = new CustomProperty(pcc.Names[p.Name], cat, f, typeof(float), false, true);
                    break;
                case Type.ByteProperty:
                case Type.NameProperty:
                    NameProp pp = new NameProp();
                    pp.name = pcc.GetName(p.Value.IntValue);
                    pp.nameindex = p.Value.IntValue;
                    pg = new CustomProperty(pcc.Names[p.Name], cat, pp, typeof(NameProp), false, true);
                    break;
                case Type.ObjectProperty:
                    ObjectProp ppo = new ObjectProp();
                    ppo.name = pcc.getObjectName(p.Value.IntValue);
                    ppo.nameindex = p.Value.IntValue;
                    pg = new CustomProperty(pcc.Names[p.Name], cat, ppo, typeof(ObjectProp), false, true);
                    break;
                case Type.StructProperty:
                    StructProp ppp = new StructProp();
                    ppp.name = pcc.GetName(p.Value.IntValue);
                    ppp.nameindex = p.Value.IntValue;
                    byte[] buf = new byte[p.Value.Array.Count()];
                    for (int i = 0; i < p.Value.Array.Count(); i++)
                        buf[i] = (byte)p.Value.Array[i].IntValue;
                    List<int> buf2 = new List<int>();
                    for (int i = 0; i < p.Value.Array.Count() / 4; i++)
                        buf2.Add(BitConverter.ToInt32(buf ,i * 4));
                    ppp.data = buf2.ToArray();
                    pg = new CustomProperty(pcc.Names[p.Name], cat, ppp, typeof(StructProp), false, true);
                    break;                    
                default:
                    pg = new CustomProperty(pcc.Names[p.Name],cat,p.Value.IntValue,typeof(int),false,true);
                    break;
            }
            return pg;
        }

        public static List<Property> ReadProp(PCCObject pcc, byte[] raw, int start)
        {
            Property p;
            PropertyValue v;
            int sname;
            List<Property> result = new List<Property>();
            int pos = start;
            if(raw.Length - pos < 8)
                return result;
            int name = (int)BitConverter.ToInt64(raw, pos);
            if (!pcc.isName(name))
                return result;
            string t = pcc.Names[name];
            if (pcc.Names[name] == "None")
            {
                p = new Property();
                p.Name = name;
                p.TypeVal = Type.None;
                p.i = 0;                
                p.offsetval = pos;
                p.Size = 8;
                p.Value = new PropertyValue();
                p.raw = BitConverter.GetBytes((Int64)name);
                p.offend = pos + 8;
                result.Add(p);
                return result;
            }
            int type = (int)BitConverter.ToInt64(raw, pos + 8);            
            int size = BitConverter.ToInt32(raw, pos + 16);
            int idx = BitConverter.ToInt32(raw, pos + 20);
            if (!pcc.isName(type) || size < 0 || size >= raw.Length)
                return result;
            string tp = pcc.Names[type];
            switch (tp)
            {

                case "DelegateProperty":
                    p = new Property();
                    p.Name = name;
                    p.TypeVal = Type.DelegateProperty;
                    p.i = 0;
                    p.offsetval = pos + 24;
                    v = new PropertyValue();
                    v.IntValue = BitConverter.ToInt32(raw, pos + 28);
                    v.len = size;
                    v.Array = new List<PropertyValue>();
                    pos += 24;
                    for (int i = 0; i < size; i++)
                    {
                        PropertyValue v2 = new PropertyValue();
                        if(pos < raw.Length)
                            v2.IntValue = raw[pos];
                        v.Array.Add(v2);
                        pos ++;
                    }
                    p.Value = v;
                    break;
                case "ArrayProperty":
                    int count = (int)BitConverter.ToInt64(raw, pos + 24);
                    p = new Property();
                    p.Name = name;
                    p.TypeVal = Type.ArrayProperty;
                    p.i = 0;
                    p.offsetval = pos + 24;
                    v = new PropertyValue();
                    v.IntValue = type;
                    v.len = size - 4;
                    count = v.len;//TODO can be other objects too
                    v.Array = new List<PropertyValue>();
                    pos += 28;
                    for (int i = 0; i < count; i++)
                    {
                        PropertyValue v2 = new PropertyValue();
                        if(pos < raw.Length)
                            v2.IntValue = raw[pos];
                        v.Array.Add(v2);
                        pos ++;
                    }
                    p.Value = v;
                    break;
                case "StrProperty":
                    count = (int)BitConverter.ToInt64(raw, pos + 24);
                    p = new Property();
                    p.Name = name;
                    p.TypeVal = Type.StrProperty;
                    p.i = 0;
                    p.offsetval = pos + 24;
                    v = new PropertyValue();
                    v.IntValue = type;
                    v.len = count;
                    pos += 28;
                    string s = "";
                    for (int i = 0; i < count; i++)
                        s += (char)raw[pos++];
                    v.StringValue = s;
                    p.Value = v;
                    break;
                case "StructProperty":
                    sname = (int)BitConverter.ToInt64(raw, pos + 24);
                    p = new Property();
                    p.Name = name;
                    p.TypeVal = Type.StructProperty;
                    p.i = 0;
                    p.offsetval = pos + 24;
                    v = new PropertyValue();
                    v.IntValue = sname;
                    v.len = size;
                    v.Array = new List<PropertyValue>();
                    pos += 32;
                    for (int i = 0; i < size; i++)
                    {
                        PropertyValue v2 = new PropertyValue();
                        if (pos < raw.Length)
                            v2.IntValue = raw[pos];
                        v.Array.Add(v2);
                        pos++;
                    }
                    p.Value = v;
                    break;
                case "ByteProperty":
                    p = new Property();
                    p.Name = name;
                    p.TypeVal = Type.ByteProperty;
                    p.i = idx;
                    p.offsetval = pos + 24;
                    v = new PropertyValue();
                    v.IntValue = raw[pos + 24];
                    v.len = size;
                    pos += 24 + size;
                    p.Value = v;
                    break;
                case "BoolProperty":
                    p = new Property();
                    p.Name = name;
                    p.TypeVal = Type.BoolProperty;
                    p.i = idx;
                    p.offsetval = pos + 24;
                    v = new PropertyValue();
                    v.IntValue = raw[pos + 24];
                    v.len = 4;
                    pos += 28;
                    p.Value = v;
                    break;  
                default:
                    p = new Property();
                    p.Name = name;
                    p.TypeVal = getType(pcc,type);
                    p.i = 0;
                    p.offsetval = pos + 24;
                    p.Value = ReadValue(pcc, raw, pos + 24, type);
                    pos += p.Value.len + 24;
                    break;
            }
            p.raw = new byte[pos - start];
            p.offend = pos;
            if(pos < raw.Length)
                for (int i = 0; i < pos - start; i++) 
                    p.raw[i] = raw[start + i];
            result.Add(p);            
            if(pos!=start) result.AddRange(ReadProp(pcc, raw, pos));
            return result;
        }

        private static Type getType(PCCObject pcc, int type)
        {
            switch (pcc.GetName(type))
            {
                case "None": return Type.None;
                case "StructProperty": return Type.StructProperty;
                case "IntProperty": return Type.IntProperty;
                case "FloatProperty": return Type.FloatProperty;
                case "ObjectProperty": return Type.ObjectProperty;
                case "NameProperty": return Type.NameProperty;
                case "BoolProperty": return Type.BoolProperty;
                case "ByteProperty": return Type.ByteProperty;
                case "ArrayProperty": return Type.ArrayProperty;
                case "DelegateProperty": return Type.DelegateProperty;
                case "StrProperty": return Type.StrProperty;
                case "StringRefProperty": return Type.StringRefProperty;                    
                default:
                    return Type.Unknown;
            }
        }

        private static PropertyValue ReadValue(PCCObject pcc, byte[] raw, int start, int type)
        {
            PropertyValue v = new PropertyValue();
            switch (pcc.Names[type])
            {
                case "IntProperty":
                case "FloatProperty":
                case "ObjectProperty":
                case "StringRefProperty":
                    v.IntValue = BitConverter.ToInt32(raw, start);
                    v.len = 4;
                    break;
                case "NameProperty":
                    v.IntValue = BitConverter.ToInt32(raw, start);
                    v.len = 8;
                    break;
                case "BoolProperty":
                    if(start < raw.Length)
                        v.IntValue = raw[start];
                    v.len = 1;
                    break;
            }
            return v;
        }

        public static int detectStart(PCCObject pcc, byte[] raw)
        {
            int result = 8;
            int test1 = BitConverter.ToInt32(raw, 4);
            if (test1 < 0)
                result = 30;
            else
            {
                int test2 = BitConverter.ToInt32(raw, 8);
                if (pcc.isName(test1) && test2 == 0)
                    result = 4;
                if (pcc.isName(test1) && pcc.isName(test2) && test2 != 0)
                    result = 8;
            }
            return result;
        }
        public static int detectStart(PCCObject pcc, byte[] raw, long flags)
        {
            if ((flags & (long)UnrealFlags.EObjectFlags.HasStack) != 0)
            {
                return 30;
            }
            int result = 8;
            int test1 = BitConverter.ToInt32(raw, 4);
            int test2 = BitConverter.ToInt32(raw, 8);
            if (pcc.isName(test1) && test2 == 0)
                result = 4;
            if (pcc.isName(test1) && pcc.isName(test2) && test2 != 0)
                result = 8;    
            return result;
        }
    }
}
