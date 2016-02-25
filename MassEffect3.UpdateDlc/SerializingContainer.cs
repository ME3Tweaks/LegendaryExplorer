using System.IO;
using Gammtek.Conduit.IO;

namespace MassEffect3.UpdateDlc
{
	public struct SerializingContainer
	{
		public bool IsLoading;
		public MemoryStream Memory;

		private ByteOrder _byteOrder;

		public ByteOrderConverter Converter { get; private set; }

		public ByteOrder ByteOrder
		{
			get { return _byteOrder; }
			set
			{
				Converter = (value == ByteOrder.BigEndian) ? ByteOrderConverter.BigEndian : ByteOrderConverter.LittleEndian;
				_byteOrder = value;
			}
		}

		public SerializingContainer(MemoryStream m, ByteOrder byteOrder = ByteOrder.LittleEndian) : this()
		{
			Memory = m;
			IsLoading = true;
			ByteOrder = byteOrder;
		}

		public static int operator +(SerializingContainer container, int i)
		{
			if (container.IsLoading)
			{
				var buff = new byte[4];
				container.Memory.Read(buff, 0, 4);
				i = container.Converter.ToInt32(buff, 0);
			}
			else
			{
				var buff = container.Converter.GetBytes(i);
				container.Memory.Write(buff, 0, 4);
			}

			return i;
		}

		public static uint operator +(SerializingContainer container, uint i)
		{
			if (container.IsLoading)
			{
				var buff = new byte[4];
				container.Memory.Read(buff, 0, 4);
				i = container.Converter.ToUInt32(buff, 0);
			}
			else
			{
				var buff = container.Converter.GetBytes(i);
				container.Memory.Write(buff, 0, 4);
			}

			return i;
		}

		public static short operator +(SerializingContainer container, short i)
		{
			if (container.IsLoading)
			{
				var buff = new byte[4];
				container.Memory.Read(buff, 0, 2);
				i = container.Converter.ToInt16(buff, 0);
			}
			else
			{
				var buff = container.Converter.GetBytes(i);
				container.Memory.Write(buff, 0, 2);
			}

			return i;
		}

		public static ushort operator +(SerializingContainer container, ushort i)
		{
			if (container.IsLoading)
			{
				var buff = new byte[4];
				container.Memory.Read(buff, 0, 2);
				i = container.Converter.ToUInt16(buff, 0);
			}
			else
			{
				var buff = container.Converter.GetBytes(i);
				container.Memory.Write(buff, 0, 2);
			}

			return i;
		}

		public static byte operator +(SerializingContainer container, byte i)
		{
			if (container.IsLoading)
			{
				i = (byte) container.Memory.ReadByte();
			}
			else
			{
				container.Memory.WriteByte(i);
			}

			return i;
		}

		public static char operator +(SerializingContainer container, char c)
		{
			if (container.IsLoading)
			{
				c = (char) container.Memory.ReadByte();
			}
			else
			{
				container.Memory.WriteByte((byte) c);
			}

			return c;
		}

		public static float operator +(SerializingContainer container, float f)
		{
			if (container.IsLoading)
			{
				var buff = new byte[4];
				container.Memory.Read(buff, 0, 4);
				f = container.Converter.ToSingle(buff, 0);
			}
			else
			{
				var buff = container.Converter.GetBytes(f);
				container.Memory.Write(buff, 0, 4);
			}

			return f;
		}

		public void Seek(int pos, SeekOrigin origin)
		{
			if (Memory != Stream.Null)
			{
				Memory.Seek(pos, origin);
			}
		}

		public int GetPos()
		{
			if (Memory != Stream.Null)
			{
				return (int) Memory.Position;
			}

			return -1;
		}
	}

	public struct SerializingFile
	{
		public bool IsLoading;
		public FileStream Memory;

		private ByteOrder _byteOrder;

		public ByteOrderConverter Converter { get; private set; }

		public ByteOrder ByteOrder
		{
			get { return _byteOrder; }
			set
			{
				Converter = (value == ByteOrder.BigEndian) ? ByteOrderConverter.BigEndian : ByteOrderConverter.LittleEndian;
				_byteOrder = value;
			}
		}

		public SerializingFile(FileStream m, ByteOrder byteOrder = ByteOrder.LittleEndian) : this()
		{
			Memory = m;
			IsLoading = true;
			ByteOrder = byteOrder;
		}

		public static int operator +(SerializingFile container, int i)
		{
			if (container.IsLoading)
			{
				var buff = new byte[4];
				container.Memory.Read(buff, 0, 4);
				i = container.Converter.ToInt32(buff, 0);
			}
			else
			{
				var buff = container.Converter.GetBytes(i);
				container.Memory.Write(buff, 0, 4);
			}

			return i;
		}

		public static uint operator +(SerializingFile container, uint i)
		{
			if (container.IsLoading)
			{
				var buff = new byte[4];
				container.Memory.Read(buff, 0, 4);
				i = container.Converter.ToUInt32(buff, 0);
			}
			else
			{
				var buff = container.Converter.GetBytes(i);
				container.Memory.Write(buff, 0, 4);
			}

			return i;
		}

		public static short operator +(SerializingFile container, short i)
		{
			if (container.IsLoading)
			{
				var buff = new byte[4];
				container.Memory.Read(buff, 0, 2);
				i = container.Converter.ToInt16(buff, 0);
			}
			else
			{
				var buff = container.Converter.GetBytes(i);
				container.Memory.Write(buff, 0, 2);
			}

			return i;
		}

		public static ushort operator +(SerializingFile container, ushort i)
		{
			if (container.IsLoading)
			{
				var buff = new byte[4];
				container.Memory.Read(buff, 0, 2);
				i = container.Converter.ToUInt16(buff, 0);
			}
			else
			{
				var buff = container.Converter.GetBytes(i);
				container.Memory.Write(buff, 0, 2);
			}

			return i;
		}

		public static byte operator +(SerializingFile container, byte i)
		{
			if (container.IsLoading)
			{
				i = (byte) container.Memory.ReadByte();
			}
			else
			{
				container.Memory.WriteByte(i);
			}

			return i;
		}

		public static char operator +(SerializingFile container, char c)
		{
			if (container.IsLoading)
			{
				c = (char) container.Memory.ReadByte();
			}
			else
			{
				container.Memory.WriteByte((byte) c);
			}

			return c;
		}

		public static float operator +(SerializingFile container, float f)
		{
			if (container.IsLoading)
			{
				var buff = new byte[4];
				container.Memory.Read(buff, 0, 4);
				f = container.Converter.ToSingle(buff, 0);
			}
			else
			{
				var buff = container.Converter.GetBytes(f);
				container.Memory.Write(buff, 0, 4);
			}

			return f;
		}

		public void Seek(int pos, SeekOrigin origin)
		{
			if (Memory != Stream.Null)
			{
				Memory.Seek(pos, origin);
			}
		}

		public int GetPos()
		{
			if (Memory != Stream.Null)
			{
				return (int) Memory.Position;
			}

			return 0;
		}
	}
}