using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;

namespace ME3LibWV
{
	[TypeConverter(typeof (ExpandableObjectConverter))]
	public struct ObjectProp
	{
		[DesignOnly(true)]
		public string name { get; set; }

		public int nameindex { get; set; }
	}

	[TypeConverter(typeof (ExpandableObjectConverter))]
	public struct NameProp
	{
		[DesignOnly(true)]
		public string name { get; set; }

		public int nameindex { get; set; }
	}

	[TypeConverter(typeof (ExpandableObjectConverter))]
	public struct StructProp
	{
		public int[] data { get; set; }

		[DesignOnly(true)]
		public string name { get; set; }

		public int nameindex { get; set; }
	}

	public static class PropertyReader
	{
		public static int detectStart(PCCPackage pcc, byte[] raw)
		{
			var result = 8;
			var test1 = BitConverter.ToInt32(raw, 4);
			if (test1 < 0)
			{
				result = 30;
			}
			else
			{
				var test2 = BitConverter.ToInt32(raw, 8);
				if (pcc.IsName(test1) && test2 == 0)
				{
					result = 4;
				}
				if (pcc.IsName(test1) && pcc.IsName(test2) && test2 != 0)
				{
					result = 8;
				}
			}
			return result;
		}

		public static int detectStart(PCCPackage pcc, byte[] raw, uint flags)
		{
			if ((flags & 0x02000000) != 0)
			{
				return 30;
			}
			var result = 8;
			var test1 = BitConverter.ToInt32(raw, 4);
			var test2 = BitConverter.ToInt32(raw, 8);
			if (pcc.IsName(test1) && test2 == 0)
			{
				result = 4;
			}
			if (pcc.IsName(test1) && pcc.IsName(test2) && test2 != 0)
			{
				result = 8;
			}
			return result;
		}

		public static List<Property> getPropList(PCCPackage pcc, byte[] raw)
		{
			Application.DoEvents();
			var start = detectStart(pcc, raw);
			return ReadProp(pcc, raw, start);
		}

		public static CustomProperty PropertyToGrid(Property p, PCCPackage pcc)
		{
			var cat = p.TypeVal.ToString();
			CustomProperty pg;
			switch (p.TypeVal)
			{
				case Type.BoolProperty:
					pg = new CustomProperty(pcc.Names[p.Name], cat, (p.Value.IntValue == 1), typeof (bool), false, true);
					break;
				case Type.FloatProperty:
					var buff = BitConverter.GetBytes(p.Value.IntValue);
					var f = BitConverter.ToSingle(buff, 0);
					pg = new CustomProperty(pcc.Names[p.Name], cat, f, typeof (float), false, true);
					break;
				case Type.ByteProperty:
				case Type.NameProperty:
					var pp = new NameProp();
					pp.name = pcc.GetName(p.Value.IntValue);
					pp.nameindex = p.Value.IntValue;
					pg = new CustomProperty(pcc.Names[p.Name], cat, pp, typeof (NameProp), false, true);
					break;
				case Type.ObjectProperty:
					var ppo = new ObjectProp();
					ppo.name = pcc.GetName(p.Value.IntValue);
					ppo.nameindex = p.Value.IntValue;
					pg = new CustomProperty(pcc.Names[p.Name], cat, ppo, typeof (ObjectProp), false, true);
					break;
				case Type.StructProperty:
					var ppp = new StructProp();
					ppp.name = pcc.GetName(p.Value.IntValue);
					ppp.nameindex = p.Value.IntValue;
					var buf = new byte[p.Value.Array.Count()];
					for (var i = 0; i < p.Value.Array.Count(); i++)
					{
						buf[i] = (byte) p.Value.Array[i].IntValue;
					}
					var buf2 = new List<int>();
					for (var i = 0; i < p.Value.Array.Count() / 4; i++)
					{
						buf2.Add(BitConverter.ToInt32(buf, i * 4));
					}
					ppp.data = buf2.ToArray();
					pg = new CustomProperty(pcc.Names[p.Name], cat, ppp, typeof (StructProp), false, true);
					break;
				default:
					pg = new CustomProperty(pcc.Names[p.Name], cat, p.Value.IntValue, typeof (int), false, true);
					break;
			}
			return pg;
		}

		public static string PropertyToText(Property p, PCCPackage pcc)
		{
			var s = "";
			s = "Name: " + pcc.Names[p.Name];
			s += " Type: " + TypeToString((int) p.TypeVal);
			s += " Size: " + p.Value.len;
			switch (p.TypeVal)
			{
				case Type.StructProperty:
					s += " \"" + pcc.GetName(p.Value.IntValue) + "\" with " + p.Value.Array.Count + " bytes";
					break;
				case Type.IntProperty:
				case Type.ObjectProperty:
				case Type.BoolProperty:
				case Type.StringRefProperty:
					s += " Value: " + p.Value.IntValue;
					break;
				case Type.FloatProperty:
					var buff = BitConverter.GetBytes(p.Value.IntValue);
					var f = BitConverter.ToSingle(buff, 0);
					s += " Value: " + f;
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
					{
						break;
					}
					s += " Value: " + p.Value.StringValue.Substring(0, p.Value.StringValue.Length - 1);
					break;
			}
			return s;
		}

		public static List<Property> ReadProp(PCCPackage pcc, byte[] raw, int start)
		{
			Property p;
			PropertyValue v;
			int sname;
			var result = new List<Property>();
			var pos = start;
			if (raw.Length - pos < 8)
			{
				return result;
			}
			var name = (int) BitConverter.ToInt64(raw, pos);
			if (!pcc.IsName(name))
			{
				return result;
			}
			var t = pcc.Names[name];
			if (pcc.Names[name] == "None")
			{
				p = new Property();
				p.Name = name;
				p.TypeVal = Type.None;
				p.i = 0;
				p.offsetval = pos;
				p.Size = 8;
				p.Value = new PropertyValue();
				p.raw = BitConverter.GetBytes((Int64) name);
				p.offend = pos + 8;
				result.Add(p);
				return result;
			}
			var type = (int) BitConverter.ToInt64(raw, pos + 8);
			var size = BitConverter.ToInt32(raw, pos + 16);
			var idx = BitConverter.ToInt32(raw, pos + 20);
			if (!pcc.IsName(type) || size < 0 || size >= raw.Length)
			{
				return result;
			}
			var tp = pcc.Names[type];
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
					for (var i = 0; i < size; i++)
					{
						var v2 = new PropertyValue();
						if (pos < raw.Length)
						{
							v2.IntValue = raw[pos];
						}
						v.Array.Add(v2);
						pos ++;
					}
					p.Value = v;
					break;
				case "ArrayProperty":
					var count = (int) BitConverter.ToInt64(raw, pos + 24);
					p = new Property();
					p.Name = name;
					p.TypeVal = Type.ArrayProperty;
					p.i = 0;
					p.offsetval = pos + 24;
					v = new PropertyValue();
					v.IntValue = type;
					v.len = size - 4;
					count = v.len; //TODO can be other objects too
					v.Array = new List<PropertyValue>();
					pos += 28;
					for (var i = 0; i < count; i++)
					{
						var v2 = new PropertyValue();
						if (pos < raw.Length)
						{
							v2.IntValue = raw[pos];
						}
						v.Array.Add(v2);
						pos ++;
					}
					p.Value = v;
					break;
				case "StrProperty":
					count = (int) BitConverter.ToInt64(raw, pos + 24);
					p = new Property();
					p.Name = name;
					p.TypeVal = Type.StrProperty;
					p.i = 0;
					p.offsetval = pos + 24;
					count *= -1;
					v = new PropertyValue();
					v.IntValue = type;
					v.len = count;
					pos += 28;
					var s = "";
					for (var i = 0; i < count; i++)
					{
						s += (char) raw[pos];
						pos += 2;
					}
					v.StringValue = s;
					p.Value = v;
					break;
				case "StructProperty":
					sname = (int) BitConverter.ToInt64(raw, pos + 24);
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
					for (var i = 0; i < size; i++)
					{
						var v2 = new PropertyValue();
						if (pos < raw.Length)
						{
							v2.IntValue = raw[pos];
						}
						v.Array.Add(v2);
						pos++;
					}
					p.Value = v;
					break;
				case "ByteProperty":
					sname = (int) BitConverter.ToInt64(raw, pos + 24);
					p = new Property();
					p.Name = name;
					p.TypeVal = Type.ByteProperty;
					p.i = 0;
					p.offsetval = pos + 32;
					v = new PropertyValue();
					v.StringValue = pcc.GetName(sname);
					v.len = size;
					pos += 32;
					v.IntValue = (int) BitConverter.ToInt64(raw, pos);
					pos += size;
					p.Value = v;
					break;
				default:
					p = new Property();
					p.Name = name;
					p.TypeVal = getType(pcc, type);
					p.i = 0;
					p.offsetval = pos + 24;
					p.Value = ReadValue(pcc, raw, pos + 24, type);
					pos += p.Value.len + 24;
					break;
			}
			p.raw = new byte[pos - start];
			p.offend = pos;
			if (pos < raw.Length)
			{
				for (var i = 0; i < pos - start; i++)
				{
					p.raw[i] = raw[start + i];
				}
			}
			result.Add(p);
			if (pos != start)
			{
				result.AddRange(ReadProp(pcc, raw, pos));
			}
			return result;
		}

		public static string TypeToString(int type)
		{
			switch (type)
			{
				case 1:
					return "Struct Property";
				case 2:
					return "Integer Property";
				case 3:
					return "Float Property";
				case 4:
					return "Object Property";
				case 5:
					return "Name Property";
				case 6:
					return "Bool Property";
				case 7:
					return "Byte Property";
				case 8:
					return "Array Property";
				case 9:
					return "String Property";
				case 10:
					return "String Ref Property";
				default:
					return "Unkown/None";
			}
		}

		private static Type getType(PCCPackage pcc, int type)
		{
			switch (pcc.GetName(type))
			{
				case "None":
					return Type.None;
				case "StructProperty":
					return Type.StructProperty;
				case "IntProperty":
					return Type.IntProperty;
				case "FloatProperty":
					return Type.FloatProperty;
				case "ObjectProperty":
					return Type.ObjectProperty;
				case "NameProperty":
					return Type.NameProperty;
				case "BoolProperty":
					return Type.BoolProperty;
				case "ByteProperty":
					return Type.ByteProperty;
				case "ArrayProperty":
					return Type.ArrayProperty;
				case "DelegateProperty":
					return Type.DelegateProperty;
				case "StrProperty":
					return Type.StrProperty;
				case "StringRefProperty":
					return Type.StringRefProperty;
				default:
					return Type.Unknown;
			}
		}

		private static PropertyValue ReadValue(PCCPackage pcc, byte[] raw, int start, int type)
		{
			var v = new PropertyValue();
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
					if (start < raw.Length)
					{
						v.IntValue = raw[start];
					}
					v.len = 1;
					break;
			}
			return v;
		}

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

		public struct PropertyValue
		{
			public List<PropertyValue> Array;
			public int IntValue;
			public int len;
			public string StringValue;
		}

		public class Property
		{
			public int i;
			public int Name;
			public int offend;
			public int offsetval;
			public byte[] raw;
			public int Size;
			public Type TypeVal;
			public PropertyValue Value;
		}
	}
}
