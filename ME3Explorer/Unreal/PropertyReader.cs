using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ME3Explorer.Packages;

namespace ME3Explorer.Unreal
{
    #region PropGrid Properties
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
    #endregion

    public struct NameReference
    {
        public int index;
        public int count;
        public string Name;
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
            public int offsetval;
            public int offend;
            public PropertyValue Value;
            public byte[] raw;
        }        

        public struct PropertyValue
        {
            public int len;
            public string StringValue;
            public int IntValue;
            public float FloatValue;
            public NameReference NameValue;
            public List<PropertyValue> Array;
        }

        public static Property getPropOrNull(IExportEntry export, string propName)
        {
            List<Property> props = getPropList(export);
            foreach (Property prop in props)
            {
                if (export.FileRef.getNameEntry(prop.Name) == propName)
                {
                    return prop;
                }
            }
            return null;
        }

        public static Property getPropOrNull(IMEPackage pcc, byte[] data, int start, string propName)
        {
            List<Property> props = ReadProp(pcc, data, start);
            foreach (Property prop in props)
            {
                if (pcc.getNameEntry(prop.Name) == propName)
                {
                    return prop;
                }
            }
            return null;
        }

        public static List<Property> getPropList(IExportEntry export)
        {
            Application.DoEvents();
            int start = detectStart(export.FileRef, export.Data, export.ObjectFlags);
            return ReadProp(export.FileRef, export.Data, start);
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
                default: return "Unknown/None";
            }
        }

        public static string PropertyToText(Property p, IMEPackage pcc)
        {
            string s = "";
            s = "Name: " + pcc.getNameEntry(p.Name);
            s += " Type: " + TypeToString((int)p.TypeVal);
            s += " Size: " + p.Size.ToString();
            switch (p.TypeVal)
            {
                case Type.StructProperty:
                    s += " \"" + pcc.getNameEntry (p.Value.IntValue) + "\" with " + p.Value.Array.Count.ToString() + " bytes";
                    break;
                case Type.IntProperty:                
                case Type.ObjectProperty:
                case Type.StringRefProperty :
                    s += " Value: " + p.Value.IntValue.ToString();
                    break;
                case Type.BoolProperty:
                    s += " Value: " + (p.raw[24] == 1);
                    break;
                case Type.FloatProperty:
                    s += " Value: " + p.Value.FloatValue;
                    break;
                case Type.NameProperty:
                    s += " " + pcc.getNameEntry(p.Value.IntValue);
                    break;
                case Type.ByteProperty:
                    s += " Value: \"" + p.Value.StringValue + "\" with \"" + pcc.getNameEntry(p.Value.IntValue) + "\"";
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

        public static CustomProperty PropertyToGrid(Property p, IMEPackage pcc)
        {
            string cat = p.TypeVal.ToString();
            CustomProperty pg;
            NameProp pp;
            switch (p.TypeVal)
            {
                case Type.BoolProperty :
                    pg = new CustomProperty(pcc.getNameEntry(p.Name), cat, (p.Value.IntValue == 1), typeof(bool), false, true);
                    break;
                case Type.FloatProperty: 
                    pg = new CustomProperty(pcc.getNameEntry(p.Name), cat, p.Value.FloatValue, typeof(float), false, true);
                    break;
                case Type.ByteProperty:
                    if (p.Size != 8)
                    {
                        pg = new CustomProperty(pcc.getNameEntry(p.Name), cat, (byte)p.Value.IntValue, typeof(byte), false, true);
                    }
                    else
                    {

                        pp = new NameProp();
                        pp.name = pcc.getNameEntry(p.Value.IntValue);
                        pp.nameindex = p.Value.IntValue;
                        pg = new CustomProperty(pcc.getNameEntry(p.Name), cat, pp, typeof(NameProp), false, true);
                    }
                    break;
                case Type.NameProperty:
                    pp = new NameProp();
                    pp.name = pcc.getNameEntry(p.Value.IntValue);
                    pp.nameindex = p.Value.IntValue;
                    pg = new CustomProperty(pcc.getNameEntry(p.Name), cat, pp, typeof(NameProp), false, true);
                    break;
                case Type.ObjectProperty:
                    ObjectProp ppo = new ObjectProp();
                    ppo.objectName = pcc.getObjectName(p.Value.IntValue);
                    ppo.index = p.Value.IntValue;
                    pg = new CustomProperty(pcc.getNameEntry(p.Name), cat, ppo, typeof(ObjectProp), false, true);
                    break;
                case Type.StrProperty:
                    pg = new CustomProperty(pcc.getNameEntry(p.Name), cat, p.Value.StringValue, typeof(string), false, true);
                    break;
                case Type.ArrayProperty:
                    pg = new CustomProperty(pcc.getNameEntry(p.Name), cat, BitConverter.ToInt32(p.raw,24) + " elements", typeof(string), false, true);
                    break;
                case Type.StructProperty:
                    string structType = pcc.getNameEntry(p.Value.IntValue);
                    if(structType == "Color") {
                        ColorProp  cp = new ColorProp();
                        cp.name = structType;
                        cp.nameindex = p.Value.IntValue;
                        System.Drawing.Color color = System.Drawing.Color.FromArgb(BitConverter.ToInt32(p.raw, 32));
                        cp.Alpha = color.A;
                        cp.Red = color.R;
                        cp.Green = color.G;
                        cp.Blue = color.B;
                        pg = new CustomProperty(pcc.getNameEntry(p.Name), cat, cp, typeof(ColorProp), false, true);
                    }
                    else if (structType == "Vector")
                    {
                        VectorProp vp = new VectorProp();
                        vp.name = structType;
                        vp.nameindex = p.Value.IntValue;
                        vp.X = BitConverter.ToSingle(p.raw, 32);
                        vp.Y = BitConverter.ToSingle(p.raw, 36);
                        vp.Z = BitConverter.ToSingle(p.raw, 40);
                        pg = new CustomProperty(pcc.getNameEntry(p.Name), cat, vp, typeof(VectorProp), false, true);
                    }
                    else if (structType == "Rotator")
                    {
                        RotatorProp rp = new RotatorProp();
                        rp.name = structType;
                        rp.nameindex = p.Value.IntValue;
                        rp.Pitch = (float)BitConverter.ToInt32(p.raw, 32) * 360f / 65536f;
                        rp.Yaw = (float)BitConverter.ToInt32(p.raw, 36) * 360f / 65536f;
                        rp.Roll = (float)BitConverter.ToInt32(p.raw, 40) * 360f / 65536f;
                        pg = new CustomProperty(pcc.getNameEntry(p.Name), cat, rp, typeof(RotatorProp), false, true);
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
                        pg = new CustomProperty(pcc.getNameEntry(p.Name), cat, lcp, typeof(VectorProp), false, true);
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
                        pg = new CustomProperty(pcc.getNameEntry(p.Name), cat, ppp, typeof(StructProp), false, true);
                    }
                    break;                    
                default:
                    pg = new CustomProperty(pcc.getNameEntry(p.Name),cat,p.Value.IntValue,typeof(int),false,true);
                    break;
            }
            return pg;
        }

        public static List<List<Property>> ReadStructArrayProp(IMEPackage pcc, Property p)
        {
            List<List<Property>> res = new List<List<Property>>();
            int pos = 28;
            int linkCount = BitConverter.ToInt32(p.raw, 24);
            for (int i = 0; i < linkCount; i++)
            {
                List<Property> p2 = ReadProp(pcc, p.raw, pos);
                for (int j = 0; j < p2.Count(); j++)
                {
                    pos += p2[j].raw.Length;
                }
                res.Add(p2);
            }
            return res;
        }

        public static List<Property> ReadProp(IMEPackage pcc, byte[] raw, int start)
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
            string t = pcc.getNameEntry(name);
            p = new Property();
            p.Name = name;
            if (pcc.getNameEntry(name) == "None")
            {
                p.TypeVal = Type.None;         
                p.offsetval = pos;
                p.Size = 8;
                p.Value = new PropertyValue();
                p.raw = BitConverter.GetBytes((long)name);
                p.offend = pos + 8;
                result.Add(p);
                return result;
            }
            int type = (int)BitConverter.ToInt64(raw, pos + 8);            
            p.Size = BitConverter.ToInt32(raw, pos + 16);
            if (!pcc.isName(type) || p.Size < 0 || p.Size >= raw.Length)
                return result;
            string tp = pcc.getNameEntry(type);
            switch (tp)
            {

                case "DelegateProperty":
                    p.TypeVal = Type.DelegateProperty;
                    p.offsetval = pos + 24;
                    v = new PropertyValue();
                    v.IntValue = BitConverter.ToInt32(raw, pos + 28);
                    v.len = p.Size;
                    v.Array = new List<PropertyValue>();
                    pos += 24;
                    for (int i = 0; i < p.Size; i++)
                    {
                        PropertyValue v2 = new PropertyValue();
                        if(pos < raw.Length)
                            v2.IntValue = raw[pos];
                        v.Array.Add(v2);
                        pos++;
                    }
                    p.Value = v;
                    break;
                case "ArrayProperty":
                    int count = BitConverter.ToInt32(raw, pos + 24);
                    p.TypeVal = Type.ArrayProperty;
                    p.offsetval = pos + 24;
                    v = new PropertyValue();
                    v.IntValue = type;
                    v.len = p.Size - 4;
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
                    count = BitConverter.ToInt32(raw, pos + 24);
                    p.TypeVal = Type.StrProperty;
                    p.offsetval = pos + 24;
                    v = new PropertyValue();
                    v.IntValue = type;
                    v.len = count;
                    pos += 28;
                    string s = "";
                    if (count < 0)
                    {
                        count *= -1;
                        for (int i = 1; i < count; i++)
                        {
                            s += (char)raw[pos];
                            pos += 2;
                        }
                        pos += 2;
                    }
                    else
                    {
                        for (int i = 1; i < count; i++)
                        {
                            s += (char)raw[pos];
                            pos++;
                        }
                        pos++;
                    }
                    v.StringValue = s;
                    p.Value = v;
                    break;
                case "StructProperty":
                    sname = BitConverter.ToInt32(raw, pos + 24);
                    p.TypeVal = Type.StructProperty;
                    p.offsetval = pos + 24;
                    v = new PropertyValue();
                    v.IntValue = sname;
                    v.len = p.Size;
                    v.Array = new List<PropertyValue>();
                    pos += 32;
                    for (int i = 0; i < p.Size; i++)
                    {
                        PropertyValue v2 = new PropertyValue();
                        if (pos < raw.Length)
                            v2.IntValue = raw[pos];
                        v.Array.Add(v2);
                        pos++;
                    }
                    p.Value = v;
                    break;
                case "BioMask4Property":
                    p.TypeVal = Type.ByteProperty;
                    p.offsetval = pos + 24;
                    v = new PropertyValue();
                    v.len = p.Size;
                    pos += 24;
                    v.IntValue = raw[pos];
                    pos += p.Size;
                    p.Value = v;
                    break;
                case "ByteProperty":
                    sname = BitConverter.ToInt32(raw, pos + 24);
                    p.TypeVal = Type.ByteProperty;
                    v = new PropertyValue();
                    v.len = p.Size;
                    if (pcc.game == MEGame.ME3)
                    {
                        p.offsetval = pos + 32;
                        v.StringValue = pcc.getNameEntry(sname);
                        pos += 32;
                        if (p.Size == 8)
                        {
                            v.IntValue = BitConverter.ToInt32(raw, pos);
                        }
                        else
                        {
                            v.IntValue = raw[pos];
                        }
                        pos += p.Size;
                    }
                    else
                    {
                        p.offsetval = pos + 24;
                        if (p.Size != 1)
                        {
                            v.StringValue = pcc.getNameEntry(sname);
                            v.IntValue = sname;
                            pos += 32;
                        }
                        else
                        {
                            v.StringValue = "";
                            v.IntValue = raw[pos + 24];
                            pos += 25;
                        }
                    }
                    p.Value = v;
                    break;
                case "FloatProperty":
                    sname = BitConverter.ToInt32(raw, pos + 24);
                    p.TypeVal = Type.FloatProperty;
                    p.offsetval = pos + 24;
                    v = new PropertyValue();
                    v.FloatValue = BitConverter.ToSingle(raw, pos + 24);
                    v.len = p.Size;
                    pos += 28;
                    p.Value = v;
                    break;
                case "BoolProperty":
                    p = new Property();
                    p.Name = name;
                    p.TypeVal = Type.BoolProperty;
                    p.offsetval = pos + 24;
                    v = new PropertyValue();
                    v.IntValue = raw[pos + 24];
                    if (pcc.game == MEGame.ME3)
                    {
                        v.len = 1; 
                    }
                    else
                    {
                        v.len = 4;
                    }
                    pos += v.len + 24;
                    p.Value = v;
                    break;
                default:
                    p.TypeVal = getType(pcc,type);
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

        private static Type getType(IMEPackage pcc, int type)
        {
            switch (pcc.getNameEntry(type))
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

        private static PropertyValue ReadValue(IMEPackage pcc, byte[] raw, int start, int type)
        {
            PropertyValue v = new PropertyValue();
            switch (pcc.getNameEntry(type))
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
                    nameRef.Name = pcc.getNameEntry(nameRef.index);
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
        
        public static int detectStart(IMEPackage pcc, byte[] raw, ulong flags)
        {
            if ((flags & (ulong)UnrealFlags.EObjectFlags.HasStack) != 0)
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

        public static void ImportProperty(IMEPackage pcc, IMEPackage importpcc, Property p, string className, System.IO.MemoryStream m, bool inStruct = false)
        {
            string name = importpcc.getNameEntry(p.Name);
            int idxname = pcc.FindNameOrAdd(name);
            m.Write(BitConverter.GetBytes(idxname), 0, 4);
            m.Write(new byte[4], 0, 4);
            if (name == "None")
                return;
            string type = importpcc.getNameEntry(BitConverter.ToInt32(p.raw, 8));
            int idxtype = pcc.FindNameOrAdd(type);
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
                case "StringRefProperty":
                    m.Write(BitConverter.GetBytes(4), 0, 4);
                    m.Write(new byte[4], 0, 4);
                    m.Write(BitConverter.GetBytes(p.Value.IntValue), 0, 4);
                    break;
                case "NameProperty":
                    m.Write(BitConverter.GetBytes(8), 0, 4);
                    m.Write(new byte[4], 0, 4);
                    m.Write(BitConverter.GetBytes(pcc.FindNameOrAdd(importpcc.getNameEntry(p.Value.IntValue))), 0, 4);
                    //preserve index or whatever the second part of a namereference is
                    m.Write(p.raw, 28, 4);
                    break;
                case "BoolProperty":
                    m.Write(new byte[8], 0, 8);
                    m.WriteByte((byte)p.Value.IntValue);
                    if (pcc.game != MEGame.ME3)
                    {
                        m.Write(new byte[3], 0, 3);
                    }
                    break;
                case "BioMask4Property":
                    m.Write(BitConverter.GetBytes(p.Size), 0, 4);
                    m.Write(new byte[4], 0, 4);
                    m.WriteByte((byte)p.Value.IntValue);
                    break;
                case "ByteProperty":
                    m.Write(BitConverter.GetBytes(p.Size), 0, 4);
                    m.Write(new byte[4], 0, 4);
                    if (pcc.game == MEGame.ME3)
                    {
                        name2 = importpcc.getNameEntry(BitConverter.ToInt32(p.raw, 24));
                        idxname2 = pcc.FindNameOrAdd(name2);
                        m.Write(BitConverter.GetBytes(idxname2), 0, 4);
                        m.Write(new byte[4], 0, 4);
                    }
                    if (p.Size != 1)
                    {
                        m.Write(BitConverter.GetBytes(pcc.FindNameOrAdd(importpcc.getNameEntry(p.Value.IntValue))), 0, 4);
                        m.Write(new byte[4], 0, 4);
                    }
                    else
                    {
                        m.WriteByte(Convert.ToByte(p.Value.IntValue));
                    }
                    break;
                case "DelegateProperty":
                    size = BitConverter.ToInt32(p.raw, 16);
                    if (size == 0xC)
                    {
                        name2 = importpcc.getNameEntry(BitConverter.ToInt32(p.raw, 28));
                        idxname2 = pcc.FindNameOrAdd(name2);
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
                    name2 = p.Value.StringValue + '\0';
                    if (p.Value.len < 0)
                    {
                        m.Write(BitConverter.GetBytes(4 + name2.Length * 2), 0, 4);
                        m.Write(new byte[4], 0, 4);
                        m.Write(BitConverter.GetBytes(-name2.Length), 0, 4);
                        foreach (char c in name2)
                        {
                            m.WriteByte((byte)c);
                            m.WriteByte(0);
                        } 
                    }
                    else
                    {
                        m.Write(BitConverter.GetBytes(4 + name2.Length), 0, 4);
                        m.Write(new byte[4], 0, 4);
                        m.Write(BitConverter.GetBytes(name2.Length), 0, 4);
                        foreach (char c in name2)
                        {
                            m.WriteByte((byte)c);
                        }
                    }
                    break;
                case "StructProperty":
                    size = BitConverter.ToInt32(p.raw, 16);
                    name2 = importpcc.getNameEntry(BitConverter.ToInt32(p.raw, 24));
                    idxname2 = pcc.FindNameOrAdd(name2);
                    pos = 32;
                    Props = new List<Property>();
                    try
                    {
                        Props = ReadProp(importpcc, p.raw, pos);
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
                            ImportProperty(pcc, importpcc, pp, className, m, inStruct);
                    }
                    break;
                case "ArrayProperty":
                    size = BitConverter.ToInt32(p.raw, 16);
                    count = BitConverter.ToInt32(p.raw, 24);
                    PropertyInfo info = ME3UnrealObjectInfo.getPropertyInfo(className, name, inStruct);
                    ArrayType arrayType = ME3UnrealObjectInfo.getArrayType(info);
                    pos = 28;
                    List<Property> AllProps = new List<Property>();

                    if (arrayType == ArrayType.Struct)
                    {
                        for (int i = 0; i < count; i++)
                        {
                            Props = new List<Property>();
                            try
                            {
                                Props = ReadProp(importpcc, p.raw, pos);
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
                    if (AllProps.Count != 0 && (info == null || !ME3UnrealObjectInfo.isImmutable(info.reference)))
                    {
                        foreach (Property pp in AllProps)
                            ImportProperty(pcc, importpcc, pp, className, m, inStruct);
                    }
                    else if (arrayType == ArrayType.Name)
                    {
                        for (int i = 0; i < count; i++)
                        {
                            string s = importpcc.getNameEntry(BitConverter.ToInt32(p.raw, 28 + i * 8));
                            m.Write(BitConverter.GetBytes(pcc.FindNameOrAdd(s)), 0, 4);
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

        public static void ImportImmutableProperty(ME3Package pcc, ME3Package importpcc, Property p, string className, System.IO.MemoryStream m, bool inStruct = false)
        {
            string name = importpcc.getNameEntry(p.Name);
            int idxname = pcc.FindNameOrAdd(name);
            if (name == "None")
                return;
            string type = importpcc.getNameEntry(BitConverter.ToInt32(p.raw, 8));
            int idxtype = pcc.FindNameOrAdd(type);
            string name2;
            int idxname2;
            int size, count, pos;
            List<Property> Props;
            switch (type)
            {
                case "IntProperty":
                case "FloatProperty":
                case "ObjectProperty":
                case "StringRefProperty":
                    m.Write(BitConverter.GetBytes(p.Value.IntValue), 0, 4);
                    break;
                case "NameProperty":
                    m.Write(BitConverter.GetBytes(pcc.FindNameOrAdd(importpcc.getNameEntry(p.Value.IntValue))), 0, 4);
                    //preserve index or whatever the second part of a namereference is
                    m.Write(p.raw, 28, 4);
                    break;
                case "BoolProperty":
                    m.WriteByte((byte)p.Value.IntValue);
                    break;
                case "BioMask4Property":
                    m.WriteByte((byte)p.Value.IntValue);
                    break;
                case "ByteProperty":
                    name2 = importpcc.getNameEntry(BitConverter.ToInt32(p.raw, 24));
                    idxname2 = pcc.FindNameOrAdd(name2);
                    if (p.Size == 8)
                    {
                        m.Write(BitConverter.GetBytes(pcc.FindNameOrAdd(importpcc.getNameEntry(p.Value.IntValue))), 0, 4);
                        m.Write(new byte[4], 0, 4);
                    }
                    else
                    {
                        m.WriteByte(p.raw[32]);
                    }
                    break;
                case "StrProperty":
                    name2 = p.Value.StringValue + '\0';
                    m.Write(BitConverter.GetBytes(-name2.Length), 0, 4);
                    foreach (char c in name2)
                    {
                        m.WriteByte((byte)c);
                        m.WriteByte(0);
                    }
                    break;
                case "StructProperty":
                    size = BitConverter.ToInt32(p.raw, 16);
                    name2 = importpcc.getNameEntry(BitConverter.ToInt32(p.raw, 24));
                    idxname2 = pcc.FindNameOrAdd(name2);
                    pos = 32;
                    Props = new List<Property>();
                    try
                    {
                        Props = ReadProp(importpcc, p.raw, pos);
                    }
                    catch (Exception)
                    {
                    }
                    if (Props.Count == 0 || Props[0].TypeVal == Type.Unknown)
                    {
                        for (int i = 0; i < size; i++)
                            m.WriteByte(p.raw[32 + i]);
                    }
                    else
                    {
                        foreach (Property pp in Props)
                            ImportImmutableProperty(pcc, importpcc, pp, className, m, inStruct);
                    }
                    break;
                case "ArrayProperty":
                    size = BitConverter.ToInt32(p.raw, 16);
                    count = BitConverter.ToInt32(p.raw, 24);
                    ArrayType arrayType = ME3UnrealObjectInfo.getArrayType(className, importpcc.getNameEntry(p.Name), inStruct);
                    pos = 28;
                    List<Property> AllProps = new List<Property>();

                    if (arrayType == ArrayType.Struct)
                    {
                        for (int i = 0; i < count; i++)
                        {
                            Props = new List<Property>();
                            try
                            {
                                Props = ReadProp(importpcc, p.raw, pos);
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
                    m.Write(BitConverter.GetBytes(count), 0, 4);
                    if (AllProps.Count != 0)
                    {
                        foreach (Property pp in AllProps)
                            ImportImmutableProperty(pcc, importpcc, pp, className, m, inStruct);
                    }
                    else if (arrayType == ArrayType.Name)
                    {
                        for (int i = 0; i < count; i++)
                        {
                            string s = importpcc.getNameEntry(BitConverter.ToInt32(p.raw, 28 + i * 8));
                            m.Write(BitConverter.GetBytes(pcc.FindNameOrAdd(s)), 0, 4);
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
                case "DelegateProperty":
                    throw new NotImplementedException(type);
            }
        }
    }
}
