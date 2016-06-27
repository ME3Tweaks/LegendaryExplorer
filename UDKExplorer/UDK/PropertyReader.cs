using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using static ME3LibWV.UnrealObjectInfo;

namespace UDKExplorer.UDK
{
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public struct ObjectProp
    {
        private string _name;
        private int _nameindex;
        [DesignOnly(true)]
        public string objectName
        {
            get { return _name; }
            set { _name = value; }
        }

        public int index
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

    [TypeConverter(typeof(ExpandableObjectConverter))]
    public struct ColorProp
    {
        private string _name;
        private int _nameindex;
        private byte _a;
        private byte _r;
        private byte _g;
        private byte _b;
        [DesignOnly(true)]
        public string name
        {
            get { return _name; }
            set { _name = value; }
        }

        public byte Alpha
        {
            get { return _a; }
            set { _a = value; }
        }

        public byte Red
        {
            get { return _r; }
            set { _r = value; }
        }

        public byte Green
        {
            get { return _g; }
            set { _g = value; }
        }

        public byte Blue
        {
            get { return _b; }
            set { _b = value; }
        }

        public int nameindex
        {
            get { return _nameindex; }
            set { _nameindex = value; }
        }
    }

    [TypeConverter(typeof(ExpandableObjectConverter))]
    public struct VectorProp
    {
        private string _name;
        private int _nameindex;
        private float _x;
        private float _y;
        private float _z;
        [DesignOnly(true)]
        public string name
        {
            get { return _name; }
            set { _name = value; }
        }

        public float X
        {
            get { return _x; }
            set { _x = value; }
        }

        public float Y
        {
            get { return _y; }
            set { _y = value; }
        }

        public float Z
        {
            get { return _z; }
            set { _z = value; }
        }

        public int nameindex
        {
            get { return _nameindex; }
            set { _nameindex = value; }
        }
    }

    [TypeConverter(typeof(ExpandableObjectConverter))]
    public struct RotatorProp
    {
        private string _name;
        private int _nameindex;
        private float _pitch;
        private float _yaw;
        private float _roll;
        [DesignOnly(true)]
        public string name
        {
            get { return _name; }
            set { _name = value; }
        }

        public float Pitch
        {
            get { return _pitch; }
            set { _pitch = value; }
        }

        public float Yaw
        {
            get { return _yaw; }
            set { _yaw = value; }
        }

        public float Roll
        {
            get { return _roll; }
            set { _roll = value; }
        }

        public int nameindex
        {
            get { return _nameindex; }
            set { _nameindex = value; }
        }
    }

    [TypeConverter(typeof(ExpandableObjectConverter))]
    public struct LinearColorProp
    {
        private string _name;
        private int _nameindex;
        private float _r;
        private float _g;
        private float _b;
        private float _a;
        [DesignOnly(true)]
        public string name
        {
            get { return _name; }
            set { _name = value; }
        }

        public float Red
        {
            get { return _r; }
            set { _r = value; }
        }

        public float Green
        {
            get { return _g; }
            set { _g = value; }
        }

        public float Blue
        {
            get { return _b; }
            set { _b = value; }
        }

        public float Alpha
        {
            get { return _a; }
            set { _a = value; }
        }

        public int nameindex
        {
            get { return _nameindex; }
            set { _nameindex = value; }
        }
    }

    public struct NameReference
    {
        public int index;
        public int count;
        public String Name;
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
        }        

        public struct PropertyValue
        {
            public int len;
            public string StringValue;
            public int IntValue;
            public NameReference NameValue;
            public List<PropertyValue> Array;
        }

        public static Property getPropOrNull(UDKFile udk, UDKFile.ExportEntry export, string propName)
        {
            List<Property> props = getPropList(udk, export);
            foreach (Property prop in props)
            {
                if (udk.getName(prop.Name) == propName)
                {
                    return prop;
                }
            }
            return null;
        }

        public static Property getPropOrNull(UDKFile udk, byte[] data, int start, string propName)
        {
            List<Property> props = ReadProp(udk, data, start);
            foreach (Property prop in props)
            {
                if (udk.getName(prop.Name) == propName)
                {
                    return prop;
                }
            }
            return null;
        }

        public static List<Property> getPropList(UDKFile udk, UDKFile.ExportEntry export)
        {
            Application.DoEvents();
            int start = detectStart(udk, export.Data, export.ObjectFlags);
            return ReadProp(udk, export.Data, start);
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
                default: return "Unknown/None";
            }
        }

        public static string PropertyToText(Property p,UDKFile udk)
        {
            string s = "";
            s = "Name: " + udk.getName(p.Name);
            s += " Type: " + TypeToString((int)p.TypeVal);
            s += " Size: " + p.Value.len.ToString();
            switch (p.TypeVal)
            {
                case Type.StructProperty:
                    s += " \"" + udk.getName (p.Value.IntValue) + "\" with " + p.Value.Array.Count.ToString() + " bytes";
                    break;
                case Type.IntProperty:                
                case Type.ObjectProperty:
                case Type.BoolProperty:
                    s += " Value: " + p.Value.IntValue.ToString();
                    break;
                case Type.FloatProperty:
                    byte[] buff = BitConverter.GetBytes(p.Value.IntValue);
                    float f = BitConverter.ToSingle(buff,0);
                    s += " Value: " + f.ToString();
                    break;
                case Type.NameProperty:
                    s += " " + udk.getName(p.Value.IntValue);
                    break;
                case Type.ByteProperty:
                    s += " Value: \"" + p.Value.StringValue + "\" with \"" + udk.getName(p.Value.IntValue) + "\"";
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

        public static ME3LibWV.CustomProperty PropertyToGrid(Property p, UDKFile udk)
        {
            string cat = p.TypeVal.ToString();
            ME3LibWV.CustomProperty pg;
            NameProp pp;
            switch (p.TypeVal)
            {
                case Type.BoolProperty :
                    pg = new ME3LibWV.CustomProperty(udk.getName(p.Name), cat, (p.Value.IntValue == 1), typeof(bool), false, true);
                    break;
                case Type.FloatProperty:
                    byte[] buff = BitConverter.GetBytes(p.Value.IntValue);
                    float f = BitConverter.ToSingle(buff, 0);
                    pg = new ME3LibWV.CustomProperty(udk.getName(p.Name), cat, f, typeof(float), false, true);
                    break;
                case Type.ByteProperty:
                    if (p.Size != 8)
                    {
                        pg = new ME3LibWV.CustomProperty(udk.getName(p.Name), cat, (byte)p.Value.IntValue, typeof(byte), false, true);
                    }
                    else
                    {

                        pp = new NameProp();
                        pp.name = udk.getName(p.Value.IntValue);
                        pp.nameindex = p.Value.IntValue;
                        pg = new ME3LibWV.CustomProperty(udk.getName(p.Name), cat, pp, typeof(NameProp), false, true);
                    }
                    break;
                case Type.NameProperty:
                    pp = new NameProp();
                    pp.name = udk.getName(p.Value.IntValue);
                    pp.nameindex = p.Value.IntValue;
                    pg = new ME3LibWV.CustomProperty(udk.getName(p.Name), cat, pp, typeof(NameProp), false, true);
                    break;
                case Type.ObjectProperty:
                    ObjectProp ppo = new ObjectProp();
                    ppo.objectName = udk.getObjectName(p.Value.IntValue);
                    ppo.index = p.Value.IntValue;
                    pg = new ME3LibWV.CustomProperty(udk.getName(p.Name), cat, ppo, typeof(ObjectProp), false, true);
                    break;
                case Type.StrProperty:
                    pg = new ME3LibWV.CustomProperty(udk.getName(p.Name), cat, p.Value.StringValue, typeof(string), false, true);
                    break;
                case Type.ArrayProperty:
                    pg = new ME3LibWV.CustomProperty(udk.getName(p.Name), cat, BitConverter.ToInt32(p.raw,24) + " elements", typeof(string), false, true);
                    break;
                case Type.StructProperty:
                    string structType = udk.getName(p.Value.IntValue);
                    if(structType == "Color") {
                        ColorProp  cp = new ColorProp();
                        cp.name = structType;
                        cp.nameindex = p.Value.IntValue;
                        System.Drawing.Color color = System.Drawing.Color.FromArgb(BitConverter.ToInt32(p.raw, 32));
                        cp.Alpha = color.A;
                        cp.Red = color.R;
                        cp.Green = color.G;
                        cp.Blue = color.B;
                        pg = new ME3LibWV.CustomProperty(udk.getName(p.Name), cat, cp, typeof(ColorProp), false, true);
                    }
                    else if (structType == "Vector")
                    {
                        VectorProp vp = new VectorProp();
                        vp.name = structType;
                        vp.nameindex = p.Value.IntValue;
                        vp.X = BitConverter.ToSingle(p.raw, 32);
                        vp.Y = BitConverter.ToSingle(p.raw, 36);
                        vp.Z = BitConverter.ToSingle(p.raw, 40);
                        pg = new ME3LibWV.CustomProperty(udk.getName(p.Name), cat, vp, typeof(VectorProp), false, true);
                    }
                    else if (structType == "Rotator")
                    {
                        RotatorProp rp = new RotatorProp();
                        rp.name = structType;
                        rp.nameindex = p.Value.IntValue;
                        rp.Pitch = (float)BitConverter.ToInt32(p.raw, 32) * 360f / 65536f;
                        rp.Yaw = (float)BitConverter.ToInt32(p.raw, 36) * 360f / 65536f;
                        rp.Roll = (float)BitConverter.ToInt32(p.raw, 40) * 360f / 65536f;
                        pg = new ME3LibWV.CustomProperty(udk.getName(p.Name), cat, rp, typeof(RotatorProp), false, true);
                    }
                    else if (structType == "LinearColor")
                    {
                        LinearColorProp lcp = new LinearColorProp();
                        lcp.name = structType;
                        lcp.nameindex = p.Value.IntValue;
                        lcp.Red = BitConverter.ToSingle(p.raw, 32);
                        lcp.Green = BitConverter.ToSingle(p.raw, 36);
                        lcp.Blue = BitConverter.ToSingle(p.raw, 40);
                        lcp.Alpha = BitConverter.ToSingle(p.raw, 44);
                        pg = new ME3LibWV.CustomProperty(udk.getName(p.Name), cat, lcp, typeof(VectorProp), false, true);
                    }
                    else {
                        StructProp ppp = new StructProp();
                        ppp.name = structType;
                        ppp.nameindex = p.Value.IntValue;
                        byte[] buf = new byte[p.Value.Array.Count()];
                        for (int i = 0; i < p.Value.Array.Count(); i++)
                            buf[i] = (byte)p.Value.Array[i].IntValue;
                        List<int> buf2 = new List<int>();
                        for (int i = 0; i < p.Value.Array.Count() / 4; i++)
                            buf2.Add(BitConverter.ToInt32(buf ,i * 4));
                        ppp.data = buf2.ToArray();
                        pg = new ME3LibWV.CustomProperty(udk.getName(p.Name), cat, ppp, typeof(StructProp), false, true);
                    }
                    break;                    
                default:
                    pg = new ME3LibWV.CustomProperty(udk.getName(p.Name),cat,p.Value.IntValue,typeof(int),false,true);
                    break;
            }
            return pg;
        }

        public static List<List<Property>> ReadStructArrayProp(UDKFile udk, Property p)
        {
            List<List<Property>> res = new List<List<Property>>();
            int pos = 28;
            int linkCount = BitConverter.ToInt32(p.raw, 24);
            for (int i = 0; i < linkCount; i++)
            {
                List<Property> p2 = ReadProp(udk, p.raw, pos);
                for (int j = 0; j < p2.Count(); j++)
                {
                    pos += p2[j].raw.Length;
                }
                res.Add(p2);
            }
            return res;
        }

        public static List<Property> ReadProp(UDKFile udk, byte[] raw, int start)
        {
            Property p;
            PropertyValue v;
            int sname;
            List<Property> result = new List<Property>();
            int pos = start;
            if(raw.Length - pos < 8)
                return result;
            int name = (int)BitConverter.ToInt64(raw, pos);
            if (!udk.isName(name))
                return result;
            string t = udk.getName(name);
            if (udk.getName(name) == "None")
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
            if (!udk.isName(type) || size < 0 || size >= raw.Length)
                return result;
            string tp = udk.getName(type);
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
                    {
                        s += (char)raw[pos];
                        pos += 1;
                    }
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
                    sname = (int)BitConverter.ToInt64(raw, pos + 24);
                    p = new Property();
                    p.Name = name;
                    p.TypeVal = Type.ByteProperty;
                    p.i = 0;
                    p.offsetval = pos + 32;
                    v = new PropertyValue();
                    v.StringValue = udk.getName(sname);
                    v.len = size;
                    pos += 32;
                    if (size == 8)
                    {
                        v.IntValue = BitConverter.ToInt32(raw, pos);
                    }
                    else
                    {
                        v.IntValue = raw[pos];
                    }
                    pos += size;
                    p.Value = v;
                    break;                     
                default:
                    p = new Property();
                    p.Name = name;
                    p.TypeVal = getType(udk,type);
                    p.i = 0;
                    p.offsetval = pos + 24;
                    p.Value = ReadValue(udk, raw, pos + 24, type);
                    pos += p.Value.len + 24;
                    break;
            }
            p.Size = size;
            p.raw = new byte[pos - start];
            p.offend = pos;
            if(pos < raw.Length)
                for (int i = 0; i < pos - start; i++) 
                    p.raw[i] = raw[start + i];
            result.Add(p);            
            if(pos!=start) result.AddRange(ReadProp(udk, raw, pos));
            return result;
        }

        private static Type getType(UDKFile udk, int type)
        {
            switch (udk.getName(type))
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
                default:
                    return Type.Unknown;
            }
        }

        private static PropertyValue ReadValue(UDKFile udk, byte[] raw, int start, int type)
        {
            PropertyValue v = new PropertyValue();
            switch (udk.getName(type))
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
                    var nameRef = new NameReference();
                    nameRef.index = v.IntValue;
                    nameRef.count = BitConverter.ToInt32(raw, start + 4);
                    nameRef.Name = udk.getName(nameRef.index);
                    if (nameRef.count > 0)
                        nameRef.Name += "_" + (nameRef.count - 1);
                    v.NameValue = nameRef;
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
        
        public static int detectStart(UDKFile udk, byte[] raw, ulong flags)
        {
            if ((flags & (ulong)UnrealFlags.EObjectFlags.HasStack) != 0)
            {
                return 26;
            }
            int result = 8;
            int test1 = BitConverter.ToInt32(raw, 4);
            int test2 = BitConverter.ToInt32(raw, 8);
            if (udk.isName(test1) && test2 == 0)
                result = 4;
            if (udk.isName(test1) && udk.isName(test2) && test2 != 0)
                result = 8;    
            return result;
        }

        public static void ImportProperty(UDKFile udk, UDKFile importudk, Property p, string className, System.IO.MemoryStream m, bool inStruct = false)
        {
            string name = importudk.getName(p.Name);
            int idxname = udk.FindNameOrAdd(name);
            m.Write(BitConverter.GetBytes(idxname), 0, 4);
            m.Write(new byte[4], 0, 4);
            if (name == "None")
                return;
            string type = importudk.getName(BitConverter.ToInt32(p.raw, 8));
            int idxtype = udk.FindNameOrAdd(type);
            m.Write(BitConverter.GetBytes(idxtype), 0, 4);
            m.Write(new byte[4], 0, 4);
            string name2;
            int idxname2;
            int size, count, pos;
            List<Property> Props;
            switch (type)
            {
                case "IntProperty":
                case "FloatProperty":
                case "ObjectProperty":
                case "NameProperty":
                    m.Write(BitConverter.GetBytes(8), 0, 4);
                    m.Write(new byte[4], 0, 4);
                    m.Write(BitConverter.GetBytes(udk.FindNameOrAdd(importudk.getName(p.Value.IntValue))), 0, 4);
                    //preserve index or whatever the second part of a namereference is
                    m.Write(p.raw, 28, 4);
                    break;
                case "BoolProperty":
                    m.Write(new byte[8], 0, 8);
                    m.WriteByte((byte)p.Value.IntValue);
                    break;
                case "ByteProperty":
                    name2 = importudk.getName(BitConverter.ToInt32(p.raw, 24));
                    idxname2 = udk.FindNameOrAdd(name2);
                    m.Write(BitConverter.GetBytes(p.Size), 0, 4);
                    m.Write(new byte[4], 0, 4);
                    m.Write(BitConverter.GetBytes(idxname2), 0, 4);
                    m.Write(new byte[4], 0, 4);
                    if (p.Size == 8)
                    {
                        m.Write(BitConverter.GetBytes(udk.FindNameOrAdd(importudk.getName(p.Value.IntValue))), 0, 4);
                        m.Write(new byte[4], 0, 4);
                    }
                    else
                    {
                        m.WriteByte(p.raw[32]);
                    }
                    break;
                case "DelegateProperty":
                    size = BitConverter.ToInt32(p.raw, 16);
                    if (size == 0xC)
                    {
                        name2 = importudk.getName(BitConverter.ToInt32(p.raw, 28));
                        idxname2 = udk.FindNameOrAdd(name2);
                        m.Write(BitConverter.GetBytes(0xC), 0, 4);
                        m.Write(new byte[4], 0, 4);
                        m.Write(new byte[4], 0, 4);
                        m.Write(BitConverter.GetBytes(idxname2), 0, 4);
                        m.Write(new byte[4], 0, 4);
                    }
                    else
                    {
                        m.Write(BitConverter.GetBytes(size), 0, 4);
                        m.Write(new byte[4], 0, 4);
                        for (int i = 0; i < size; i++)
                            m.WriteByte(p.raw[24 + i]);
                    }
                    break;
                case "StrProperty":
                    name2 = p.Value.StringValue;
                    m.Write(BitConverter.GetBytes(4 + name2.Length), 0, 4);
                    m.Write(new byte[4], 0, 4);
                    m.Write(BitConverter.GetBytes(name2.Length), 0, 4);
                    foreach (char c in name2)
                    {
                        m.WriteByte((byte)c);
                    }
                    break;
                case "StructProperty":
                    size = BitConverter.ToInt32(p.raw, 16);
                    name2 = importudk.getName(BitConverter.ToInt32(p.raw, 24));
                    idxname2 = udk.FindNameOrAdd(name2);
                    pos = 32;
                    Props = new List<Property>();
                    try
                    {
                        Props = ReadProp(importudk, p.raw, pos);
                    }
                    catch (Exception)
                    {
                    }
                    m.Write(BitConverter.GetBytes(size), 0, 4);
                    m.Write(new byte[4], 0, 4);
                    m.Write(BitConverter.GetBytes(idxname2), 0, 4);
                    m.Write(new byte[4], 0, 4);
                    if (Props.Count == 0 || Props[0].TypeVal == Type.Unknown)
                    {
                        for (int i = 0; i < size; i++)
                            m.WriteByte(p.raw[32 + i]);
                    }
                    else
                    {
                        foreach (Property pp in Props)
                            ImportProperty(udk, importudk, pp, className, m, inStruct);
                    }
                    break;
                case "ArrayProperty":
                    size = BitConverter.ToInt32(p.raw, 16);
                    count = BitConverter.ToInt32(p.raw, 24);
                    PropertyInfo info = getPropertyInfo(className, name, inStruct);
                    ArrayType arrayType = getArrayType(info);
                    pos = 28;
                    List<Property> AllProps = new List<Property>();

                    if (arrayType == ArrayType.Struct)
                    {
                        for (int i = 0; i < count; i++)
                        {
                            Props = new List<Property>();
                            try
                            {
                                Props = ReadProp(importudk, p.raw, pos);
                            }
                            catch (Exception)
                            {
                            }
                            AllProps.AddRange(Props);
                            if (Props.Count != 0)
                            {
                                pos = Props[Props.Count - 1].offend;
                            }
                        }
                    }
                    m.Write(BitConverter.GetBytes(size), 0, 4);
                    m.Write(new byte[4], 0, 4);
                    m.Write(BitConverter.GetBytes(count), 0, 4);
                    if (AllProps.Count != 0 && (info == null || !isImmutable(info.reference)))
                    {
                        foreach (Property pp in AllProps)
                            ImportProperty(udk, importudk, pp, className, m, inStruct);
                    }
                    else if (arrayType == ArrayType.Name)
                    {
                        for (int i = 0; i < count; i++)
                        {
                            string s = importudk.getName(BitConverter.ToInt32(p.raw, 28 + i * 8));
                            m.Write(BitConverter.GetBytes(udk.FindNameOrAdd(s)), 0, 4);
                            //preserve index or whatever the second part of a namereference is
                            m.Write(p.raw, 32 + i * 8, 4);
                        }
                    }
                    else
                    {
                        m.Write(p.raw, 28, size - 4);
                    }
                    break;
                default:
                    throw new Exception(type);
            }
        }
        
    }
}
