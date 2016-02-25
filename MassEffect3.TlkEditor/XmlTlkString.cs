using System;
using System.IO;
using System.Xml.Serialization;

namespace MassEffect3.TlkEditor
{
	[XmlType("XmlTlkString")]
	public struct XmlTlkString : IComparable<XmlTlkString>, IComparable
	{
		[XmlIgnore]
		public const string EmptyText = "-1";

		public XmlTlkString(int id, string text = EmptyText, int position = 0)
			: this()
		{
			Id = id;
			Position = position;
			Text = text;
		}

		public XmlTlkString(BinaryReader reader, int position = 0)
			: this()
		{
			Id = reader.ReadInt32();
			BitOffset = reader.ReadInt32();
			Position = position;
		}

		[XmlIgnore]
		public int BitOffset { get; set; }

		[XmlAttribute("id")]
		public int Id { get; set; }

		//[XmlAttribute("position")]
		[XmlIgnore]
		public int Position { get; set; }

		[XmlIgnore]
		public int StringStart { get; set; }

		[XmlText]
		public string Text { get; set; }

		public int CompareTo(object other)
		{
			var entry = (XmlTlkString)other;

			return Position.CompareTo(entry.Position);
		}

		public int CompareTo(XmlTlkString other)
		{
			return Position.CompareTo(other.Position);
		}
	}
}